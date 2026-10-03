using APCVehicleTracker.API.DTOs;
using APCVehicleTracker.API.Services;
using APCVehicleTracker.Data;
using APCVehicleTracker.Data.Auth;
using APCVehicleTracker.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APCVehicleTracker.API.Controllers
{
    [ApiController]
    public class VehicleImagesController : ControllerBase
    {
        // 5 files x 5 MB plus multipart overhead.
        private const long MaxRequestBytes = 27L * 1024 * 1024;

        private readonly ApplicationDbContext _context;
        private readonly IVehicleImageStorage _storage;
        private readonly ILogger<VehicleImagesController> _logger;

        public VehicleImagesController(
            ApplicationDbContext context,
            IVehicleImageStorage storage,
            ILogger<VehicleImagesController> logger)
        {
            _context = context;
            _storage = storage;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("api/vehicles/{vehicleId:int}/images")]
        public async Task<IActionResult> GetImages(int vehicleId)
        {
            var images = await _context.VehicleImages
                .AsNoTracking()
                .Where(i => i.VehicleId == vehicleId)
                .OrderBy(i => i.SortOrder)
                .Select(i => new VehicleImageDto
                {
                    ImageId = i.VehicleImageId,
                    BlobName = i.ImageUrl,
                    SortOrder = i.SortOrder,
                    IsPrimary = i.IsPrimary
                })
                .ToListAsync();

            return Ok(images);
        }

        [Authorize(Policy = AuthPolicies.CanEditVehicles)]
        [HttpPost("api/vehicles/{vehicleId:int}/images")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<IActionResult> UploadImages(
            int vehicleId, [FromForm] List<IFormFile> files, CancellationToken ct)
        {
            if (files == null || files.Count == 0)
                return BadRequest("No files were uploaded.");

            var vehicleExists = await _context.Vehicles
                .AsNoTracking()
                .AnyAsync(v => v.VehicleId == vehicleId && v.IsActive, ct);

            if (!vehicleExists) return NotFound("Vehicle not found.");

            var existing = await _context.VehicleImages
                .Where(i => i.VehicleId == vehicleId)
                .ToListAsync(ct);

            if (existing.Count + files.Count > VehicleRules.MaxImages)
                return BadRequest(
                    $"A vehicle can have at most {VehicleRules.MaxImages} images. It currently has {existing.Count}.");

            var uploaded = new List<string>();

            try
            {
                var nextOrder = existing.Count == 0 ? 0 : existing.Max(i => i.SortOrder) + 1;
                var hasPrimary = existing.Any(i => i.IsPrimary);

                foreach (var file in files)
                {
                    var blobName = await _storage.SaveAsync(file, ct);
                    uploaded.Add(blobName);

                    _context.VehicleImages.Add(new VehicleImage
                    {
                        VehicleId = vehicleId,
                        ImageUrl = blobName,              // blob name only, never a URL
                        UploadedDate = DateTime.UtcNow,
                        SortOrder = nextOrder++,
                        IsPrimary = !hasPrimary           // the first image becomes primary
                    });

                    hasPrimary = true;
                }

                await _context.SaveChangesAsync(ct);
            }
            catch (ImageValidationException ex)
            {
                await CleanUpAsync(uploaded);
                return BadRequest(ex.Message);
            }
            catch
            {
                await CleanUpAsync(uploaded);
                throw;
            }

            return await GetImages(vehicleId);
        }

        [Authorize(Policy = AuthPolicies.CanEditVehicles)]
        [HttpPut("api/vehicles/{vehicleId:int}/images/{imageId:int}/primary")]
        public async Task<IActionResult> SetPrimary(int vehicleId, int imageId, CancellationToken ct)
        {
            var images = await _context.VehicleImages
                .Where(i => i.VehicleId == vehicleId)
                .ToListAsync(ct);

            var target = images.FirstOrDefault(i => i.VehicleImageId == imageId);
            if (target == null) return NotFound("Image not found.");
            if (target.IsPrimary) return NoContent();

            // Two saves inside one transaction so the partial unique index is never violated.
            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            foreach (var image in images.Where(i => i.IsPrimary))
                image.IsPrimary = false;
            await _context.SaveChangesAsync(ct);

            target.IsPrimary = true;
            await _context.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
            return NoContent();
        }

        [Authorize(Policy = AuthPolicies.CanEditVehicles)]
        [HttpPut("api/vehicles/{vehicleId:int}/images/order")]
        public async Task<IActionResult> ReorderImages(
            int vehicleId, [FromBody] ReorderImagesRequestDto request, CancellationToken ct)
        {
            var images = await _context.VehicleImages
                .Where(i => i.VehicleId == vehicleId)
                .ToListAsync(ct);

            var requested = request.ImageIds;

            if (requested.Count != images.Count ||
                requested.Distinct().Count() != requested.Count ||
                !images.Select(i => i.VehicleImageId).OrderBy(x => x).SequenceEqual(requested.OrderBy(x => x)))
            {
                return BadRequest("The order must list every image for this vehicle exactly once.");
            }

            for (var position = 0; position < requested.Count; position++)
                images.First(i => i.VehicleImageId == requested[position]).SortOrder = position;

            await _context.SaveChangesAsync(ct);
            return NoContent();
        }

        [Authorize(Policy = AuthPolicies.CanEditVehicles)]
        [HttpDelete("api/vehicles/{vehicleId:int}/images/{imageId:int}")]
        public async Task<IActionResult> DeleteImage(int vehicleId, int imageId, CancellationToken ct)
        {
            var images = await _context.VehicleImages
                .Where(i => i.VehicleId == vehicleId)
                .OrderBy(i => i.SortOrder)
                .ToListAsync(ct);

            var target = images.FirstOrDefault(i => i.VehicleImageId == imageId);
            if (target == null) return NotFound("Image not found.");

            var blobName = target.ImageUrl;
            var wasPrimary = target.IsPrimary;

            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            _context.VehicleImages.Remove(target);
            await _context.SaveChangesAsync(ct); // frees the primary slot if this was the primary

            var remaining = images.Where(i => i.VehicleImageId != imageId).ToList();

            for (var position = 0; position < remaining.Count; position++)
                remaining[position].SortOrder = position;

            // If the primary was deleted, the next image in order becomes primary.
            if (wasPrimary && remaining.Count > 0)
                remaining[0].IsPrimary = true;

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // Remove the blobs (image + thumbnail) after the row is gone.
            try
            {
                await _storage.DeleteAsync(blobName, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Row deleted but blob {BlobName} could not be removed.", blobName);
            }

            return NoContent();
        }

        // Streams a blob through the API so the container can stay private (no SAS / public access needed).
        [Authorize]
        [HttpGet("api/vehicle-images/{blobName}")]
        public async Task<IActionResult> GetImage(string blobName, [FromQuery] bool thumb = false, CancellationToken ct = default)
        {
            if (!VehicleImageStorage.IsValidBlobName(blobName))
                return NotFound();

            var result = await _storage.OpenReadAsync(blobName, thumb, ct);
            if (result == null) return NotFound();

            Response.Headers.CacheControl = "private, max-age=86400";
            return File(result.Value.Content, result.Value.ContentType);
        }

        private async Task CleanUpAsync(IEnumerable<string> blobNames)
        {
            foreach (var name in blobNames)
            {
                try { await _storage.DeleteAsync(name, CancellationToken.None); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not clean up blob {BlobName}.", name); }
            }
        }
    }
}
