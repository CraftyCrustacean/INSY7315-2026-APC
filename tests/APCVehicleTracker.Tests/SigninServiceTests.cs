using System.Security.Claims;
using APCVehicleTracker.Auth;
using APCVehicleTracker.Models;
using APCVehicleTracker.Services;

public class StaffSignInServiceTests
{

    // Set up dummy data for the tests to run against
    private class FakeLookup : IStaffLookup
    {
        private readonly StaffInfo? _staff;
        public FakeLookup(StaffInfo? staff) => _staff = staff;
        public Task<StaffInfo?> FindByObjectIdAsync(string id, CancellationToken ct = default)
            => Task.FromResult(_staff?.EntraObjectId == id ? _staff : null);
    }

    private static ClaimsPrincipal UserWithOid(string oid) =>
        new(new ClaimsIdentity(new[] { new Claim("oid", oid) }, "test", "name", "roles"));

    private static StaffInfo Staff(bool active = true) =>
        new(1, "garbage", "email@example.com", "a", "b", StaffRoles.Admin, active);
    
    // Test fetched role matches expectation
    [Fact]
    public async Task Active_staff_gets_role_claim()
    {
        var user = UserWithOid("garbage");
        var ok = await new StaffSigninService(new FakeLookup(Staff())).StaffClaimAsync(user);
        Assert.True(ok);
        Assert.True(user.IsInRole(StaffRoles.Admin));
    }

    // Test non existant user is refused
    [Fact]
    public async Task Unknown_user_is_refused()
        => Assert.False(await new StaffSigninService(new FakeLookup(null))
                                 .StaffClaimAsync(UserWithOid("garbage")));

    // Test inactive user is refused
    [Fact]
    public async Task Inactive_staff_is_refused()
        => Assert.False(await new StaffSigninService(new FakeLookup(Staff(active: false)))
                                 .StaffClaimAsync(UserWithOid("garbage")));
}