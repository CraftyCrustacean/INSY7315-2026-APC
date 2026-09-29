using System.Runtime.CompilerServices;
using APCVehicleTracker.Models;

namespace APCVehicleTracker.Auth;

public static class AuthPolicies
{
    public const string CanManageUsers = nameof(CanManageUsers);
    public const string CanEditVehicles = nameof(CanEditVehicles);
    public const string CanLogMovements = nameof(CanLogMovements);
    public const string CanViewReports = nameof(CanViewReports);

    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
        => services.AddAuthorization(auth =>
        {
            auth.AddPolicy(CanManageUsers, p => p.RequireRole(StaffRoles.Admin));
            auth.AddPolicy(CanEditVehicles, p => p.RequireRole(StaffRoles.Admin, StaffRoles.StockController));
            auth.AddPolicy(CanLogMovements, p => p.RequireRole(StaffRoles.Admin, StaffRoles.StockController, StaffRoles.BranchManager, StaffRoles.TransportStaff));
            auth.AddPolicy(CanViewReports, p => p.RequireRole(StaffRoles.Admin, StaffRoles.BranchManager));
        });

}

