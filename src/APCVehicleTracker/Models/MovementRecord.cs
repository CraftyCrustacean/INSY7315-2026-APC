namespace APCVehicleTracker.Models
{
    public class MovementRecord
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public string FromLocation { get; set; } = string.Empty;
        public string ToLocation { get; set; } = string.Empty;
        public string MovedBy { get; set; } = string.Empty;
        public DateTime MovementDate { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}