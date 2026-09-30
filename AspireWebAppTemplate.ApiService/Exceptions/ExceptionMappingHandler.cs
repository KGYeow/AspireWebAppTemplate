using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace AspireWebAppTemplate.ApiService.Exceptions;

/// <summary>
/// Maps service-layer exceptions that propagate past a controller action to HTTP status codes,
/// applying the mapping: <see cref="KeyNotFoundException"/> to 404, <see cref="InvalidOperationException"/>
/// to 400, <see cref="ArgumentException"/> (including its subtypes) to 400,
/// <see cref="UnauthorizedAccessException"/> to 403, <see cref="NotImplementedException"/> to 501, and
/// <see cref="TimeoutException"/> to 504. For a mapped exception the exception message is written as the
/// response body text; unmapped exceptions are left to the default handler, which produces a 500 with no
/// exception detail. The mapping is defined in a single switch that application authors extend to add
/// their own exception-to-status mappings.
/// </summary>
public sealed class ExceptionMappingHandler : IExceptionHandler
{
    #region Constructor

    /// <summary>
    /// Writes RFC 7807 problem-details responses using the ASP.NET Core problem-details service.
    /// </summary>
    private readonly IProblemDetailsService _problemDetailsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionMappingHandler"/> class.
    /// </summary>
    /// <param name="problemDetailsService">The problem-details service used to write the response.</param>
    public ExceptionMappingHandler(IProblemDetailsService problemDetailsService)
    {
        _problemDetailsService = problemDetailsService;
    }

    #endregion

    #region Exception Handling

    /// <summary>
    /// Attempts to map <paramref name="exception"/> to a 404, 400, 403, 501, or 504 response. Returns
    /// <c>true</c> when the exception is one of the mapped types and a response was written; returns
    /// <c>false</c> for any other exception so that the default handler produces a 500 with no detail.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="exception">The exception that propagated past the action.</param>
    /// <param name="cancellationToken">A token to observe cancellation.</param>
    /// <returns><c>true</c> if the exception was mapped and handled; otherwise <c>false</c>.</returns>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Single mapping location: extend this switch to add a new exception-to-status mapping.
        var statusCode = exception switch
        {
            KeyNotFoundException => StatusCodes.Status404NotFound,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            ArgumentException => StatusCodes.Status400BadRequest, // includes ArgumentNullException, ArgumentOutOfRangeException
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            NotImplementedException => StatusCodes.Status501NotImplemented,
            TimeoutException => StatusCodes.Status504GatewayTimeout,
            _ => (int?)null
        };

        // Unmapped exceptions fall through to the default handler, which produces a 500 with no detail.
        if (statusCode is null)
            return false;

        httpContext.Response.StatusCode = statusCode.Value;

        // Carry the exception message unmodified into the response body so mapped responses match
        // the text an inline NotFound(ex.Message) / BadRequest(ex.Message) call produces.
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = statusCode.Value,
                Detail = exception.Message
            }
        });
    }

    #endregion
}
