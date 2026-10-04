namespace APCVehicleTracker.Data.Models
{
    public class Vehicle
    {
        public int VehicleId { get; set; }

        public string Make { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        public int Year { get; set; }

        public string Registration { get; set; } = string.Empty;

        public string? Vin { get; set; }

        public string Status { get; set; } = string.Empty;
        //Deleting a vehicle sets this to false; an admin can set it back to true.
        public bool IsActive { get; set; } = true;
    }
}