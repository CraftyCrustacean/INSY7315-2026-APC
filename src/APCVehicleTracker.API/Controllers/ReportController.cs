namespace APCVehicleTracker.API.Controllers;

using APCVehicleTracker.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;


    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,Branch Manager")]

public class ReportsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public ReportsController(ApplicationDbContext context) => _context = context;

        private record Row(int VehicleId, string Registration, string Make, string Model,
            string Status, int? LocationId, string Location, DateTime? LastMoved, DateTime? FirstMoved);

        private async Task<List<Row>> GetRowsAsync()
        {
            var raw = await (
                from v in _context.Vehicles.AsNoTracking()
                where v.Status != "Sold"
                let last = _context.Movements
                    .Where(m => m.VehicleId == v.VehicleId)
                    .OrderByDescending(m => m.MovementDateTime)
                    .ThenByDescending(m => m.MovementId)
                    .FirstOrDefault()
                let first = _context.Movements
                    .Where(m => m.VehicleId == v.VehicleId)
                    .OrderBy(m => m.MovementDateTime)
                    .Select(m => (DateTime?)m.MovementDateTime)
                    .FirstOrDefault()
                select new
                {
                    v.VehicleId,
                    v.Registration,
                    v.Make,
                    v.Model,
                    v.Status,
                    LocationId = last == null ? (int?)null : last.ToLocationId,
                    LastMoved = last == null ? (DateTime?)null : last.MovementDateTime,
                    FirstMoved = first
                }).ToListAsync();

            var names = await _context.Locations.AsNoTracking()
                .ToDictionaryAsync(l => l.LocationId, l => l.LocationName);

            return raw.Select(r => new Row(r.VehicleId, r.Registration, r.Make, r.Model, r.Status,
                r.LocationId,
                r.LocationId.HasValue && names.TryGetValue(r.LocationId.Value, out var n) ? n : "Unknown",
                r.LastMoved, r.FirstMoved)).ToList();
        }

        private static int DaysSince(DateTime? d) =>
            d.HasValue ? Math.Max(0, (DateTime.UtcNow.Date - d.Value.Date).Days) : 0;

        private static string Csv(string s) => $"\"{s.Replace("\"", "\"\"")}\"";

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(int stuckDays = 2)
        {
            var rows = await GetRowsAsync();
            return Ok(new
            {
                totalInStock = rows.Count(r => r.Status == "In Stock"),
                totalInWorkshop = rows.Count(r => r.Status == "In Workshop"),
                stuckVehicles = rows.Count(r => r.LastMoved.HasValue && DaysSince(r.LastMoved) > stuckDays)
            });
        }

        [HttpGet("locations")]
        public async Task<IActionResult> GetLocations() =>
            Ok(await _context.Locations.AsNoTracking()
                .OrderBy(l => l.LocationName)
                .Select(l => new { l.LocationId, l.LocationName })
                .ToListAsync());

        [HttpGet("stuck")]
        public async Task<IActionResult> GetStuck(int? locationId, int days = 2)
        {
            var rows = await GetRowsAsync();
            var result = rows
                .Where(r => r.LastMoved.HasValue && DaysSince(r.LastMoved) > days)
                .Where(r => !locationId.HasValue || r.LocationId == locationId)
                .Select(r => new
                {
                    r.VehicleId,
                    r.Registration,
                    r.Make,
                    r.Model,
                    r.Location,
                    daysAtLocation = DaysSince(r.LastMoved)
                })
                .OrderByDescending(r => r.daysAtLocation);
            return Ok(result);
        }

        [HttpGet("stock-aging")]
        public async Task<IActionResult> GetStockAging()
        {
            var rows = await GetRowsAsync();
            return Ok(rows.Select(r => new
            {
                r.VehicleId,
                r.Registration,
                r.Make,
                r.Model,
                r.Status,
                startDate = r.FirstMoved,
                daysInStock = DaysSince(r.FirstMoved)
            }).OrderByDescending(r => r.daysInStock));
        }

        [HttpGet("stuck/export")]
        public async Task<IActionResult> ExportStuck(int? locationId, int days = 2)
        {
            var rows = (await GetRowsAsync())
                .Where(r => r.LastMoved.HasValue && DaysSince(r.LastMoved) > days)
                .Where(r => !locationId.HasValue || r.LocationId == locationId);

            var sb = new StringBuilder("Registration,Make,Model,Location,Days At Location\n");
            foreach (var r in rows)
                sb.AppendLine($"{Csv(r.Registration)},{Csv(r.Make)},{Csv(r.Model)},{Csv(r.Location)},{DaysSince(r.LastMoved)}");
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "stuck-vehicles.csv");
        }

        [HttpGet("stock-aging/export")]
        public async Task<IActionResult> ExportStockAging()
        {
            var sb = new StringBuilder("Registration,Make,Model,Status,Start Date,Days In Stock\n");
            foreach (var r in await GetRowsAsync())
                sb.AppendLine($"{Csv(r.Registration)},{Csv(r.Make)},{Csv(r.Model)},{Csv(r.Status)}," +
                              $"{r.FirstMoved:yyyy-MM-dd},{DaysSince(r.FirstMoved)}");
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "stock-aging.csv");
        }
    }
