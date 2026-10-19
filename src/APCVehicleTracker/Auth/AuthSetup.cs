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

            // Tell Entra which account is signing out so it can skip the "pick an account" page.
            // Needs the login_hint optional claim on the ID token (App registration > Token configuration).
            var existingSignOut = options.Events.OnRedirectToIdentityProviderForSignOut;
            options.Events.OnRedirectToIdentityProviderForSignOut = async ctx =>
            {
                await existingSignOut(ctx);
                var loginHint = ctx.HttpContext.User.FindFirst("login_hint")?.Value;
                if (!string.IsNullOrEmpty(loginHint))
                {
                    ctx.ProtocolMessage.SetParameter("logout_hint", loginHint);
                }
            };

            // After Entra signs the user out, go to the home page instead of the "Signed out" page.
            // Every page needs sign-in, so this lands straight on the Entra sign-in screen.
            var existingSignedOut = options.Events.OnSignedOutCallbackRedirect;
            options.Events.OnSignedOutCallbackRedirect = async ctx =>
            {
                await existingSignedOut(ctx);
                ctx.Response.Redirect(ctx.Request.PathBase + "/");
                ctx.HandleResponse();
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