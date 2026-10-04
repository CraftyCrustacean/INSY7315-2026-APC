using System.ComponentModel.DataAnnotations;

namespace APCVehicleTracker.API.DTOs
{
    public static class VehicleRules
    {
        public static readonly string[] Statuses =
            { "Available", "Reserved", "In Transit", "In Workshop", "Sold" };

        public const int MaxImages = 5;
    }

    public class CreateVehicleRequestDto
    {
        [Required, StringLength(50)]
        public string Make { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Model { get; set; } = string.Empty;

        [Range(1900, 2100)]
        public int Year { get; set; }

        [Required, StringLength(20)]
        public string Registration { get; set; } = string.Empty;

        [StringLength(17, MinimumLength = 11)]
        [RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "VIN may only contain letters and digits.")]
        public string? Vin { get; set; }

        [Required]
        public string Status { get; set; } = string.Empty;

        /// <summary>Required. Saved as the vehicle's first movement.</summary>
        [Range(1, int.MaxValue, ErrorMessage = "A starting location is required.")]
        public int StartingLocationId { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    // Location is intentionally absent: it can only change by logging a movement.
    public class UpdateVehicleRequestDto
    {
        [Required, StringLength(50)]
        public string Make { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Model { get; set; } = string.Empty;

        [Range(1900, 2100)]
        public int Year { get; set; }

        [Required, StringLength(20)]
        public string Registration { get; set; } = string.Empty;

        [StringLength(17, MinimumLength = 11)]
        [RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "VIN may only contain letters and digits.")]
        public string? Vin { get; set; }

        [Required]
        public string Status { get; set; } = string.Empty;
    }

    public class VehicleImageDto
    {
        public int ImageId { get; set; }

        public string BlobName { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public bool IsPrimary { get; set; }
    }

    public class ReorderImagesRequestDto
    {
        [Required]
        public List<int> ImageIds { get; set; } = new();
    }
}
