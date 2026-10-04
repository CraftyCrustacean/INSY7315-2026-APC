using APCVehicleTracker.API.DTOs;
using APCVehicleTracker.Data;
using APCVehicleTracker.Data.Auth;
using APCVehicleTracker.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace APCVehicleTracker.API.Controllers
{
    // Shares the api/vehicles prefix with VehiclesController (which keeps the GET/search/movement endpoints).
    [ApiController]
    [Route("api/vehicles")]
    public class VehicleManagementController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public VehicleManagementController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize(Policy = AuthPolicies.CanEditVehicles)]
        [HttpPost]
        public async Task<IActionResult> CreateVehicle([FromBody] CreateVehicleRequestDto request)
        {
            var error = ValidateCommon(request.Year, request.Status, allowSold: false);
            if (error != null) return BadRequest(error);

            var staffIdClaim = User.FindFirst("staff_id");
            if (staffIdClaim == null || !int.TryParse(staffIdClaim.Value, out var staffId))
                return Unauthorized("Authenticated staff member could not be identified.");

            var locationExists = await _context.Locations
                .AsNoTracking()
                .AnyAsync(l => l.LocationId == request.StartingLocationId);

            if (!locationExists)
                return BadRequest("The starting location does not exist.");

            var registration = request.Registration.Trim().ToUpperInvariant();
            var vin = NormaliseVin(request.Vin);

            var conflict = await FindConflictAsync(registration, vin, excludeVehicleId: 0);
            if (conflict != null) return Conflict(conflict);

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var vehicle = new Vehicle
                {
                    Make = request.Make.Trim(),
                    Model = request.Model.Trim(),
                    Year = request.Year,
                    Registration = registration,
                    Vin = vin,
                    Status = request.Status.Trim(),
                    IsActive = true
                };

                _context.Vehicles.Add(vehicle);
                await _context.SaveChangesAsync(); // generates VehicleId

                // The starting location is saved as the vehicle's first movement.
                _context.Movements.Add(new Movement
                {
                    VehicleId = vehicle.VehicleId,
                    FromLocationId = null,
                    ToLocationId = request.StartingLocationId,
                    StaffId = staffId,
                    MovementDateTime = DateTime.UtcNow,
                    Notes = string.IsNullOrWhiteSpace(request.Notes)
                        ? "Vehicle added - starting location."
                        : request.Notes.Trim()
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Created($"api/vehicles/{vehicle.VehicleId}", new { vehicleId = vehicle.VehicleId });
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                await transaction.RollbackAsync();
                return Conflict("A vehicle with this registration already exists.");
            }
            catch
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "The vehicle could not be saved.");
            }
        }

        [Authorize(Policy = AuthPolicies.CanEditVehicles)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateVehicle(int id, [FromBody] UpdateVehicleRequestDto request)
        {
            var error = ValidateCommon(request.Year, request.Status, allowSold: true);
            if (error != null) return BadRequest(error);

            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == id);
            if (vehicle == null) return NotFound("Vehicle not found.");

            if (!vehicle.IsActive)
                return Conflict("This vehicle is inactive. An admin must reactivate it before it can be edited.");

            var registration = request.Registration.Trim().ToUpperInvariant();
            var vin = NormaliseVin(request.Vin);

            var conflict = await FindConflictAsync(registration, vin, excludeVehicleId: id);
            if (conflict != null) return Conflict(conflict);

            vehicle.Make = request.Make.Trim();
            vehicle.Model = request.Model.Trim();
            vehicle.Year = request.Year;
            vehicle.Registration = registration;
            vehicle.Vin = vin;
            vehicle.Status = request.Status.Trim();

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                return Conflict("A vehicle with this registration already exists.");
            }

            return NoContent();
        }

        // "Delete" = soft delete. Images and movement history are kept.
        [Authorize(Policy = AuthPolicies.CanEditVehicles)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeactivateVehicle(int id)
        {
            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == id);
            if (vehicle == null) return NotFound("Vehicle not found.");

            if (vehicle.IsActive)
            {
                vehicle.IsActive = false;
                await _context.SaveChangesAsync();
            }

            return NoContent();
        }

        // Admin only.
        [Authorize(Policy = AuthPolicies.CanReactivateVehicles)]
        [HttpPost("{id:int}/reactivate")]
        public async Task<IActionResult> ReactivateVehicle(int id)
        {
            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == id);
            if (vehicle == null) return NotFound("Vehicle not found.");

            if (!vehicle.IsActive)
            {
                vehicle.IsActive = true;
                await _context.SaveChangesAsync();
            }

            return NoContent();
        }

        // ---------- helpers ----------

        private static string? ValidateCommon(int year, string status, bool allowSold)
        {
            if (year < 1900 || year > DateTime.UtcNow.Year + 1)
                return $"Year must be between 1900 and {DateTime.UtcNow.Year + 1}.";

            var trimmed = status?.Trim() ?? string.Empty;

            if (!VehicleRules.Statuses.Contains(trimmed))
                return "Invalid vehicle status.";

            if (!allowSold && trimmed == "Sold")
                return "A new vehicle cannot be added with the status Sold.";

            return null;
        }

        private static string? NormaliseVin(string? vin) =>
            string.IsNullOrWhiteSpace(vin) ? null : vin.Trim().ToUpperInvariant();

        private async Task<string?> FindConflictAsync(string registration, string? vin, int excludeVehicleId)
        {
            if (await _context.Vehicles.AnyAsync(v =>
                    v.VehicleId != excludeVehicleId && v.Registration.ToUpper() == registration))
                return "A vehicle with this registration already exists.";

            if (vin != null && await _context.Vehicles.AnyAsync(v =>
                    v.VehicleId != excludeVehicleId && v.Vin != null && v.Vin.ToUpper() == vin))
                return "A vehicle with this VIN already exists.";

            return null;
        }

        private static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }
}
