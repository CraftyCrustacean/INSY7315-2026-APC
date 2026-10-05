using APCVehicleTracker.API.Filters;
using APCVehicleTracker.API.Services;
using APCVehicleTracker.Data.Auth;
using APCVehicleTracker.Data.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APCVehicleTracker.API.Controllers;

[ApiController]
[Route("api/staff")]
[Authorize(Policy = AuthPolicies.CanManageUsers)]
[StaffAdminErrors]
public class StaffController : ControllerBase
{
    private readonly AdminService _service;
    public StaffController(AdminService service)
    {
        _service = service;
    }

    private int ActingStaffId => int.Parse(User.FindFirst(StaffSigninService.StaffIdClaim)!.Value);

    [HttpGet]
    public Task<List<StaffDto>> List(CancellationToken cancelationToken)
    {
        return _service.ListAsync(cancelationToken);
    }

    [HttpGet("{id:int}")]
    public Task<StaffDto> Get(int id, CancellationToken cancelationToken)
    {
        return _service.GetAsync(id, cancelationToken);
    }

    [HttpPost]
    public async Task<ActionResult<TemporaryPasswordResponse>> Create(CreateStaffRequest request, CancellationToken cancelationToken)
    {
        var result = await _service.CreateAsync(request, ActingStaffId, cancelationToken);
        Response.Headers.CacheControl = "no-store";
        return CreatedAtAction(nameof(Get), new { id = result.Staff.StaffId }, result);
    }

    [HttpPut("{id:int}")]
    public Task<StaffDto> Update(int id, UpdateStaffRequest request, CancellationToken cancelationToken)
    {
        return _service.UpdateAsync(id, request, ActingStaffId, cancelationToken);
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<TemporaryPasswordResponse> ResetPassword(int id, CancellationToken cancelationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return await _service.ResetPasswordAsync(id, ActingStaffId, cancelationToken);
    }

    [HttpPost("{id:int}/deactivate")]
    public Task<StaffDto> Deactivate(int id, CancellationToken cancelationToken)
    {
        return _service.SetActiveAsync(id, false, ActingStaffId, cancelationToken);
    }

    [HttpPost("{id:int}/activate")]
    public Task<StaffDto> Activate(int id, CancellationToken cancelationToken)
    {
        return _service.SetActiveAsync(id, true, ActingStaffId, cancelationToken);
    }

}
