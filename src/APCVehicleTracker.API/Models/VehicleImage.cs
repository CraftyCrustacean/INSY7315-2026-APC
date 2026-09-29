namespace APCVehicleTracker.API.Models
{
    public class VehicleImage
    {
        public int VehicleImageId { get; set; }

        public int VehicleId { get; set; }

        public string ImageUrl { get; set; } = string.Empty;

        public DateTime UploadedDate { get; set; }
    }
}