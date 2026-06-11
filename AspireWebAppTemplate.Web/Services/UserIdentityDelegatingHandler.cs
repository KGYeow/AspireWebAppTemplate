using System.Security.Claims;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// Forwards the authenticated user's identity from the Web project's cookie session
/// to the API service via custom headers on outbound HTTP requests.
/// This enables service-to-service identity propagation in the Aspire architecture.
/// </summary>
public class UserIdentityDelegatingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var user = httpContext?.User;

        if (user?.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = user.FindFirstValue(ClaimTypes.Name);
            var email = user.FindFirstValue(ClaimTypes.Email);
            var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value);

            if (!string.IsNullOrEmpty(userId))
                request.Headers.TryAddWithoutValidation("X-User-Id", userId);

            if (!string.IsNullOrEmpty(userName))
                request.Headers.TryAddWithoutValidation("X-User-Name", userName);

            if (!string.IsNullOrEmpty(email))
                request.Headers.TryAddWithoutValidation("X-User-Email", email);

            var roleList = string.Join(",", roles);
            if (!string.IsNullOrEmpty(roleList))
                request.Headers.TryAddWithoutValidation("X-User-Roles", roleList);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
