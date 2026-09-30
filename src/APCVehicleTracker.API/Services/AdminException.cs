namespace APCVehicleTracker.API.Services;

public enum StaffAdminErrorType { Validation, DuplicateEmail, NotFound, NoSignInAccount }

public class AdminException(StaffAdminErrorType type, string message) : Exception(message)
{
    public StaffAdminErrorType Type { get; } = type;
}
