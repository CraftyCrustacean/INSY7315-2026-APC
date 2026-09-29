using System.ComponentModel.DataAnnotations.Schema;

namespace APCVehicleTracker.Data.Models
{
    public class Staff
    {
        public int StaffId { get; set; }

        public string EntraObjectId { get; set; } = string.Empty;

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? Phone { get; set; }

        public string Role { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}