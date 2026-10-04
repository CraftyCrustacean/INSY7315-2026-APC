using APCVehicleTracker.API.Options;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using System.Text.RegularExpressions;

namespace APCVehicleTracker.API.Services
{
    public sealed class ImageValidationException : Exception
    {
        public ImageValidationException(string message) : base(message) { }
    }

    public interface IVehicleImageStorage
    {
        /// <summary>Validates, resizes, uploads image + thumbnail. Returns the generated blob name.</summary>
        Task<string> SaveAsync(IFormFile file, CancellationToken ct);

        /// <summary>Deletes the image and its thumbnail.</summary>
        Task DeleteAsync(string blobName, CancellationToken ct);

        Task<(Stream Content, string ContentType)?> OpenReadAsync(string blobName, bool thumbnail, CancellationToken ct);
    }

    public sealed partial class VehicleImageStorage : IVehicleImageStorage
    {
        public const long MaxFileBytes = 5 * 1024 * 1024;
        public const int MaxDimension = 1280;
        public const int ThumbnailDimension = 300;
        private const long MaxPixels = 50_000_000; // decompression-bomb guard

        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private readonly BlobContainerClient _container;

        public VehicleImageStorage(BlobServiceClient serviceClient, IOptions<StorageOptions> options)
        {
            _container = serviceClient.GetBlobContainerClient(options.Value.ContainerName);
        }

        // Server-generated names only: 32 hex chars + .jpg/.png
        [GeneratedRegex(@"^[0-9a-f]{32}\.(jpg|png)$")]
        private static partial Regex BlobNameRegex();

        public static bool IsValidBlobName(string? name) => name != null && BlobNameRegex().IsMatch(name);

        private static string ThumbPath(string blobName) => $"thumbs/{blobName}";

        public async Task<string> SaveAsync(IFormFile file, CancellationToken ct)
        {
            if (file.Length == 0)
                throw new ImageValidationException("One of the files is empty.");

            if (file.Length > MaxFileBytes)
                throw new ImageValidationException("Each image must be 5 MB or smaller.");

            await using var input = file.OpenReadStream();

            // 1. Check the real file signature (magic bytes) - the extension and Content-Type are ignored.
            var header = new byte[8];
            var read = await input.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
            var isPng = IsPng(header, read);
            var isJpeg = IsJpeg(header, read);

            if (!isPng && !isJpeg)
                throw new ImageValidationException("Only JPEG and PNG images are accepted.");

            input.Position = 0;

            // 2. Decode. A file that has the right header but is not a real image fails here.
            Image image;
            try
            {
                var info = await Image.IdentifyAsync(input, ct);

                var decoded = info.Metadata.DecodedImageFormat;
                if ((isPng && decoded != PngFormat.Instance) || (isJpeg && decoded != JpegFormat.Instance))
                    throw new ImageValidationException("The file contents do not match a JPEG or PNG image.");

                if ((long)info.Width * info.Height > MaxPixels)
                    throw new ImageValidationException("The image dimensions are too large.");

                input.Position = 0;
                image = await Image.LoadAsync(input, ct);
            }
            catch (Exception ex) when (ex is ImageFormatException or NotSupportedException)
            {
                throw new ImageValidationException("The file is not a valid JPEG or PNG image.");
            }

            using (image)
            {
                // 3. Normalise: respect EXIF rotation, then strip metadata (GPS etc.).
                image.Mutate(x => x.AutoOrient());
                image.Metadata.ExifProfile = null;
                image.Metadata.XmpProfile = null;
                image.Metadata.IptcProfile = null;

                // 4. Full-size (max 1280px) and thumbnail (max 300px). Never upscale.
                FitWithin(image, MaxDimension);
                using var thumb = image.Clone(_ => { });
                FitWithin(thumb, ThumbnailDimension);

                // 5. Server-generated name. The client file name is never used.
                var extension = isPng ? ".png" : ".jpg";
                var blobName = $"{Guid.NewGuid():N}{extension}";
                var contentType = isPng ? "image/png" : "image/jpeg";

                using var fullStream = await EncodeAsync(image, isPng, ct);
                using var thumbStream = await EncodeAsync(thumb, isPng, ct);

                await UploadAsync(blobName, fullStream, contentType, ct);

                try
                {
                    await UploadAsync(ThumbPath(blobName), thumbStream, contentType, ct);
                }
                catch
                {
                    await _container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: CancellationToken.None);
                    throw;
                }

                return blobName;
            }
        }

        public async Task DeleteAsync(string blobName, CancellationToken ct)
        {
            if (!IsValidBlobName(blobName)) return;

            await _container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: ct);
            await _container.GetBlobClient(ThumbPath(blobName)).DeleteIfExistsAsync(cancellationToken: ct);
        }

        public async Task<(Stream Content, string ContentType)?> OpenReadAsync(
            string blobName, bool thumbnail, CancellationToken ct)
        {
            if (!IsValidBlobName(blobName)) return null;

            var blob = _container.GetBlobClient(thumbnail ? ThumbPath(blobName) : blobName);

            try
            {
                var result = await blob.DownloadStreamingAsync(cancellationToken: ct);
                return (result.Value.Content, result.Value.Details.ContentType ?? "application/octet-stream");
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        private async Task UploadAsync(string name, Stream data, string contentType, CancellationToken ct)
        {
            await _container.GetBlobClient(name).UploadAsync(
                data,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = contentType,
                        // Names are unique GUIDs and content never changes, so cache hard.
                        CacheControl = "private, max-age=31536000, immutable"
                    }
                },
                ct);
        }

        private static void FitWithin(Image image, int max)
        {
            if (image.Width <= max && image.Height <= max) return;

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(max, max),
                Mode = ResizeMode.Max
            }));
        }

        private static async Task<MemoryStream> EncodeAsync(Image image, bool png, CancellationToken ct)
        {
            var ms = new MemoryStream();

            if (png)
                await image.SaveAsync(ms, new PngEncoder(), ct);
            else
                await image.SaveAsync(ms, new JpegEncoder { Quality = 85 }, ct);

            ms.Position = 0;
            return ms;
        }

        private static bool IsJpeg(byte[] h, int n) => n >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF;

        private static bool IsPng(byte[] h, int n) => n >= 8 && h.AsSpan(0, 8).SequenceEqual(PngSignature);
    }

    public static class StorageServiceCollectionExtensions
    {
        /// <summary>
        /// Works locally AND on Azure:
        ///  - Storage:ConnectionString present (user-secrets / Azurite)  -> connection string
        ///  - otherwise Storage:AccountUrl present (App Service)         -> DefaultAzureCredential (managed identity)
        /// </summary>
        public static IServiceCollection AddVehicleImageStorage(
            this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

            services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<StorageOptions>>().Value;

                if (!string.IsNullOrWhiteSpace(options.ConnectionString))
                    return new BlobServiceClient(options.ConnectionString);

                if (!string.IsNullOrWhiteSpace(options.AccountUrl))
                    return new BlobServiceClient(new Uri(options.AccountUrl), new DefaultAzureCredential());

                throw new InvalidOperationException(
                    "Storage is not configured. Set Storage:ConnectionString (local) or Storage:AccountUrl (Azure).");
            });

            services.AddSingleton<IVehicleImageStorage, VehicleImageStorage>();

            return services;
        }
    }
}
