namespace APCVehicleTracker.Services;

public record StaffInfo(int StaffId, string EntraObjectId, string Email, string FirstName, string LastName, string Role, bool IsActive);
public interface IStaffLookup
{
    Task<StaffInfo?> FindByObjectIdAsync(string objectId, CancellationToken cancellationToken = default);
}
