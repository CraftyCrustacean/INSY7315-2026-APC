using APCVehicleTracker.API.Auth;
using APCVehicleTracker.API.Services;
using APCVehicleTracker.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddAppDatabase(builder.Configuration);
builder.Services.AddApiAuthentication(builder.Configuration);
builder.Services.AddStaffAdmin();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next();
});

app.UseAuthentication();
app.UseStaffCheck();
app.UseAuthorization();

app.MapControllers();

app.Run();