using Core;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Infrastructure;

/// <summary>
/// Обробник доменних винятків для додаткового завдання 3:
/// Мапінг DomainException -> 409 Conflict у форматі ProblemDetails (RFC 9457).
/// </summary>
public sealed class DomainExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainEx)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Domain business rule violation",
                Detail = domainEx.Message,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10"
            },
            Exception = exception
        });
    }
}
