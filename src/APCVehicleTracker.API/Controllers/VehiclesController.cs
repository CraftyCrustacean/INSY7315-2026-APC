using APCVehicleTracker.API.DTOs;
using APCVehicleTracker.Data;
using APCVehicleTracker.Data.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APCVehicleTracker.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VehiclesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public VehiclesController(ApplicationDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        private async Task<bool> IsAdminAsync() => (await _authorizationService.AuthorizeAsync(User, AuthPolicies.CanReactivateVehicles)).Succeeded;

        [HttpGet]
        public async Task<IActionResult> SearchVehicles(
            string? make = null,
            string? model = null,
            int? yearFrom = null,
            int? yearTo = null,
            [FromQuery] string[]? status = null,
            [FromQuery] int[]? location = null,
            bool includeSold = false,
            bool inactiveOnly = false,
            int page = 1,
            int pageSize = 25)
        {
            if (page < 1)
                page = 1;

            pageSize = 25;

            var query = _context.Vehicles
                .AsNoTracking()
                .AsQueryable();

            var showInactive = inactiveOnly && await IsAdminAsync();
            query = query.Where(v => v.IsActive != showInactive);

            if (!includeSold)
                query = query.Where(v => v.Status != "Sold");

            if (!string.IsNullOrWhiteSpace(make))
                query = query.Where(v => v.Make == make);

            if (!string.IsNullOrWhiteSpace(model))
                query = query.Where(v => v.Model == model);

            if (yearFrom.HasValue)
                query = query.Where(v => v.Year >= yearFrom.Value);

            if (yearTo.HasValue)
                query = query.Where(v => v.Year <= yearTo.Value);

            if (status != null && status.Length > 0)
                query = query.Where(v => status.Contains(v.Status));

            var totalCount = await query.CountAsync();

            var vehicles = await query
                .OrderBy(v => v.VehicleId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vehicleIds = vehicles
                .Select(v => v.VehicleId)
                .ToList();

            var movements = await _context.Movements
                .AsNoTracking()
                .Where(m => vehicleIds.Contains(m.VehicleId))
                .OrderByDescending(m => m.MovementDateTime)
                .ToListAsync();

            var locations = await _context.Locations
                .AsNoTracking()
                .ToListAsync();

            var images = await _context.VehicleImages
                .AsNoTracking()
                .Where(i => vehicleIds.Contains(i.VehicleId))
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.SortOrder)
                .ToListAsync();

            var results = vehicles.Select(vehicle =>
            {
                var latestMovement = movements
                    .Where(m => m.VehicleId == vehicle.VehicleId)
                    .OrderByDescending(m => m.MovementDateTime)
                    .FirstOrDefault();

                var currentLocation = latestMovement == null
                    ? null
                    : locations
                        .FirstOrDefault(l => l.LocationId == latestMovement.ToLocationId)
                        ?.LocationName;

                var primaryImage = images
                    .FirstOrDefault(i => i.VehicleId == vehicle.VehicleId)
                    ?.ImageUrl;

                int? daysAtLocation = latestMovement == null
                    ? null
                    : (int)(DateTime.UtcNow - latestMovement.MovementDateTime).TotalDays;

                return new VehicleSearchResultDto
                {
                    VehicleId = vehicle.VehicleId,
                    PrimaryImage = primaryImage,
                    Make = vehicle.Make,
                    Model = vehicle.Model,
                    Year = vehicle.Year,
                    Registration = vehicle.Registration,
                    Status = vehicle.Status,
                    CurrentLocation = currentLocation,
                    DaysAtCurrentLocation = daysAtLocation
                };
            }).ToList();

            if (location != null && location.Length > 0)
            {
                results = results
                    .Where(v =>
                        v.CurrentLocation != null &&
                        location.Any(locationId =>
                            locations.Any(l =>
                                l.LocationId == locationId &&
                                l.LocationName == v.CurrentLocation)))
                    .ToList();
            }

            return Ok(new VehicleSearchResponseDto
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                Vehicles = results
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetVehicleDetails(int id)
        {
            var isAdmin = await IsAdminAsync();

            var vehicle = await _context.Vehicles
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.VehicleId == id && (v.IsActive || isAdmin));

            if (vehicle == null)
            {
                return NotFound();
            }

            var latestMovement = await _context.Movements
                .AsNoTracking()
                .Where(m => m.VehicleId == id)
                .OrderByDescending(m => m.MovementDateTime)
                .FirstOrDefaultAsync();

            string? currentLocation = null;
            int? daysAtLocation = null;

            if (latestMovement != null)
            {
                currentLocation = await _context.Locations
                    .AsNoTracking()
                    .Where(l => l.LocationId == latestMovement.ToLocationId)
                    .Select(l => l.LocationName)
                    .FirstOrDefaultAsync();

                daysAtLocation =
                    (int)(DateTime.UtcNow - latestMovement.MovementDateTime).TotalDays;
            }

            var images = await _context.VehicleImages
                .AsNoTracking()
                .Where(i => i.VehicleId == id)
                .OrderBy(i => i.SortOrder)
                .Select(i => new VehicleImageDto
                {
                    ImageId = i.VehicleImageId,
                    BlobName = i.ImageUrl,
                    SortOrder = i.SortOrder,
                    IsPrimary = i.IsPrimary
                })
                .ToListAsync();

            var result = new VehicleDetailsDto
            {
                VehicleId = vehicle.VehicleId,
                PrimaryImage = images.FirstOrDefault(i => i.IsPrimary)?.BlobName,
                IsActive = vehicle.IsActive,
                Images = images,
                Make = vehicle.Make,
                Model = vehicle.Model,
                Year = vehicle.Year,
                Registration = vehicle.Registration,
                Vin = vehicle.Vin,
                Status = vehicle.Status,
                CurrentLocation = currentLocation,
                DaysAtCurrentLocation = daysAtLocation
            };

            return Ok(result);
        }

        [HttpGet("{id}/movements")]
        public async Task<IActionResult> GetMovementHistory(int id)
        {
            var vehicleExists = await _context.Vehicles
                .AsNoTracking()
                .AnyAsync(v => v.VehicleId == id);

            if (!vehicleExists)
            {
                return NotFound();
            }

            var movements = await _context.Movements
                .AsNoTracking()
                .Where(m => m.VehicleId == id)
                .OrderByDescending(m => m.MovementDateTime)
                .ToListAsync();

            var locationIds = movements
                .SelectMany(m => new[] { m.FromLocationId, m.ToLocationId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var locations = await _context.Locations
                .AsNoTracking()
                .Where(l => locationIds.Contains(l.LocationId))
                .ToListAsync();

            var staffIds = movements
                .Where(m => m.StaffId.HasValue)
                .Select(m => m.StaffId!.Value)
                .Distinct()
                .ToList();

            var staff = await _context.Staff
                .AsNoTracking()
                .Where(s => staffIds.Contains(s.StaffId))
                .ToListAsync();

            var results = movements.Select(m =>
            {
                var fromLocation = m.FromLocationId.HasValue
                    ? locations
                        .FirstOrDefault(l => l.LocationId == m.FromLocationId.Value)
                        ?.LocationName
                    : null;

                var toLocation = locations
                    .FirstOrDefault(l => l.LocationId == m.ToLocationId)
                    ?.LocationName ?? "Unknown";

                var movedBy = m.StaffId.HasValue
                    ? staff.FirstOrDefault(s => s.StaffId == m.StaffId.Value)
                    : null;

                return new MovementHistoryDto
                {
                    MovementId = m.MovementId,
                    MovementDateTime = m.MovementDateTime,
                    FromLocation = fromLocation,
                    ToLocation = toLocation,
                    MovedBy = movedBy == null
                        ? null
                        : $"{movedBy.FirstName} {movedBy.LastName}",
                    Notes = m.Notes
                };
            }).ToList();

            return Ok(results);
        }

        [HttpPost("{id}/movements")]
        public async Task<IActionResult> LogMovement(
            int id,
            [FromBody] LogMovementRequestDto request)
        {
            // Only roles allowed to log movements (otherwise 403).
            if (!(await _authorizationService.AuthorizeAsync(User, AuthPolicies.CanLogMovements)).Succeeded)
            {
                return Forbid();
            }

            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.VehicleId == id);

            if (vehicle == null)
            {
                return NotFound("Vehicle not found.");
            }

            if (!vehicle.IsActive)
            {
                return BadRequest("Inactive vehicles cannot have movements logged.");
            }

            if (vehicle.Status == "Sold")
            {
                return BadRequest("Sold vehicles cannot have movements logged.");
            }

            var latestMovement = await _context.Movements
                .Where(m => m.VehicleId == id)
                .OrderByDescending(m => m.MovementDateTime)
                .FirstOrDefaultAsync();

            // A vehicle with no movements yet has no current location. Its first
            // movement is an initial placement, saved with no "from" location.
            int? fromLocationId = latestMovement?.ToLocationId;

            var destination = await _context.Locations
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LocationId == request.ToLocationId);

            if (destination == null)
            {
                return BadRequest("Destination location does not exist.");
            }

            if (fromLocationId.HasValue && request.ToLocationId == fromLocationId.Value)
            {
                return BadRequest(
                    "Destination must be different from the current location.");
            }

            var staffIdClaim = User.FindFirst("staff_id");

            if (staffIdClaim == null ||
                !int.TryParse(staffIdClaim.Value, out var staffId))
            {
                return Unauthorized(
                    "Authenticated staff member could not be identified.");
            }

            if (!string.IsNullOrWhiteSpace(request.Notes) &&
                request.Notes.Length > 500)
            {
                return BadRequest("Notes cannot exceed 500 characters.");
            }

            if (!string.IsNullOrWhiteSpace(request.NewStatus))
            {
                var validStatuses = new[]
                {
                    "Available",
                    "Reserved",
                    "In Transit",
                    "In Workshop",
                    "Sold"
                };

                if (!validStatuses.Contains(request.NewStatus.Trim()))
                {
                    return BadRequest("Invalid vehicle status.");
                }

                if (request.NewStatus.Trim() == "Sold")
                {
                    return BadRequest(
                        "A vehicle cannot be marked as Sold when logging a movement.");
                }
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var movement = new APCVehicleTracker.Data.Models.Movement
                {
                    VehicleId = vehicle.VehicleId,
                    FromLocationId = fromLocationId,
                    ToLocationId = request.ToLocationId,
                    StaffId = staffId,
                    MovementDateTime = DateTime.UtcNow,
                    Notes = string.IsNullOrWhiteSpace(request.Notes)
                        ? null
                        : request.Notes.Trim()
                };

                _context.Movements.Add(movement);

                if (!string.IsNullOrWhiteSpace(request.NewStatus))
                {
                    vehicle.Status = request.NewStatus.Trim();
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Vehicle movement logged successfully.",
                    movementId = movement.MovementId
                });
            }
            catch
            {
                await transaction.RollbackAsync();

                return StatusCode(
                    500,
                    "The movement could not be saved.");
            }
        }

        [HttpGet("makes")]
        public async Task<IActionResult> GetMakes()
        {
            var makes = await _context.Vehicles
                .AsNoTracking()
                .Where(v => v.IsActive && v.Status != "Sold")
                .Select(v => v.Make)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync();

            return Ok(makes);
        }

        [HttpGet("models")]
        public async Task<IActionResult> GetModels(string? make = null)
        {
            var query = _context.Vehicles
                .AsNoTracking()
                .Where(v => v.IsActive && v.Status != "Sold");

            if (!string.IsNullOrWhiteSpace(make))
            {
                query = query.Where(v => v.Make == make);
            }

            var models = await query
                .Select(v => v.Model)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync();

            return Ok(models);
        }
    }
}