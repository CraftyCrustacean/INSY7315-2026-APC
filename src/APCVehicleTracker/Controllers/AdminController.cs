using APCVehicleTracker.Data.Auth;
using APCVehicleTracker.Data.Contracts;
using APCVehicleTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APCVehicleTracker.Controllers;

[Authorize(Policy = AuthPolicies.CanManageUsers)]
public class AdminController : Controller
{
    private readonly StaffApiService _staff;
    public AdminController(StaffApiService staff)
    { 
        _staff = staff; 
    }

    public async Task<IActionResult> Index() 
    { 
        return View(await _staff.ListAsync());
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateStaffRequest());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateStaffRequest model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _staff.CreateAsync(model);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }
        return ShowTemporaryPassword(result.Value!);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var staff = await _staff.GetAsync(id);
        if (staff is null) return NotFound();

        ViewData["Email"] = staff.Email;
        ViewData["IsActive"] = staff.IsActive;
        ViewData["IsSelf"] = staff.StaffId.ToString() == User.FindFirst(StaffSigninService.StaffIdClaim)?.Value;
        return View(new UpdateStaffRequest
        {
            FirstName = staff.FirstName,
            LastName = staff.LastName,
            Phone = staff.Phone,
            Role = staff.Role
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, UpdateStaffRequest model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _staff.UpdateAsync(id, model);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }
        TempData["Message"] = $"{result.Value!.FirstName} {result.Value.LastName} was updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var result = await _staff.ResetPasswordAsync(id);
        if (!result.Ok)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index));
        }
        return ShowTemporaryPassword(result.Value!);
    }

    [HttpPost]
    public async Task<IActionResult> SetActive(int id, bool active)
    {
        var result = await _staff.SetActiveAsync(id, active);
        if (!result.Ok)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index));
        }
        TempData["Message"] = $"{result.Value!.FirstName} {result.Value.LastName} was {(active ? "reactivated" : "deactivated")}.";
        return RedirectToAction(nameof(Index));
    }

    private IActionResult ShowTemporaryPassword(TemporaryPasswordResponse response)
    {
        Response.Headers.CacheControl = "no-store";
        return View("TemporaryPassword", response);
    }
}
