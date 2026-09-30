using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net;

namespace APCVehicleTracker.Auth;

public class AuthFailureFilter : IExceptionFilter
{

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not HttpRequestException exception) return;

        if(exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            context.Result = new SignOutResult(
                new[] { CookieAuthenticationDefaults.AuthenticationScheme }, new AuthenticationProperties { RedirectUri = "/Home/NotAuthorised" });
            context.ExceptionHandled = true;
        }
        else if (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Account", new { area = "MicrosoftIdentity" });
            context.ExceptionHandled = true;
        }
    }
}
