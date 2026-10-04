using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;

namespace APCVehicleTracker.Data.Auth;

public static class AuthPolicies
{
    public const string CanManageUsers = nameof(CanManageUsers);
    public const string CanEditVehicles = nameof(CanEditVehicles);
    public const string CanLogMovements = nameof(CanLogMovements);
    public const string CanViewReports = nameof(CanViewReports);
    public const string CanReactivateVehicles = nameof(CanReactivateVehicles);

    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
        => services.AddAuthorization(auth =>
        {
            auth.AddPolicy(CanManageUsers, p => p.RequireRole(StaffRoles.Admin));
            auth.AddPolicy(CanEditVehicles, p => p.RequireRole(StaffRoles.Admin, StaffRoles.StockController));
            auth.AddPolicy(CanLogMovements, p => p.RequireRole(StaffRoles.Admin, StaffRoles.StockController, StaffRoles.BranchManager, StaffRoles.TransportStaff));
            auth.AddPolicy(CanReactivateVehicles, p => p.RequireRole(StaffRoles.Admin));
            auth.AddPolicy(CanViewReports, p => p.RequireRole(StaffRoles.Admin, StaffRoles.BranchManager));
        });

}

