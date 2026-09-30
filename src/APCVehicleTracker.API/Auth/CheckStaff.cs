using APCVehicleTracker.Data.Auth;

namespace APCVehicleTracker.API.Auth;

public class CheckStaff
{
    private readonly RequestDelegate _next;

    public CheckStaff(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, StaffSigninService signIn)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            !await signIn.StaffClaimAsync(context.User, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }
}
