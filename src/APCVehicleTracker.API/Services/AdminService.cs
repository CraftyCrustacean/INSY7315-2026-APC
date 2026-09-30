using APCVehicleTracker.Data.Auth;
using APCVehicleTracker.Data.Contracts;
using APCVehicleTracker.Data.Models;
using APCVehicleTracker.Data.Repositories;

namespace APCVehicleTracker.API.Services;

public class AdminService
{
    private readonly IStaffRepository _repo;
    private readonly IGraphUserService _graph;
    private readonly ILogger<AdminService> _log;

    public AdminService(IStaffRepository repo, IGraphUserService graph, ILogger<AdminService> log)
    {
        _repo = repo;
        _graph = graph;
        _log = log;
    }

    public async Task<List<StaffDto>> ListAsync(CancellationToken cancelationToken = default)
        => (await _repo.ListAsync(cancelationToken)).Select(ToDto).ToList();

    public async Task<StaffDto> GetAsync(int id, CancellationToken cancelationToken = default)
        => ToDto(await _repo.GetAsync(id, cancelationToken) ?? throw NotFound(id));

    public async Task<TemporaryPasswordResponse> CreateAsync(CreateStaffRequest req, int actingStaffId, CancellationToken cancelationToken = default)
    {
        ValidateRole(req.Role);
        var email = req.Email.Trim().ToLowerInvariant();

        if (await _repo.EmailExistsAsync(email, cancelationToken))
            throw new AdminException(StaffAdminErrorType.DuplicateEmail, "An account with this email already exists.");

        var password = TemporaryPasswordGenerator.Generate();
        var objectId = await _graph.CreateUserAsync(req.FirstName.Trim(), req.LastName.Trim(), email, password, cancelationToken);

        var staff = new Staff
        {
            EntraObjectId = objectId,
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            Email = email,
            Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
            Role = req.Role,
            IsActive = true
        };

        try
        {
            await _repo.AddAsync(staff, cancelationToken);
        }
        catch
        {
            // If Entra worked but the db didnt for some reason, delete the entra user to keep sync.
            try { await _graph.DeleteUserAsync(objectId, CancellationToken.None); }
            catch (Exception deleteError)
            {
                _log.LogError(deleteError, "Rollback failed, entra user {ObjectId} exists without a staff row", objectId);
            }
            throw;
        }

        _log.LogInformation("Created staff {StaffId} ({Role})", staff.StaffId, staff.Role);
        return new TemporaryPasswordResponse(ToDto(staff), password);
    }

    public async Task<StaffDto> UpdateAsync(int id, UpdateStaffRequest req, int actingStaffId, CancellationToken cancelationToken = default)
    {
        ValidateRole(req.Role);
        var staff = await _repo.GetAsync(id, cancelationToken) ?? throw NotFound(id);

        if (id == actingStaffId && staff.Role == StaffRoles.Admin && req.Role != StaffRoles.Admin)
            throw new AdminException(StaffAdminErrorType.Validation, "You cant remove your own admin role...");

        var oldRole = staff.Role;
        staff.FirstName = req.FirstName.Trim();
        staff.LastName = req.LastName.Trim();
        staff.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
        staff.Role = req.Role;
        await _repo.SaveChangesAsync(cancelationToken);

        _log.LogInformation("Updated staff {StaffId} (role {Old} -> {New})", id, oldRole, staff.Role);
        return ToDto(staff);
    }

    public async Task<TemporaryPasswordResponse> ResetPasswordAsync(int id, int actingStaffId, CancellationToken cancelationToken = default)
    {
        var staff = await _repo.GetAsync(id, cancelationToken) ?? throw NotFound(id);
        if (string.IsNullOrEmpty(staff.EntraObjectId))
            throw new AdminException(StaffAdminErrorType.NoSignInAccount, "This staff member has no account.");

        var password = TemporaryPasswordGenerator.Generate();
        await _graph.ResetPasswordAsync(staff.EntraObjectId, password, cancelationToken);

        _log.LogInformation("Admin {Actor} reset the password for staff member {StaffId}", actingStaffId, id);
        return new TemporaryPasswordResponse(ToDto(staff), password);
    }

    private static void ValidateRole(string role)
    {
        if (!StaffRoles.All.Contains(role))
            throw new AdminException(StaffAdminErrorType.Validation, $"'{role}' is not a valid role.");
    }

    private static AdminException NotFound(int id)
        => new(StaffAdminErrorType.NotFound, $"Staff member {id} was not found.");

    private static StaffDto ToDto(Staff staff)
        => new(staff.StaffId, staff.FirstName, staff.LastName, staff.Email, staff.Phone, staff.Role, staff.IsActive,
               !string.IsNullOrEmpty(staff.EntraObjectId));
}
