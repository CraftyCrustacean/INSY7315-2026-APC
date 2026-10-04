namespace APCVehicleTracker.Models;
using Microsoft.AspNetCore.Mvc.Rendering;



    public class SummaryDto
    {
        public int TotalInStock { get; set; }
        public int TotalInWorkshop { get; set; }
        public int StuckVehicles { get; set; }
    }

    public class StuckDto
    {
        public int VehicleId { get; set; }
        public string Registration { get; set; } = "";
        public string Make { get; set; } = "";
        public string Model { get; set; } = "";
        public string Location { get; set; } = "";
        public int DaysAtLocation { get; set; }
    }

    public class StockAgingDto
    {
        public int VehicleId { get; set; }
        public string Registration { get; set; } = "";
        public string Make { get; set; } = "";
        public string Model { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime? StartDate { get; set; }
        public int DaysInStock { get; set; }
    }

    public class LocationDto
    {
        public int LocationId { get; set; }
        public string LocationName { get; set; } = "";
    }

    public class ReportsViewModel
    {
        public SummaryDto Summary { get; set; } = new();
        public List<StuckDto> Stuck { get; set; } = new();
        public List<StockAgingDto> Aging { get; set; } = new();
        public List<SelectListItem> Locations { get; set; } = new();
        public int? LocationId { get; set; }
        public int Days { get; set; } = 2;
    }
