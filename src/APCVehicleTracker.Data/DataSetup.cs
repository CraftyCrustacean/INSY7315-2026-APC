using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace APCVehicleTracker.Data;

public static class DataSetup
{
    public static IServiceCollection AddAppDatabase(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionString: DefaultConnection isnt configured.");
        return services.AddDbContext<ApplicationDbContext>(s => s.UseNpgsql(cs));
    }
}
