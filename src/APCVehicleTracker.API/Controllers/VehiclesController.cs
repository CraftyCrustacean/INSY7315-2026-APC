using APCVehicleTracker.API.Data;
using APCVehicleTracker.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APCVehicleTracker.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VehiclesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public VehiclesController(ApplicationDbContext context) { _context = context; }

        [HttpGet]
        public async Task<IActionResult> SearchVehicles(
            string? make = null, string? model = null, int? yearFrom = null, int? yearTo = null,
            [FromQuery] string[]? status = null, [FromQuery] int[]? location = null,
            bool includeSold = false, int page = 1, int pageSize = 25)
        {
            if (page < 1) page = 1;
            pageSize = 25;

            var query = _context.Vehicles.AsNoTracking().AsQueryable();

            // exclude sold vehicles by default
            if (!includeSold) query = query.Where(v => v.Status != "Sold");

            // make filter
            if (!string.IsNullOrWhiteSpace(make)) query = query.Where(v => v.Make == make);

            // model filter
            if (!string.IsNullOrWhiteSpace(model)) query = query.Where(v => v.Model == model);

            // year filters
            if (yearFrom.HasValue) query = query.Where(v => v.Year >= yearFrom.Value);
            if (yearTo.HasValue) query = query.Where(v => v.Year <= yearTo.Value);

            // status filter
            if (status != null && status.Length > 0) query = query.Where(v => status.Contains(v.Status));

            var totalCount = await query.CountAsync();

            var vehicles = await query
                .OrderBy(v => v.VehicleId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vehicleIds = vehicles.Select(v => v.VehicleId).ToList();

            var movements = await _context.Movements
                .AsNoTracking()
                .Where(m => vehicleIds.Contains(m.VehicleId))
                .OrderByDescending(m => m.MovementDateTime)
                .ToListAsync();

            var locations = await _context.Locations.AsNoTracking().ToListAsync();

            var images = await _context.VehicleImages
                .AsNoTracking()
                .Where(i => vehicleIds.Contains(i.VehicleId))
                .OrderBy(i => i.UploadedDate)
                .ToListAsync();

            var results = vehicles.Select(vehicle =>
            {
                var latestMovement = movements
                    .Where(m => m.VehicleId == vehicle.VehicleId)
                    .OrderByDescending(m => m.MovementDateTime)
                    .FirstOrDefault();

                var currentLocation = latestMovement == null ? null :
                    locations.FirstOrDefault(l => l.LocationId == latestMovement.ToLocationId)?.LocationName;

                var primaryImage = images.FirstOrDefault(i => i.VehicleId == vehicle.VehicleId)?.ImageUrl;

                int? daysAtLocation = latestMovement == null ? null :
                    (int)(DateTime.UtcNow - latestMovement.MovementDateTime).TotalDays;

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

            // Location filter
            if (location != null && location.Length > 0)
            {
                results = results.Where(v =>
                    v.CurrentLocation != null &&
                    location.Any(locationId => locations.Any(l =>
                        l.LocationId == locationId && l.LocationName == v.CurrentLocation))
                ).ToList();
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
                var vehicle = await _context.Vehicles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.VehicleId == id);

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

                var primaryImage = await _context.VehicleImages
                    .AsNoTracking()
                    .Where(i => i.VehicleId == id)
                    .OrderBy(i => i.UploadedDate)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefaultAsync();

                var result = new VehicleDetailsDto
                {
                    VehicleId = vehicle.VehicleId,
                    PrimaryImage = primaryImage,
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
                    ? locations.FirstOrDefault(l => l.LocationId == m.FromLocationId.Value)?.LocationName
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
        [HttpGet("makes")]
        public async Task<IActionResult> GetMakes()
        {
            var makes = await _context.Vehicles
                .AsNoTracking()
                .Where(v => v.Status != "Sold")
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
                .Where(v => v.Status != "Sold");

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


