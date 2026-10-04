using APCVehicleTracker.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace APCVehicleTracker.Services
{
    public sealed record ApiResult(bool Success, string? Error = null, int? Id = null);

    // Second half of VehicleApiService (the original file is now "partial").
    public partial class VehicleApiService
    {
        public static readonly string[] StatusOptions =
            { "Available", "Reserved", "In Transit", "In Workshop", "Sold" };

        public async Task<VehicleFormViewModel?> GetVehicleForEditAsync(int vehicleId)
        {
            var response = await _httpClient.GetAsync($"api/vehicles/{vehicleId}");
            if (!response.IsSuccessStatusCode) return null;

            var r = await response.Content.ReadFromJsonAsync<VehicleEditApiResponse>();
            if (r == null) return null;

            return new VehicleFormViewModel
            {
                VehicleId = r.VehicleId,
                Make = r.Make,
                Model = r.Model,
                Year = r.Year,
                Registration = r.Registration,
                Vin = r.Vin,
                Status = r.Status,
                CurrentLocation = r.CurrentLocation,
                IsActive = r.IsActive,
                Images = r.Images.Select(i => new VehicleImageViewModel
                {
                    ImageId = i.ImageId,
                    BlobName = i.BlobName,
                    SortOrder = i.SortOrder,
                    IsPrimary = i.IsPrimary
                }).OrderBy(i => i.SortOrder).ToList()
            };
        }

        public async Task<ApiResult> CreateVehicleAsync(VehicleFormViewModel vm)
        {
            var response = await _httpClient.PostAsJsonAsync("api/vehicles", new
            {
                vm.Make,
                vm.Model,
                vm.Year,
                vm.Registration,
                vm.Vin,
                vm.Status,
                vm.StartingLocationId,
                vm.Notes
            });

            if (!response.IsSuccessStatusCode)
                return new ApiResult(false, await ReadErrorAsync(response));

            var created = await response.Content.ReadFromJsonAsync<CreatedVehicleApiResponse>();
            return new ApiResult(true, Id: created?.VehicleId);
        }

        public async Task<ApiResult> UpdateVehicleAsync(int id, VehicleFormViewModel vm)
        {
            var response = await _httpClient.PutAsJsonAsync($"api/vehicles/{id}", new
            {
                vm.Make,
                vm.Model,
                vm.Year,
                vm.Registration,
                vm.Vin,
                vm.Status
            });

            return await ToResultAsync(response);
        }

        public async Task<ApiResult> DeactivateVehicleAsync(int id) =>
            await ToResultAsync(await _httpClient.DeleteAsync($"api/vehicles/{id}"));

        public async Task<ApiResult> ReactivateVehicleAsync(int id) =>
            await ToResultAsync(await _httpClient.PostAsync($"api/vehicles/{id}/reactivate", null));

        // Admin only (the API ignores inactiveOnly for other roles).
        public async Task<List<Vehicle>> GetInactiveVehiclesAsync()
        {
            var vehicles = new List<Vehicle>();
            var page = 1;

            while (true)
            {
                var response = await _httpClient.GetFromJsonAsync<VehicleListApiResponse>(
                    $"api/vehicles?inactiveOnly=true&includeSold=true&page={page}");

                if (response == null || response.Vehicles.Count == 0) break;

                vehicles.AddRange(response.Vehicles.Select(MapToVehicle));

                if (vehicles.Count >= response.TotalCount) break;
                page++;
            }

            return vehicles;
        }

        // ---------- images ----------

        public async Task<ApiResult> UploadImagesAsync(int vehicleId, IEnumerable<IFormFile> files)
        {
            using var content = new MultipartFormDataContent();

            foreach (var file in files)
            {
                var stream = new StreamContent(file.OpenReadStream());
                stream.Headers.ContentType = new MediaTypeHeaderValue(
                    string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
                content.Add(stream, "files", Path.GetFileName(file.FileName)); // API ignores the name anyway
            }

            return await ToResultAsync(
                await _httpClient.PostAsync($"api/vehicles/{vehicleId}/images", content));
        }

        public async Task<ApiResult> SetPrimaryImageAsync(int vehicleId, int imageId) =>
            await ToResultAsync(await _httpClient.PutAsync(
                $"api/vehicles/{vehicleId}/images/{imageId}/primary", null));

        public async Task<ApiResult> ReorderImagesAsync(int vehicleId, IEnumerable<int> orderedImageIds) =>
            await ToResultAsync(await _httpClient.PutAsJsonAsync(
                $"api/vehicles/{vehicleId}/images/order", new { ImageIds = orderedImageIds }));

        public async Task<ApiResult> DeleteImageAsync(int vehicleId, int imageId) =>
            await ToResultAsync(await _httpClient.DeleteAsync(
                $"api/vehicles/{vehicleId}/images/{imageId}"));

        // Caller owns the response (register it for dispose) so the image can be streamed.
        public Task<HttpResponseMessage> GetImageAsync(string blobName, bool thumbnail) =>
            _httpClient.GetAsync(
                $"api/vehicle-images/{Uri.EscapeDataString(blobName)}?thumb={thumbnail.ToString().ToLowerInvariant()}",
                HttpCompletionOption.ResponseHeadersRead);

        // ---------- helpers ----------

        private static async Task<ApiResult> ToResultAsync(HttpResponseMessage response) =>
            response.IsSuccessStatusCode
                ? new ApiResult(true)
                : new ApiResult(false, await ReadErrorAsync(response));

        private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(body))
                return $"The request failed ({(int)response.StatusCode}).";

            try
            {
                using var doc = JsonDocument.Parse(body);

                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("errors", out var errors) &&
                    errors.ValueKind == JsonValueKind.Object)
                {
                    var messages = errors.EnumerateObject()
                        .SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString()))
                        .Where(m => !string.IsNullOrWhiteSpace(m));

                    return string.Join(" ", messages);
                }

                if (doc.RootElement.ValueKind == JsonValueKind.String)
                    return doc.RootElement.GetString() ?? body;
            }
            catch (JsonException)
            {
                // Plain-text error body - fall through.
            }

            return body;
        }

        private sealed class CreatedVehicleApiResponse
        {
            public int VehicleId { get; set; }
        }

        private sealed class VehicleEditApiResponse
        {
            public int VehicleId { get; set; }
            public string Make { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public int Year { get; set; }
            public string Registration { get; set; } = string.Empty;
            public string? Vin { get; set; }
            public string Status { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public string? CurrentLocation { get; set; }
            public List<ImageApiResponse> Images { get; set; } = new();
        }

        private sealed class ImageApiResponse
        {
            public int ImageId { get; set; }
            public string BlobName { get; set; } = string.Empty;
            public int SortOrder { get; set; }
            public bool IsPrimary { get; set; }
        }
    }
}
