using APCVehicleTracker.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace APCVehicleTracker.Data.Repositories;

public interface IStaffRepository
{
    Task<List<Staff>> ListAsync(CancellationToken cancellationToken = default);
    Task<Staff?> GetAsync(int staffId, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task AddAsync(Staff staff, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public class StaffRepository : IStaffRepository
{
    private readonly ApplicationDbContext _db;
    public StaffRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<List<Staff>> ListAsync(CancellationToken cancellationToken = default)
    {
        return _db.Staff.AsNoTracking()
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ToListAsync(cancellationToken);
    }

    public Task<Staff?> GetAsync(int staffId, CancellationToken cancellationToken = default)
    {
        return _db.Staff.FirstOrDefaultAsync(s => s.StaffId == staffId, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var lower = email.Trim().ToLower();
        return _db.Staff.AnyAsync(s => s.Email.ToLower() == lower, cancellationToken);
    }

    public async Task AddAsync(Staff staff, CancellationToken cancellationToken = default)
    {
        _db.Staff.Add(staff);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
}
