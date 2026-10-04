namespace APCVehicleTracker.Controllers;
using APCVehicleTracker.Data.Auth;
using APCVehicleTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Net.Http.Json;




[Authorize(Policy = AuthPolicies.CanViewReports)]
public class ReportsController : Controller
    {
        private readonly IHttpClientFactory _factory;
        public ReportsController(IHttpClientFactory factory) => _factory = factory;

        private HttpClient Api() => _factory.CreateClient("Api");

        private static string StuckQuery(int? locationId, int days) =>
            $"days={Math.Max(0, days)}" + (locationId.HasValue ? $"&locationId={locationId}" : "");

        public async Task<IActionResult> Index(int? locationId, int days = 2)
        {
            days = Math.Max(0, days);
            var api = Api();

            var model = new ReportsViewModel
            {
                LocationId = locationId,
                Days = days,
                Summary = await api.GetFromJsonAsync<SummaryDto>($"api/reports/summary?stuckDays={days}") ?? new(),
                Stuck = await api.GetFromJsonAsync<List<StuckDto>>($"api/reports/stuck?{StuckQuery(locationId, days)}") ?? new(),
                Aging = await api.GetFromJsonAsync<List<StockAgingDto>>("api/reports/stock-aging") ?? new(),
            };

            var locs = await api.GetFromJsonAsync<List<LocationDto>>("api/reports/locations") ?? new();
            model.Locations = locs.Select(l => new SelectListItem(
                l.LocationName, l.LocationId.ToString(), l.LocationId == locationId)).ToList();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportStuck(int? locationId, int days = 2)
        {
            var res = await Api().GetAsync($"api/reports/stuck/export?{StuckQuery(locationId, days)}");
            res.EnsureSuccessStatusCode();
            return File(await res.Content.ReadAsByteArrayAsync(), "text/csv", "stuck-vehicles.csv");
        }

        [HttpGet]
        public async Task<IActionResult> ExportStockAging()
        {
            var res = await Api().GetAsync("api/reports/stock-aging/export");
            res.EnsureSuccessStatusCode();
            return File(await res.Content.ReadAsByteArrayAsync(), "text/csv", "stock-aging.csv");
        }
    }
