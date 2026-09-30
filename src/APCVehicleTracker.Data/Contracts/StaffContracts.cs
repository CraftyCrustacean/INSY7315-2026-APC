using System.ComponentModel.DataAnnotations;

namespace APCVehicleTracker.Data.Contracts;

public record StaffDto(int StaffId, string FirstName, string LastName, string Email, string? Phone, string Role, bool IsActive, bool HasSignIn);

public class CreateStaffRequest
{
    [Required, StringLength(100)] public string FirstName { get; set; } = "";
    [Required, StringLength(100)] public string LastName { get; set; } = "";
    [Required, EmailAddress, StringLength(255)] public string Email { get; set; } = "";
    [StringLength(30)] public string? Phone { get; set; }
    [Required] public string Role { get; set; } = "";
}

public class UpdateStaffRequest
{
    [Required, StringLength(100)] public string FirstName { get; set; } = "";
    [Required, StringLength(100)] public string LastName { get; set; } = "";
    [StringLength(30)] public string? Phone { get; set; }
    [Required] public string Role { get; set; } = "";
}

public record TemporaryPasswordResponse(StaffDto Staff, string TemporaryPassword);