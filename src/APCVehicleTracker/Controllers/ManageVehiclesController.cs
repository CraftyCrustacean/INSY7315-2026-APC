using APCVehicleTracker.Data.Auth;
using APCVehicleTracker.Models;
using APCVehicleTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APCVehicleTracker.Controllers
{
    // Admin + Stock Controller only (CanEditVehicles). Reactivation is Admin only.
    [Authorize(Policy = AuthPolicies.CanEditVehicles)]
    public class ManageVehiclesController : Controller
    {
        private readonly VehicleApiService _api;

        public ManageVehiclesController(VehicleApiService api)
        {
            _api = api;
        }

        // ---------- Create ----------

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new VehicleFormViewModel();
            await LoadListsAsync(vm, forCreate: true);
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VehicleFormViewModel vm)
        {
            if (vm.StartingLocationId <= 0)
                ModelState.AddModelError(nameof(vm.StartingLocationId), "Please select a starting location.");

            if (ModelState.IsValid)
            {
                var result = await _api.CreateVehicleAsync(vm);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = "Vehicle added. You can now upload images.";
                    return RedirectToAction(nameof(Edit), new { id = result.Id });
                }

                ModelState.AddModelError(string.Empty, result.Error ?? "The vehicle could not be added.");
            }

            await LoadListsAsync(vm, forCreate: true);
            return View(vm);
        }

        // ---------- Edit ----------

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _api.GetVehicleForEditAsync(id);
            if (vm == null) return NotFound();

            vm.AvailableStatuses = VehicleApiService.StatusOptions.ToList();
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, VehicleFormViewModel vm)
        {
            vm.VehicleId = id;

            if (ModelState.IsValid)
            {
                var result = await _api.UpdateVehicleAsync(id, vm);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = "Vehicle updated.";
                    return RedirectToAction(nameof(Edit), new { id });
                }

                ModelState.AddModelError(string.Empty, result.Error ?? "The vehicle could not be updated.");
            }

            // Re-populate the read-only parts of the page.
            var fresh = await _api.GetVehicleForEditAsync(id);
            if (fresh == null) return NotFound();

            vm.CurrentLocation = fresh.CurrentLocation;
            vm.IsActive = fresh.IsActive;
            vm.Images = fresh.Images;
            vm.AvailableStatuses = VehicleApiService.StatusOptions.ToList();
            return View(vm);
        }

        // ---------- Delete (soft) / Reactivate ----------

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            var result = await _api.DeactivateVehicleAsync(id);

            if (result.Success)
                TempData["SuccessMessage"] = "Vehicle deleted. An admin can reactivate it if needed.";
            else
                TempData["ErrorMessage"] = result.Error;

            return RedirectToAction("Search", "Vehicle");
        }

        [Authorize(Policy = AuthPolicies.CanReactivateVehicles)]
        [HttpGet]
        public async Task<IActionResult> Inactive()
        {
            return View(await _api.GetInactiveVehiclesAsync());
        }

        [Authorize(Policy = AuthPolicies.CanReactivateVehicles)]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(int id)
        {
            var result = await _api.ReactivateVehicleAsync(id);

            if (result.Success)
                TempData["SuccessMessage"] = "Vehicle reactivated.";
            else
                TempData["ErrorMessage"] = result.Error;

            return RedirectToAction(nameof(Inactive));
        }

        // ---------- Images ----------

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadImages(int id, List<IFormFile> files)
        {
            if (files == null || files.Count == 0 || files.All(f => f.Length == 0))
            {
                TempData["ErrorMessage"] = "Please choose at least one image.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            var result = await _api.UploadImagesAsync(id, files);
            Flash(result, "Images uploaded.");
            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPrimary(int id, int imageId)
        {
            Flash(await _api.SetPrimaryImageAsync(id, imageId), "Primary image updated.");
            return RedirectToAction(nameof(Edit), new { id });
        }

        // direction: "up" or "down"
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveImage(int id, int imageId, string direction)
        {
            var vehicle = await _api.GetVehicleForEditAsync(id);
            if (vehicle == null) return NotFound();

            var ids = vehicle.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageId).ToList();
            var index = ids.IndexOf(imageId);
            var target = direction == "up" ? index - 1 : index + 1;

            if (index >= 0 && target >= 0 && target < ids.Count)
            {
                (ids[index], ids[target]) = (ids[target], ids[index]);
                Flash(await _api.ReorderImagesAsync(id, ids), "Image order updated.");
            }

            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int id, int imageId)
        {
            Flash(await _api.DeleteImageAsync(id, imageId), "Image deleted.");
            return RedirectToAction(nameof(Edit), new { id });
        }

        // ---------- helpers ----------

        private async Task LoadListsAsync(VehicleFormViewModel vm, bool forCreate)
        {
            vm.AvailableStatuses = VehicleApiService.StatusOptions
                .Where(s => !forCreate || s != "Sold")
                .ToList();

            vm.AvailableLocations = await _api.GetLocationsAsync();
        }

        private void Flash(ApiResult result, string successMessage)
        {
            if (result.Success)
                TempData["SuccessMessage"] = successMessage;
            else
                TempData["ErrorMessage"] = result.Error;
        }
    }
}
