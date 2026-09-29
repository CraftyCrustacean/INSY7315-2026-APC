namespace APCVehicleTracker.API.DTOs
{
    public class MovementHistoryDto
    {
        public int MovementId { get; set; }

        public DateTime MovementDateTime { get; set; }

        public string? FromLocation { get; set; }

        public string ToLocation { get; set; } = string.Empty;

        public string? MovedBy { get; set; }

        public string? Notes { get; set; }
    }
}