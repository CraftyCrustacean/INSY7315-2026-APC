using System.ComponentModel.DataAnnotations;

namespace APCVehicleTracker.Models
{
    public class VehicleFormViewModel
    {
        public int VehicleId { get; set; }

        [Required, StringLength(50)]
        public string Make { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Model { get; set; } = string.Empty;

        [Range(1900, 2100)]
        public int Year { get; set; } = DateTime.UtcNow.Year;

        [Required, StringLength(20)]
        public string Registration { get; set; } = string.Empty;

        [Display(Name = "VIN"), StringLength(17, MinimumLength = 11)]
        [RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "VIN may only contain letters and digits.")]
        public string? Vin { get; set; }

        [Required]
        public string Status { get; set; } = "Available";

        // Create only (validated in the controller). Not editable afterwards - use Log Movement.
        [Display(Name = "Starting location")]
        public int StartingLocationId { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        // Edit only (display)
        public string? CurrentLocation { get; set; }
        public bool IsActive { get; set; } = true;
        public List<VehicleImageViewModel> Images { get; set; } = new();

        public List<string> AvailableStatuses { get; set; } = new();
        public List<LocationViewModel> AvailableLocations { get; set; } = new();
    }

    public class VehicleImageViewModel
    {
        public int ImageId { get; set; }
        public string BlobName { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsPrimary { get; set; }
    }

    public record DeleteVehicleModalModel(int VehicleId, string Description);
}
