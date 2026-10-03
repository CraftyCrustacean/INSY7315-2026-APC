using APCVehicleTracker.API.DTOs;
using APCVehicleTracker.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APCVehicleTracker.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VehiclesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public VehiclesController(ApplicationDbContext context)
        {
            _context = context;
        }

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

            // Active vehicles by default. Admins can request the inactive list (reactivate screen).
            var showInactive = inactiveOnly && User.IsInRole(StaffRoles.Admin);
                query = query.Where(v => v.IsActive != showInactive);

            // Exclude sold vehicles by default.
            if (!includeSold)
                query = query.Where(v => v.Status != "Sold");

            // Make filter.
            if (!string.IsNullOrWhiteSpace(make))
                query = query.Where(v => v.Make == make);

            // Model filter.
            if (!string.IsNullOrWhiteSpace(model))
                query = query.Where(v => v.Model == model);

            // Year filters.
            if (yearFrom.HasValue)
                query = query.Where(v => v.Year >= yearFrom.Value);

            if (yearTo.HasValue)
                query = query.Where(v => v.Year <= yearTo.Value);

            // Status filter.
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

            // Location filter.
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
            var isAdmin = User.IsInRole(StaffRoles.Admin);

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

            // Sold vehicles cannot be moved.
            if (vehicle.Status == "Sold")
            {
                return BadRequest("Sold vehicles cannot have movements logged.");
            }

            // Determine the current location from the latest movement.
            var latestMovement = await _context.Movements
                .Where(m => m.VehicleId == id)
                .OrderByDescending(m => m.MovementDateTime)
                .FirstOrDefaultAsync();

            if (latestMovement == null)
            {
                return BadRequest("The vehicle does not have a current location.");
            }

            var fromLocationId = latestMovement.ToLocationId;

            // Validate destination exists.
            var destination = await _context.Locations
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LocationId == request.ToLocationId);

            if (destination == null)
            {
                return BadRequest("Destination location does not exist.");
            }

            // Destination must be different from current location.
            if (request.ToLocationId == fromLocationId)
            {
                return BadRequest(
                    "Destination must be different from the current location.");
            }

            // Get StaffId from the authenticated user's claim.
            var staffIdClaim = User.FindFirst("staff_id");

            if (staffIdClaim == null ||
                !int.TryParse(staffIdClaim.Value, out var staffId))
            {
                return Unauthorized(
                    "Authenticated staff member could not be identified.");
            }

            // Validate notes.
            if (!string.IsNullOrWhiteSpace(request.Notes) &&
                request.Notes.Length > 500)
            {
                return BadRequest("Notes cannot exceed 500 characters.");
            }

            // Validate new status when supplied.
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

                // Do not mark a vehicle Sold through movement logging.
                if (request.NewStatus.Trim() == "Sold")
                {
                    return BadRequest(
                        "A vehicle cannot be marked as Sold when logging a movement.");
                }
            }

            // Save movement and vehicle changes as one transaction.
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

