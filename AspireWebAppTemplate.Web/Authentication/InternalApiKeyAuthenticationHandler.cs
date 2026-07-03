using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AspireWebAppTemplate.Web.Authentication;

/// <summary>
/// Authentication handler that validates the <c>X-Internal-Api-Key</c> header
/// for service-to-service callbacks from the API project.
/// </summary>
/// <remarks>
/// Used exclusively by the internal notification callback endpoint. Validates that the
/// incoming request contains a header matching the shared secret configured via the
/// <c>INTERNAL_API_KEY</c> environment variable (provided by Aspire parameters).
/// </remarks>
public class InternalApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>
    /// The authentication scheme name used for internal API key validation.
    /// </summary>
    public const string SchemeName = "InternalApiKey";

    /// <summary>
    /// The HTTP header name containing the internal API key.
    /// </summary>
    private const string ApiKeyHeaderName = "X-Internal-Api-Key";

    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalApiKeyAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="options">The monitor for authentication scheme options.</param>
    /// <param name="logger">The logger factory for creating loggers.</param>
    /// <param name="encoder">The URL encoder.</param>
    /// <param name="configuration">The application configuration for reading the expected API key.</param>
    public InternalApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Validates the <c>X-Internal-Api-Key</c> header against the configured expected value.
    /// Returns a successful result with a service identity claim if the key matches.
    /// </summary>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expectedApiKey = _configuration["INTERNAL_API_KEY"];

        if (string.IsNullOrEmpty(expectedApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Internal API key is not configured on the server."));
        }

        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var headerValue))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Missing {ApiKeyHeaderName} header."));
        }

        if (!string.Equals(headerValue, expectedApiKey, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        // Create a minimal identity for the internal service caller.
        var claims = new[] { new Claim(ClaimTypes.Name, "InternalApiService") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
