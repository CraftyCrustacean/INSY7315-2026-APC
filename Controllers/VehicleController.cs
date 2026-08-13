using Microsoft.AspNetCore.Mvc;
using APCVehicleTracker.Services;

namespace APCVehicleTracker.Controllers
{
    public class VehicleController : Controller
    {
        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("User") == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var vehicles = DummyDataService.GetVehicles();
            return View(vehicles);
        }

   

        public IActionResult Details(int id)
        {
            var vehicle = DummyDataService.GetVehicles().FirstOrDefault(v => v.Id == id);
            if (vehicle == null) return NotFound();

            ViewBag.MovementHistory = DummyDataService.GetMovementHistory(id);
            return View(vehicle);
        }

        public IActionResult LogMovement(int id)
        {
            var vehicle = DummyDataService.GetVehicles().FirstOrDefault(v => v.Id == id);
            if (vehicle == null) return NotFound();
            return View(vehicle);
        }

        public IActionResult Search(string query)
        {
            var vehicles = DummyDataService.GetVehicles();
            if (!string.IsNullOrEmpty(query))
            {
                vehicles = vehicles.Where(v =>
                    v.RegistrationNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    v.Make.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    v.Model.Contains(query, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }
            ViewBag.Query = query;
            return View(vehicles);
        }

        public IActionResult Reports()
        {
            var vehicles = DummyDataService.GetVehicles();
            return View(vehicles);
        }
    }
}