
using APCVehicleTracker.Models;
using System.Net.Http.Json;

namespace APCVehicleTracker.Services
{
    public class VehicleApiService
    {
        private readonly HttpClient _httpClient;

        public VehicleApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<VehicleSearchViewModel?> SearchVehiclesAsync(
            string? make = null,
            string? model = null,
            int? yearFrom = null,
            int? yearTo = null,
            List<string>? status = null,
            List<int>? location = null,
            bool includeSold = false,
            int page = 1)
        {
            var queryParams = new List<string>();

            if (!string.IsNullOrWhiteSpace(make))
                queryParams.Add($"make={Uri.EscapeDataString(make)}");

            if (!string.IsNullOrWhiteSpace(model))
                queryParams.Add($"model={Uri.EscapeDataString(model)}");

            if (yearFrom.HasValue)
                queryParams.Add($"yearFrom={yearFrom.Value}");

            if (yearTo.HasValue)
                queryParams.Add($"yearTo={yearTo.Value}");

            if (status != null)
            {
                foreach (var item in status)
                {
                    if (!string.IsNullOrWhiteSpace(item))
                        queryParams.Add($"status={Uri.EscapeDataString(item)}");
                }
            }

            if (location != null)
            {
                foreach (var item in location)
                {
                    if (item > 0)
                        queryParams.Add($"location={item}");
                }
            }

            if (includeSold)
                queryParams.Add("includeSold=true");

            queryParams.Add($"page={page}");

            var url = "api/vehicles";

            if (queryParams.Count > 0)
                url += "?" + string.Join("&", queryParams);

            return await _httpClient.GetFromJsonAsync<VehicleSearchViewModel>(url);
        }

        public async Task<List<string>> GetMakesAsync()
        {
            return await _httpClient.GetFromJsonAsync<List<string>>(
                "api/vehicles/makes") ?? new List<string>();
        }

        public async Task<List<string>> GetModelsAsync(string? make = null)
        {
            var url = "api/vehicles/models";

            if (!string.IsNullOrWhiteSpace(make))
            {
                url += $"?make={Uri.EscapeDataString(make)}";
            }

            return await _httpClient.GetFromJsonAsync<List<string>>(
                url) ?? new List<string>();
        }

        public async Task<List<LocationViewModel>> GetLocationsAsync()
        {
            var locations = await _httpClient.GetFromJsonAsync<List<LocationViewModel>>(
                "api/locations");

            return locations ?? new List<LocationViewModel>();
        }

        public async Task<Vehicle?> GetVehicleDetailsAsync(int vehicleId)
        {
            var response = await _httpClient.GetAsync(
                $"api/vehicles/{vehicleId}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var result = await response.Content
                .ReadFromJsonAsync<VehicleDetailsApiResponse>();

            if (result == null)
            {
                return null;
            }

            return new Vehicle
            {
                Id = result.VehicleId,
                RegistrationNumber = result.Registration,
                Make = result.Make,
                Model = result.Model,
                Year = result.Year,
                VinNumber = result.Vin ?? string.Empty,
                CurrentLocation = result.CurrentLocation ?? string.Empty,
                Status = result.Status,
                LastMoved = DateTime.UtcNow
            };
        }

        public async Task<HttpResponseMessage> LogMovementAsync(
            int vehicleId,
            int toLocationId,
            string? newStatus,
            string? notes)
        {
            var request = new
            {
                ToLocationId = toLocationId,
                NewStatus = newStatus,
                Notes = notes
            };

            return await _httpClient.PostAsJsonAsync(
                $"api/vehicles/{vehicleId}/movements",
                request);
        }

        // ---------- NEW: used by the Dashboard (Index) and Reports pages ----------

        // The API returns 25 vehicles per page, so keep requesting pages until
        // every vehicle has been loaded.
        public async Task<List<Vehicle>> GetAllVehiclesAsync(bool includeSold = false)
        {
            var vehicles = new List<Vehicle>();
            var page = 1;

            while (true)
            {
                var url = $"api/vehicles?page={page}";

                if (includeSold)
                    url += "&includeSold=true";

                var response = await _httpClient
                    .GetFromJsonAsync<VehicleListApiResponse>(url);

                if (response == null || response.Vehicles.Count == 0)
                    break;

                vehicles.AddRange(response.Vehicles.Select(MapToVehicle));

                if (vehicles.Count >= response.TotalCount)
                    break;

                page++;
            }

            return vehicles;
        }

        // ---------- NEW: used by the Details page ----------

        public async Task<List<MovementRecord>> GetMovementHistoryAsync(int vehicleId)
        {
            var response = await _httpClient.GetAsync(
                $"api/vehicles/{vehicleId}/movements");

            if (!response.IsSuccessStatusCode)
            {
                return new List<MovementRecord>();
            }

            var movements = await response.Content
                .ReadFromJsonAsync<List<MovementHistoryApiResponse>>()
                ?? new List<MovementHistoryApiResponse>();

            return movements.Select(m => new MovementRecord
            {
                Id = m.MovementId,
                VehicleId = vehicleId,
                FromLocation = m.FromLocation ?? string.Empty,
                ToLocation = m.ToLocation,
                MovedBy = m.MovedBy ?? string.Empty,
                MovementDate = m.MovementDateTime,
                Notes = m.Notes ?? string.Empty
            }).ToList();
        }

        private static Vehicle MapToVehicle(VehicleListItemApiResponse item)
        {
            return new Vehicle
            {
                Id = item.VehicleId,
                RegistrationNumber = item.Registration,
                Make = item.Make,
                Model = item.Model,
                Year = item.Year,
                CurrentLocation = item.CurrentLocation ?? string.Empty,
                Status = item.Status,
                // The API only gives "days at current location", so work back
                // to a date. A vehicle with no movements keeps DateTime.MinValue.
                LastMoved = item.DaysAtCurrentLocation.HasValue
                    ? DateTime.UtcNow.Date.AddDays(-item.DaysAtCurrentLocation.Value)
                    : DateTime.MinValue
            };
        }

        private sealed class VehicleDetailsApiResponse
        {
            public int VehicleId { get; set; }

            public string? PrimaryImage { get; set; }

            public string Make { get; set; } = string.Empty;

            public string Model { get; set; } = string.Empty;

            public int Year { get; set; }

            public string Registration { get; set; } = string.Empty;

            public string? Vin { get; set; }

            public string Status { get; set; } = string.Empty;

            public string? CurrentLocation { get; set; }

            public int? DaysAtCurrentLocation { get; set; }
        }

        private sealed class VehicleListApiResponse
        {
            public int TotalCount { get; set; }

            public List<VehicleListItemApiResponse> Vehicles { get; set; } = new();
        }

        private sealed class VehicleListItemApiResponse
        {
            public int VehicleId { get; set; }

            public string Make { get; set; } = string.Empty;

            public string Model { get; set; } = string.Empty;

            public int Year { get; set; }

            public string Registration { get; set; } = string.Empty;

            public string Status { get; set; } = string.Empty;

            public string? CurrentLocation { get; set; }

            public int? DaysAtCurrentLocation { get; set; }
        }

        private sealed class MovementHistoryApiResponse
        {
            public int MovementId { get; set; }

            public DateTime MovementDateTime { get; set; }

            public string? FromLocation { get; set; }

            public string ToLocation { get; set; } = string.Empty;

            public string? MovedBy { get; set; }

            public string? Notes { get; set; }
        }
    }
}