using APCVehicleTracker.Services;
using Microsoft.Identity.Web;
using System.Security.Claims;

namespace APCVehicleTracker.Auth;

public class StaffSigninService
{

    public const string StaffIdClaim = "staff_id";
    private readonly IStaffLookup _lookup;

    public StaffSigninService(IStaffLookup lookup) => _lookup = lookup;

    public async Task<bool> StaffClaimAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var objectId = principal.GetObjectId();
        if (objectId == null || principal.Identity is not ClaimsIdentity identity) return false;

        var staff = await _lookup.FindByObjectIdAsync(objectId, cancellationToken);
        if (staff is null || !staff.IsActive) return false;

        identity.AddClaim(new Claim(identity.RoleClaimType, staff.Role));
        identity.AddClaim(new Claim(StaffIdClaim, staff.StaffId.ToString()));
        return true;
    }

}

