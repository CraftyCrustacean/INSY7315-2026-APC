using APCVehicleTracker.API.Controllers;
using APCVehicleTracker.API.DTOs;
using APCVehicleTracker.Data;
using APCVehicleTracker.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using APCVehicleTracker.Data.Auth;

namespace APCVehicleTracker.Tests
{
    public class LogMovementTests
    {
        private static IAuthorizationService BuildAuthService()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAppAuthorization();
            return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
        }

        private static async Task<(ApplicationDbContext Context, SqliteConnection Connection)>
            CreateDatabaseAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new ApplicationDbContext(options);

            await context.Database.EnsureCreatedAsync();

            return (context, connection);
        }

        // Builds a signed-in user. The role is what the CanLogMovements policy checks,
        // so pass role: null to test a user without permission.
        private static void SetUserWithStaffId(
            VehiclesController controller,
            int staffId,
            string? role = "Admin")
        {
            var claims = new List<Claim>
            {
                new Claim("staff_id", staffId.ToString())
            };

            if (role != null)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
                claims.Add(new Claim("role", role));
            }

            var identity = new ClaimsIdentity(claims, "TestAuthentication");

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };
        }

        private static async Task SeedStaffAsync(
            ApplicationDbContext context,
            int staffId = 1)
        {
            context.Staff.Add(new Staff
            {
                StaffId = staffId,
                EntraObjectId = "test-object-id",
                FirstName = "Test",
                LastName = "Staff",
                Email = "test@example.com",
                Role = "Admin",
                IsActive = true
            });

            await context.SaveChangesAsync();
        }

        private static async Task<(Vehicle Vehicle, Location CurrentLocation, Location Destination)>
            SeedVehicleAsync(ApplicationDbContext context)
        {
            var currentLocation = new Location
            {
                LocationId = 1,
                LocationName = "Showroom",
                LocationType = "Showroom",
                Address = "Test Address 1"
            };

            var destination = new Location
            {
                LocationId = 2,
                LocationName = "Workshop",
                LocationType = "Workshop",
                Address = "Test Address 2"
            };

            var vehicle = new Vehicle
            {
                VehicleId = 1,
                Make = "Toyota",
                Model = "Hilux",
                Year = 2025,
                Registration = "TEST123",
                Vin = "TESTVIN123",
                Status = "Available"
            };

            context.Locations.AddRange(
                currentLocation,
                destination);

            context.Vehicles.Add(vehicle);

            context.Movements.Add(new Movement
            {
                MovementId = 1,
                VehicleId = vehicle.VehicleId,
                FromLocationId = null,
                ToLocationId = currentLocation.LocationId,
                StaffId = 1,
                MovementDateTime = DateTime.UtcNow.AddDays(-1),
                Notes = "Starting location"
            });

            await context.SaveChangesAsync();

            return (vehicle, currentLocation, destination);
        }

        // A vehicle that has never been moved, so it has no current location.
        private static async Task<(Vehicle Vehicle, Location Destination)>
            SeedVehicleWithoutMovementAsync(ApplicationDbContext context)
        {
            var destination = new Location
            {
                LocationId = 2,
                LocationName = "Workshop",
                LocationType = "Workshop",
                Address = "Test Address 2"
            };

            var vehicle = new Vehicle
            {
                VehicleId = 1,
                Make = "Toyota",
                Model = "Hilux",
                Year = 2025,
                Registration = "TEST123",
                Vin = "TESTVIN123",
                Status = "Available"
            };

            context.Locations.Add(destination);
            context.Vehicles.Add(vehicle);

            await context.SaveChangesAsync();

            return (vehicle, destination);
        }

        [Fact]
        public async Task LogMovement_CreatesMovementWithStaffAndServerDate()
        {
            var (context, connection) = await CreateDatabaseAsync();

            try
            {
                await SeedStaffAsync(context);

                var seeded = await SeedVehicleAsync(context);

                var controller = new VehiclesController(context, BuildAuthService());

                SetUserWithStaffId(controller, 1);

                var before = DateTime.UtcNow;

                var request = new LogMovementRequestDto
                {
                    ToLocationId = seeded.Destination.LocationId,
                    NewStatus = null,
                    Notes = "Moved to workshop"
                };

                var result = await controller.LogMovement(
                    seeded.Vehicle.VehicleId,
                    request);

                var after = DateTime.UtcNow;

                var movement = await context.Movements
                    .OrderByDescending(m => m.MovementId)
                    .FirstAsync();

                Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);

                Assert.Equal(
                    seeded.Vehicle.VehicleId,
                    movement.VehicleId);

                Assert.Equal(
                    seeded.CurrentLocation.LocationId,
                    movement.FromLocationId);

                Assert.Equal(
                    seeded.Destination.LocationId,
                    movement.ToLocationId);

                Assert.Equal(1, movement.StaffId);

                Assert.Equal(
                    "Moved to workshop",
                    movement.Notes);

                Assert.InRange(
                    movement.MovementDateTime,
                    before,
                    after);
            }
            finally
            {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task LogMovement_UpdatesVehicleStatusWhenNewStatusProvided()
        {
            var (context, connection) = await CreateDatabaseAsync();

            try
            {
                await SeedStaffAsync(context);

                var seeded = await SeedVehicleAsync(context);

                var controller = new VehiclesController(context, BuildAuthService());

                SetUserWithStaffId(controller, 1);

                var request = new LogMovementRequestDto
                {
                    ToLocationId = seeded.Destination.LocationId,
                    NewStatus = "In Workshop",
                    Notes = null
                };

                var result = await controller.LogMovement(
                    seeded.Vehicle.VehicleId,
                    request);

                var vehicle = await context.Vehicles
                    .FirstAsync(v => v.VehicleId == seeded.Vehicle.VehicleId);

                Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
                Assert.Equal("In Workshop", vehicle.Status);
            }
            finally
            {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task LogMovement_RejectsSoldVehicle()
        {
            var (context, connection) = await CreateDatabaseAsync();

            try
            {
                await SeedStaffAsync(context);

                var seeded = await SeedVehicleAsync(context);

                seeded.Vehicle.Status = "Sold";
                await context.SaveChangesAsync();

                var controller = new VehiclesController(context, BuildAuthService());

                SetUserWithStaffId(controller, 1);

                var request = new LogMovementRequestDto
                {
                    ToLocationId = seeded.Destination.LocationId
                };

                var result = await controller.LogMovement(
                    seeded.Vehicle.VehicleId,
                    request);

                var badRequest =
                    Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);

                Assert.Equal(
                    "Sold vehicles cannot have movements logged.",
                    badRequest.Value);

                var movementCount = await context.Movements.CountAsync();

                Assert.Equal(1, movementCount);
            }
            finally
            {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task LogMovement_RejectsSameDestinationAsCurrentLocation()
        {
            var (context, connection) = await CreateDatabaseAsync();

            try
            {
                await SeedStaffAsync(context);

                var seeded = await SeedVehicleAsync(context);

                var controller = new VehiclesController(context, BuildAuthService());

                SetUserWithStaffId(controller, 1);

                var request = new LogMovementRequestDto
                {
                    ToLocationId = seeded.CurrentLocation.LocationId
                };

                var result = await controller.LogMovement(
                    seeded.Vehicle.VehicleId,
                    request);

                var badRequest =
                    Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);

                Assert.Equal(
                    "Destination must be different from the current location.",
                    badRequest.Value);

                var movementCount = await context.Movements.CountAsync();

                Assert.Equal(1, movementCount);
            }
            finally
            {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task LogMovement_RejectsMissingDestination()
        {
            var (context, connection) = await CreateDatabaseAsync();

            try
            {
                await SeedStaffAsync(context);

                var seeded = await SeedVehicleAsync(context);

                var controller = new VehiclesController(context, BuildAuthService());

                SetUserWithStaffId(controller, 1);

                var request = new LogMovementRequestDto
                {
                    ToLocationId = 999
                };

                var result = await controller.LogMovement(
                    seeded.Vehicle.VehicleId,
                    request);

                var badRequest =
                    Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);

                Assert.Equal(
                    "Destination location does not exist.",
                    badRequest.Value);
            }
            finally
            {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task LogMovement_RejectsNotesOver500Characters()
        {
            var (context, connection) = await CreateDatabaseAsync();

            try
            {
                await SeedStaffAsync(context);

                var seeded = await SeedVehicleAsync(context);

                var controller = new VehiclesController(context, BuildAuthService());

                SetUserWithStaffId(controller, 1);

                var request = new LogMovementRequestDto
                {
                    ToLocationId = seeded.Destination.LocationId,
                    Notes = new string('A', 501)
                };

                var result = await controller.LogMovement(
                    seeded.Vehicle.VehicleId,
                    request);

                var badRequest =
                    Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);

                Assert.Equal(
                    "Notes cannot exceed 500 characters.",
                    badRequest.Value);
            }
            finally
            {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task LogMovement_RejectsRequestWithoutStaffClaim()
        {
            var (context, connection) = await CreateDatabaseAsync();

            try
            {
                var seeded = await SeedVehicleAsync(context);

                var controller = new VehiclesController(context, BuildAuthService());

                // Has permission (role) but no staff_id claim.
                var identity = new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.Role, "Admin"),
                        new Claim("role", "Admin")
                    },
                    "TestAuthentication");

                controller.ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(identity)
                    }
                };

                var request = new LogMovementRequestDto
                {
                    ToLocationId = seeded.Destination.LocationId
                };

                var result = await controller.LogMovement(
                    seeded.Vehicle.VehicleId,
                    request);

                var unauthorized =
                    Assert.IsType<Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult>(result);

                Assert.Equal(
                    "Authenticated staff member could not be identified.",
                    unauthorized.Value);
            }
            finally
            {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        // NEW: a user without the CanLogMovements permission is refused (403).
        [Fact]
        public async Task LogMovement_ReturnsForbidWhenUserLacksPermission()
        {
            var (context, connection) = await CreateDatabaseAsync();

            try
            {
                await SeedStaffAsync(context);

                var seeded = await SeedVehicleAsync(context);

                var controller = new VehiclesController(context, BuildAuthService());

                SetUserWithStaffId(controller, 1, role: null);

                var request = new LogMovementRequestDto
                {
                    ToLocationId = seeded.Destination.LocationId
                };

                var result = await controller.LogMovement(
                    seeded.Vehicle.VehicleId,
                    request);

                Assert.IsType<ForbidResult>(result);

                var movementCount = await context.Movements.CountAsync();

                Assert.Equal(1, movementCount);
            }
            finally
            {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        // NEW: a brand-new vehicle with no movements can get its first movement.
        [Fact]
        public async Task LogMovement_AllowsFirstMovementForVehicleWithNoMovements()
        {
            var (context, connection) = await CreateDatabaseAsync();

            try
            {
                await SeedStaffAsync(context);

                var seeded = await SeedVehicleWithoutMovementAsync(context);

                var controller = new VehiclesController(context, BuildAuthService());

                SetUserWithStaffId(controller, 1);

                var request = new LogMovementRequestDto
                {
                    ToLocationId = seeded.Destination.LocationId,
                    Notes = "First placement"
                };

                var result = await controller.LogMovement(
                    seeded.Vehicle.VehicleId,
                    request);

                Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);

                var movement = await context.Movements.SingleAsync();

                Assert.Null(movement.FromLocationId);

                Assert.Equal(
                    seeded.Destination.LocationId,
                    movement.ToLocationId);

                Assert.Equal(1, movement.StaffId);
            }
            finally
            {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        // NEW: inactive (deleted) vehicles cannot be moved.
        [Fact]
        public async Task LogMovement_RejectsInactiveVehicle()
        {
            var (context, connection) = await CreateDatabaseAsync();

            try
            {
                await SeedStaffAsync(context);

                var seeded = await SeedVehicleAsync(context);

                seeded.Vehicle.IsActive = false;
                await context.SaveChangesAsync();

                var controller = new VehiclesController(context, BuildAuthService());

                SetUserWithStaffId(controller, 1);

                var request = new LogMovementRequestDto
                {
                    ToLocationId = seeded.Destination.LocationId
                };

                var result = await controller.LogMovement(
                    seeded.Vehicle.VehicleId,
                    request);

                var badRequest =
                    Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);

                Assert.Equal(
                    "Inactive vehicles cannot have movements logged.",
                    badRequest.Value);

                var movementCount = await context.Movements.CountAsync();

                Assert.Equal(1, movementCount);
            }
            finally
            {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }
    }
}