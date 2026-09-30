using APCVehicleTracker.Auth;
using APCVehicleTracker.Data;
using APCVehicleTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using APCVehicleTracker.Data.Auth;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAppDatabase(builder.Configuration);
builder.Services.AddAppAuthentication(builder.Configuration);
builder.Services.AddAppAuthorization();
builder.Services.AddTransient<ApiTokenHandler>();

builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.Filters.Add(new AuthorizeFilter(policy));
    options.Filters.Add(new AuthorizeForScopesAttribute { ScopeKeySection = "VehicleApi:Scopes" });
}).AddMicrosoftIdentityUI();

builder.Services.AddSession();

builder.Services.AddHttpClient<VehicleApiService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["VehicleApi:BaseUrl"]!);
}).AddHttpMessageHandler<ApiTokenHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();