namespace APCVehicleTracker.Data.Models
{
    public class Location
    {
        public int LocationId { get; set; }

        public string LocationName { get; set; } = string.Empty;

        public string LocationType { get; set; } = string.Empty;

        public string? Address { get; set; }
    }
}