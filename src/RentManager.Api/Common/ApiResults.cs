using Microsoft.AspNetCore.Mvc;

namespace RentManager.Api.Common;

// Plain-string error bodies come back as text/plain, which Angular's
// HttpClient cannot parse. Everything returns JSON instead.
public static class ApiResults
{
    public static IActionResult Invalid(string message)
        => new BadRequestObjectResult(new ApiErrorResponse(message));

    public static IActionResult Missing(string message)
        => new NotFoundObjectResult(new ApiErrorResponse(message));

    public static IActionResult Duplicate(string message)
        => new ConflictObjectResult(new ApiErrorResponse(message));

    public static IActionResult Blocked(string message)
        => new ObjectResult(new ApiErrorResponse(message))
        {
            StatusCode = StatusCodes.Status409Conflict
        };
}

public record ApiErrorResponse(string Message);
