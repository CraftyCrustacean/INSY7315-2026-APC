using APCVehicleTracker.Data.Auth;
using APCVehicleTracker.Models;
using APCVehicleTracker.Services;
using Microsoft.AspNetCore.Authorization;
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

        // NEW: Dashboard
        public async Task<IActionResult> Index()
        {
            var vehicles = await _vehicleApiService.GetAllVehiclesAsync();

            return View(vehicles);
        }

        // NEW: Vehicle details + movement history
        public async Task<IActionResult> Details(int id)
        {
            var vehicle = await _vehicleApiService.GetVehicleDetailsAsync(id);

            if (vehicle == null)
            {
                return NotFound();
            }

            ViewBag.MovementHistory =
                await _vehicleApiService.GetMovementHistoryAsync(id);

            return View(vehicle);
        }

        // NEW: Stock reports
        public async Task<IActionResult> Reports()
        {
            var vehicles = await _vehicleApiService.GetAllVehiclesAsync();

            return View(vehicles);
        }

        public async Task<IActionResult> Search(
            string? make = null,
            string? model = null,
            int? yearFrom = null,
            int? yearTo = null,
            List<string>? status = null,
            List<int>? location = null,
            bool includeSold = false,
            int page = 1)
        {
            var result = await _vehicleApiService.SearchVehiclesAsync(
                make,
                model,
                yearFrom,
                yearTo,
                status,
                location,
                includeSold,
                page);

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

            result.AvailableMakes =
                await _vehicleApiService.GetMakesAsync();

            result.AvailableModels =
                await _vehicleApiService.GetModelsAsync(make);

            result.AvailableLocations =
                await _vehicleApiService.GetLocationsAsync();

            result.AvailableStatuses = new List<string>
            {
                "Available",
                "Reserved",
                "In Transit",
                "In Workshop"
            };

            if (includeSold)
            {
                result.AvailableStatuses.Add("Sold");
            }

            return View(result);
        }

        [Authorize(Policy = AuthPolicies.CanLogMovements)]
        [HttpGet]
        public async Task<IActionResult> LogMovement(int id)
        {
            var vehicle =
                await _vehicleApiService.GetVehicleDetailsAsync(id);

            if (vehicle == null)
            {
                return NotFound();
            }

            if (vehicle.Status == "Sold")
            {
                return BadRequest(
                    "Sold vehicles cannot have movements logged.");
            }

            var locations =
                await _vehicleApiService.GetLocationsAsync();

            var destinationLocations = locations
                .Where(location =>
                    !string.Equals(
                        location.LocationName,
                        vehicle.CurrentLocation,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            ViewBag.DestinationLocations =
                destinationLocations;

            ViewBag.AvailableStatuses = new List<string>
            {
                "Available",
                "Reserved",
                "In Transit",
                "In Workshop"
            };

            return View(vehicle);
        }

        [Authorize(Policy = AuthPolicies.CanLogMovements)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogMovement(
            int id,
            int toLocationId,
            string? newStatus,
            string? notes)
        {
            if (toLocationId <= 0)
            {
                ModelState.AddModelError(
                    "toLocationId",
                    "Please select a destination location.");
            }

            if (!string.IsNullOrWhiteSpace(notes) &&
                notes.Length > 500)
            {
                ModelState.AddModelError(
                    "notes",
                    "Notes cannot exceed 500 characters.");
            }

            if (!ModelState.IsValid)
            {
                var invalidVehicle =
                    await _vehicleApiService.GetVehicleDetailsAsync(id);

                if (invalidVehicle == null)
                {
                    return NotFound();
                }

                await PrepareMovementFormAsync(invalidVehicle);

                return View(invalidVehicle);
            }

            var response =
                await _vehicleApiService.LogMovementAsync(
                    id,
                    toLocationId,
                    newStatus,
                    notes);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] =
                    "Vehicle movement logged successfully.";

                // Back to the vehicle that was just moved.
                return RedirectToAction(nameof(Details), new { id });
            }

            var errorMessage =
                await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                errorMessage =
                    "The vehicle movement could not be logged.";
            }

            ModelState.AddModelError(
                string.Empty,
                errorMessage);

            var vehicle =
                await _vehicleApiService.GetVehicleDetailsAsync(id);

            if (vehicle == null)
            {
                return NotFound();
            }

            await PrepareMovementFormAsync(vehicle);

            return View(vehicle);
        }

        private async Task PrepareMovementFormAsync(
            Vehicle vehicle)
        {
            var locations =
                await _vehicleApiService.GetLocationsAsync();

            ViewBag.DestinationLocations = locations
                .Where(location =>
                    !string.Equals(
                        location.LocationName,
                        vehicle.CurrentLocation,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            ViewBag.AvailableStatuses = new List<string>
            {
                "Available",
                "Reserved",
                "In Transit",
                "In Workshop"
            };
        }
    }
}