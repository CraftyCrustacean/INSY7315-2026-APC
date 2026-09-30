using APCVehicleTracker.Data.Repositories;

namespace APCVehicleTracker.API.Services;

public static class StaffAdminSetup
{
    public static IServiceCollection AddStaffAdmin(this IServiceCollection services)
    {
        services.AddScoped<IStaffRepository, StaffRepository>();
        services.AddSingleton<IGraphUserService, GraphUserService>();
        services.AddScoped<AdminService>();
        return services;
    }
}
