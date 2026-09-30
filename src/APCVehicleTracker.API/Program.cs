using APCVehicleTracker.API.Services;
using APCVehicleTracker.Data;
using APCVehicleTracker.Data.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppDatabase(
    builder.Configuration);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(
        builder.Configuration.GetSection("AzureAd"));

builder.Services.AddScoped<IStaffLookup, DbStaffLookup>();

builder.Services.AddTransient<
    IClaimsTransformation,
    StaffClaimsTransformation>();

builder.Services.AddAppAuthorization();

builder.Services.AddAuthorization(auth =>
{
    var apiUser = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireScope("access_as_user")
        .Build();

    auth.DefaultPolicy = apiUser;
    auth.FallbackPolicy = apiUser;
});

builder.Services.AddControllers();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

