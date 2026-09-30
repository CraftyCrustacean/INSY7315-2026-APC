using APCVehicleTracker.Data.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

namespace APCVehicleTracker.API.Auth;

public static class ApiAuthSetup
{
    public const string RequiredScope = "access_as_user";

    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration config)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddMicrosoftIdentityWebApi(config.GetSection("AzureAd"));

        services.AddScoped<IStaffLookup, DbStaffLookup>();
        services.AddScoped<StaffSigninService>();

        services.AddAuthorization(auth =>
        {
            var apiUser = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireScope(RequiredScope)
                .Build();
            auth.DefaultPolicy = apiUser;
            auth.FallbackPolicy = apiUser;
        });
        services.AddAppAuthorization();

        return services;
    }

    public static IApplicationBuilder UseStaffCheck(this IApplicationBuilder app)
        => app.UseMiddleware<CheckStaff>();
}