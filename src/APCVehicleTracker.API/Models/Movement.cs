namespace APCVehicleTracker.API.Models
{
    public class Movement
    {
        public int MovementId { get; set; }

        public int VehicleId { get; set; }

        public int? FromLocationId { get; set; }

        public int ToLocationId { get; set; }

        public int? StaffId { get; set; }

        public DateTime MovementDateTime { get; set; }

        public string? Notes { get; set; }
    }
}