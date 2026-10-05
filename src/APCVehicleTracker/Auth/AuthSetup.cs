using APCVehicleTracker.Data.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;

namespace APCVehicleTracker.Auth;

public static class AuthSetup
{
    public static IServiceCollection AddAppAuthentication(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IStaffLookup, DbStaffLookup>();
        services.AddScoped<StaffSigninService>();

        services
            .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(config.GetSection("AzureAd"))
            .EnableTokenAcquisitionToCallDownstreamApi(config.GetSection("VehicleApi:Scopes").Get<string[]>())
            .AddInMemoryTokenCaches();

        services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            var existing = options.Events.OnTokenValidated;
            options.Events.OnTokenValidated = async vld =>
            {
                await existing(vld);

                var signIn = vld.HttpContext.RequestServices.GetRequiredService<StaffSigninService>();
                if (!await signIn.StaffClaimAsync(vld.Principal!, vld.HttpContext.RequestAborted))
                {
                    vld.HandleResponse();
                    vld.Response.Redirect("/Home/NotAuthorised");
                }

                var existingRedirect = options.Events.OnRedirectToIdentityProvider;
                options.Events.OnRedirectToIdentityProvider = async vld =>
                {
                    await existingRedirect(vld);
                    vld.ProtocolMessage.Prompt = "login";
                };
            };
        });

        services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, cookie =>
        {
            cookie.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            cookie.SlidingExpiration = true;
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            cookie.AccessDeniedPath = "/Home/AccessDenied";
        });

        return services;
    }
}