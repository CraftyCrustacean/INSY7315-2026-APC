using APCVehicleTracker.API.Controllers;
using APCVehicleTracker.API.DTOs;
using APCVehicleTracker.Data;
using APCVehicleTracker.Data.Auth;
using APCVehicleTracker.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace APCVehicleTracker.Tests
{
    public class VehicleLocationFilterTests
    {
        private const int Showroom = 1;
        private const int Workshop = 2;

        private static async Task<(VehiclesController Controller, ApplicationDbContext Context, SqliteConnection Connection)>
            CreateControllerAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();

            context.Locations.AddRange(
                new Location { LocationId = Showroom, LocationName = "Showroom", LocationType = "Showroom" },
                new Location { LocationId = Workshop, LocationName = "Workshop", LocationType = "Workshop" });

            var start = DateTime.UtcNow.AddDays(-10);
            var movementId = 1;

            for (var id = 1; id <= 61; id++)
            {
                context.Vehicles.Add(new Vehicle
                {
                    VehicleId = id,
                    Make = "Toyota",
                    Model = "Corolla",
                    Year = 2020,
                    Registration = $"REG{id:000}",
                    Status = "Available"
                });

                if (id == 61) continue;

                context.Movements.Add(new Movement
                {
                    MovementId = movementId++, VehicleId = id, ToLocationId = Showroom, MovementDateTime = start
                });

                if (id % 2 == 1)
                {
                    context.Movements.Add(new Movement
                    {
                        MovementId = movementId++, VehicleId = id, FromLocationId = Showroom, ToLocationId = Workshop,
                        MovementDateTime = start.AddDays(1)
                    });
                }
            }

            await context.SaveChangesAsync();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAppAuthorization();
            var authService = services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();

            var controller = new VehiclesController(context, authService)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            return (controller, context, connection);
        }

        private static async Task<VehicleSearchResponseDto> SearchAsync(int[] location, int page = 1)
        {
            var (controller, context, connection) = await CreateControllerAsync();
            await using (context)
            await using (connection)
            {
                var result = await controller.SearchVehicles(location: location, page: page);
                return Assert.IsType<VehicleSearchResponseDto>(Assert.IsType<OkObjectResult>(result).Value);
            }
        }

        [Fact]
        public async Task LocationFilter_CountsOnlyMatchingVehicles()
        {
            var response = await SearchAsync(new[] { Showroom });

            Assert.Equal(30, response.TotalCount);
        }

        [Fact]
        public async Task LocationFilter_FillsPagesBeforePaging()
        {
            var page1 = await SearchAsync(new[] { Showroom }, page: 1);
            var page2 = await SearchAsync(new[] { Showroom }, page: 2);

            Assert.Equal(25, page1.Vehicles.Count);
            Assert.Equal(5, page2.Vehicles.Count);
            Assert.All(page1.Vehicles.Concat(page2.Vehicles), v => Assert.Equal("Showroom", v.CurrentLocation));
        }

        [Fact]
        public async Task LocationFilter_UsesLatestMovementOnly()
        {
            var showroom = await SearchAsync(new[] { Showroom });
            Assert.All(showroom.Vehicles, v => Assert.True(v.VehicleId % 2 == 0));

            var workshop = await SearchAsync(new[] { Workshop });
            Assert.Equal(30, workshop.TotalCount);
            Assert.All(workshop.Vehicles, v => Assert.True(v.VehicleId % 2 == 1));
        }

        [Fact]
        public async Task LocationFilter_MultipleLocations_ExcludesVehiclesWithoutMovements()
        {
            var response = await SearchAsync(new[] { Showroom, Workshop });

            Assert.Equal(60, response.TotalCount);
            Assert.DoesNotContain(response.Vehicles, v => v.VehicleId == 61);
        }
    }
}
