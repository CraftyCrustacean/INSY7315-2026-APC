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

            return await _httpClient.GetFromJsonAsync<List<string>>(url)
                   ?? new List<string>();
        }

        public async Task<List<LocationViewModel>> GetLocationsAsync()
        {
            var locations = await _httpClient.GetFromJsonAsync<List<LocationViewModel>>(
                "api/locations");

            return locations ?? new List<LocationViewModel>();
        }
    }
}