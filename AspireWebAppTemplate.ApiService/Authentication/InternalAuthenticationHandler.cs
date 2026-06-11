using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AspireWebAppTemplate.ApiService.Authentication;

/// <summary>
/// Custom authentication handler that reads user identity from internal service-to-service headers.
/// The Web frontend forwards authenticated user claims via X-User-* headers.
/// This handler trusts those headers because the API service is not publicly accessible —
/// it is only reachable via Aspire's internal service discovery.
/// </summary>
public class InternalAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Internal";

    public InternalAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers["X-User-Id"].FirstOrDefault();

        if (string.IsNullOrEmpty(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId)
        };

        var userName = Request.Headers["X-User-Name"].FirstOrDefault();
        if (!string.IsNullOrEmpty(userName))
            claims.Add(new Claim(ClaimTypes.Name, userName));

        var email = Request.Headers["X-User-Email"].FirstOrDefault();
        if (!string.IsNullOrEmpty(email))
            claims.Add(new Claim(ClaimTypes.Email, email));

        var roles = Request.Headers["X-User-Roles"].FirstOrDefault();
        if (!string.IsNullOrEmpty(roles))
        {
            foreach (var role in roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
