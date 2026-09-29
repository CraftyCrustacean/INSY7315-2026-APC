namespace APCVehicleTracker.Models
{
    public class VehicleSearchViewModel
    {
        public string? Make { get; set; }

        public string? Model { get; set; }

        public int? YearFrom { get; set; }

        public int? YearTo { get; set; }

        public List<string> Status { get; set; } = new();

        public List<int> Location { get; set; } = new();

        public bool IncludeSold { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 25;

        public int TotalCount { get; set; }

        public List<VehicleSearchResultViewModel> Vehicles { get; set; } = new();

        public List<string> AvailableMakes { get; set; } = new();

        public List<string> AvailableModels { get; set; } = new();

        public List<string> AvailableStatuses { get; set; } = new();

        public List<LocationViewModel> AvailableLocations { get; set; } = new();
    }

    public class VehicleSearchResultViewModel
    {
        public int VehicleId { get; set; }

        public string? PrimaryImage { get; set; }

        public string Make { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        public int Year { get; set; }

        public string Registration { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string? CurrentLocation { get; set; }

        public int? DaysAtCurrentLocation { get; set; }
    }

    public class LocationViewModel
    {
        public int LocationId { get; set; }

        public string LocationName { get; set; } = string.Empty;
    }
}