using APCVehicleTracker.Models;
using APCVehicleTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace APCVehicleTracker.Controllers
{
    public class VehicleController : Controller
    {
        private readonly VehicleApiService _vehicleApiService;

        public VehicleController(VehicleApiService vehicleApiService)
        {
            _vehicleApiService = vehicleApiService;
        }

        public async Task<IActionResult> Search(
            string? make = null,string? model = null, int? yearFrom = null, int? yearTo = null,
            List<string>? status = null,List<int>? location = null,bool includeSold = false,
            int page = 1)
        {
            var result = await _vehicleApiService.SearchVehiclesAsync(
                make,model,yearFrom,yearTo,status,location,includeSold,page);

            if (result == null)
            {
                result = new VehicleSearchViewModel();
            }

            result.Make = make;
            result.Model = model;
            result.YearFrom = yearFrom;
            result.YearTo = yearTo;
            result.Status = status ?? new List<string>();
            result.Location = location ?? new List<int>();
            result.IncludeSold = includeSold;
            result.Page = page;

            result.AvailableMakes = await _vehicleApiService.GetMakesAsync();
            result.AvailableModels = await _vehicleApiService.GetModelsAsync(make);
            result.AvailableLocations = await _vehicleApiService.GetLocationsAsync();

            result.AvailableStatuses = new List<string>
            {
                "Available", "Reserved", "In Transit", "In Workshop"
            };

            if (includeSold)
            {
                result.AvailableStatuses.Add("Sold");
            }

            return View(result);
        }
    }
}