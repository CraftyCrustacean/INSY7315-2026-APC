using APCVehicleTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace APCVehicleTracker.Services;

public class DbStaffLookup : IStaffLookup
{
    private readonly ApplicationDbContext _db;
    public DbStaffLookup(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<StaffInfo?> FindByObjectIdAsync(string objectId, CancellationToken cancellationToken = default)
    {
        return await _db.Staff
            .AsNoTracking()
            .Where(s => s.EntraObjectId == objectId)
            .Select(s => new StaffInfo(s.StaffId, s.EntraObjectId, s.Email, s.FirstName, s.LastName, s.Role, s.IsActive))
            .FirstOrDefaultAsync(cancellationToken); 
    }
}
