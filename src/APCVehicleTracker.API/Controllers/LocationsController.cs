
using APCVehicleTracker.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APCVehicleTracker.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LocationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LocationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetLocations()
        {
            var locations = await _context.Locations
                .AsNoTracking()
                .ToListAsync();

            return Ok(locations);
        }
    }
}

