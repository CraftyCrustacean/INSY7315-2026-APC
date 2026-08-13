namespace APCVehicleTracker.Models
{
    public class Vehicle
    {
        public int Id { get; set; }
        public string RegistrationNumber { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Year { get; set; }
        public string VinNumber { get; set; } = string.Empty;
        public string CurrentLocation { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // e.g. In Stock, Sold, In Workshop
        public DateTime LastMoved { get; set; }
    }
}