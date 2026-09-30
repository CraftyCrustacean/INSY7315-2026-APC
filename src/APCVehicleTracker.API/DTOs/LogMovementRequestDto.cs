using System.ComponentModel.DataAnnotations;

namespace APCVehicleTracker.API.DTOs
{
    public class LogMovementRequestDto
    {
        [Range(1, int.MaxValue)]
        public int ToLocationId { get; set; }

        public string? NewStatus { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}