using APCVehicleTracker.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace APCVehicleTracker.API.Filters;

public class StaffAdminErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not AdminException ex) return;

        var status = ex.Type switch
        {
            StaffAdminErrorType.NotFound => StatusCodes.Status404NotFound,
            StaffAdminErrorType.DuplicateEmail => StatusCodes.Status409Conflict,
            StaffAdminErrorType.NoSignInAccount => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        context.Result = new ObjectResult(new ProblemDetails { Status = status, Title = ex.Message }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
