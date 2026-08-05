using Microsoft.Extensions.Configuration;
namespace AspireWebAppTemplate.Infrastructure.Handlers;

/// <summary>
/// Delegating handler that attaches the <c>X-Internal-Api-Key</c> header to all outbound
/// requests from the API service to the Web project's internal endpoints.
/// </summary>
/// <remarks>
/// Reads the API key from the <c>INTERNAL_API_KEY</c> configuration value (injected by Aspire
/// as an environment variable). If the key is not configured, the header is omitted and the
/// request proceeds without authentication (the receiving endpoint will reject it with 401).
/// </remarks>
public class InternalApiKeyDelegatingHandler : DelegatingHandler
{
    #region Constructor

    /// <summary>
    /// The HTTP header name used for internal service-to-service authentication.
    /// </summary>
    private const string ApiKeyHeaderName = "X-Internal-Api-Key";

    /// <summary>
    /// The application configuration for reading the internal API key value.
    /// </summary>
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalApiKeyDelegatingHandler"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration for reading the internal API key.</param>
    public InternalApiKeyDelegatingHandler(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    #endregion

    /// <summary>
    /// Adds the internal API key header to the outbound request before forwarding it.
    /// </summary>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["INTERNAL_API_KEY"];

        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.TryAddWithoutValidation(ApiKeyHeaderName, apiKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
