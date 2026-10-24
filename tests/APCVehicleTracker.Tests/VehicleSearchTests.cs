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
    public class VehicleSearchTests
    {
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

            context.Vehicles.AddRange(
                new Vehicle { VehicleId = 1, Make = "Honda", Model = "Accord", Year = 2018, Registration = "CA 123-456", Vin = "1HGCM82633A004352", Status = "Available" },
                new Vehicle { VehicleId = 2, Make = "Toyota", Model = "Hilux", Year = 2021, Registration = "GP77XYZ", Vin = null, Status = "Available" },
                new Vehicle { VehicleId = 3, Make = "VW", Model = "Golf", Year = 2019, Registration = "WC 9988", Vin = "WVWZZZ1JZXW000001", Status = "In Workshop" },
                new Vehicle { VehicleId = 4, Make = "Ford", Model = "Ranger", Year = 2017, Registration = "CA 555", Vin = "1FTYR10D17PA00001", Status = "Sold" });
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

        private static async Task<List<int>> SearchIdsAsync(string? search, bool includeSold = false, string? make = null)
        {
            var (controller, context, connection) = await CreateControllerAsync();
            await using (context)
            await using (connection)
            {
                var result = await controller.SearchVehicles(search: search, make: make, includeSold: includeSold);
                var response = Assert.IsType<VehicleSearchResponseDto>(Assert.IsType<OkObjectResult>(result).Value);
                return response.Vehicles.Select(v => v.VehicleId).OrderBy(id => id).ToList();
            }
        }

        [Theory]
        [InlineData("CA 123-456")]
        [InlineData("ca123")]
        [InlineData("3-45")]
        [InlineData("23 4")]
        public async Task Search_MatchesPartOfRegistration(string search)
        {
            Assert.Equal(new[] { 1 }, await SearchIdsAsync(search));
        }

        [Theory]
        [InlineData("1HGCM82633A004352")]
        [InlineData("a0043")]
        public async Task Search_MatchesPartOfVin(string search)
        {
            Assert.Equal(new[] { 1 }, await SearchIdsAsync(search));
        }

        [Fact]
        public async Task Search_MatchesVehicleWithoutVinByRegistration()
        {
            Assert.Equal(new[] { 2 }, await SearchIdsAsync("77x"));
        }

        [Fact]
        public async Task Search_NoMatch_ReturnsEmpty()
        {
            Assert.Empty(await SearchIdsAsync("NOPE999"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(" - ")]
        public async Task Search_Blank_DoesNotFilter(string? search)
        {
            Assert.Equal(new[] { 1, 2, 3 }, await SearchIdsAsync(search));
        }

        [Fact]
        public async Task Search_CombinesWithIncludeSold()
        {
            Assert.Equal(new[] { 1 }, await SearchIdsAsync("CA"));
            Assert.Equal(new[] { 1, 4 }, await SearchIdsAsync("CA", includeSold: true));
        }

        [Fact]
        public async Task Search_CombinesWithOtherFilters()
        {
            Assert.Equal(new[] { 3 }, await SearchIdsAsync("W", make: "VW"));
        }
    }
}
