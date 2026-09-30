using System.Security.Claims;
using APCVehicleTracker.Data.Auth;
using Microsoft.AspNetCore.Authentication;

namespace APCVehicleTracker.API.Services
{
    public class StaffClaimsTransformation : IClaimsTransformation
    {
        private readonly IStaffLookup _staffLookup;

        public StaffClaimsTransformation(IStaffLookup staffLookup)
        {
            _staffLookup = staffLookup;
        }

        public async Task<ClaimsPrincipal> TransformAsync(
            ClaimsPrincipal principal)
        {
            if (principal.Identity is not ClaimsIdentity identity)
            {
                return principal;
            }

            var objectId =
                principal.FindFirst("oid")?.Value
                ?? principal.FindFirst(
                    "http://schemas.microsoft.com/identity/claims/objectidentifier")
                    ?.Value;

            if (string.IsNullOrWhiteSpace(objectId))
            {
                return principal;
            }

            var staff =
                await _staffLookup.FindByObjectIdAsync(objectId);

            if (staff is null || !staff.IsActive)
            {
                return principal;
            }

            if (!identity.HasClaim(
                    claim => claim.Type == identity.RoleClaimType))
            {
                identity.AddClaim(
                    new Claim(
                        identity.RoleClaimType,
                        staff.Role));
            }

            if (!identity.HasClaim(
                    claim => claim.Type == StaffSigninService.StaffIdClaim))
            {
                identity.AddClaim(
                    new Claim(
                        StaffSigninService.StaffIdClaim,
                        staff.StaffId.ToString()));
            }

            return principal;
        }
    }
}