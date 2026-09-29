namespace APCVehicleTracker.API.DTOs
{
    public class VehicleSearchResultDto
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
}