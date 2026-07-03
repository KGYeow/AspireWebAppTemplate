// Feature: realtime-notifications, Property 8: API key authentication validates correctly
using System.Text.Encodings.Web;
using AspireWebAppTemplate.Web.Authentication;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Property-based tests verifying that the InternalApiKeyAuthenticationHandler correctly
/// authenticates requests based on the X-Internal-Api-Key header matching the configured
/// expected API key value.
/// </summary>
/// <remarks>
/// **Validates: Requirements 5.4, 5.5**
/// </remarks>
public class InternalApiKeyAuthPropertyTests
{
    /// <summary>
    /// Creates an <see cref="InternalApiKeyAuthenticationHandler"/> configured with the specified
    /// expected API key and an HTTP context containing the specified header value.
    /// </summary>
    /// <param name="expectedKey">The API key value configured on the server (INTERNAL_API_KEY).</param>
    /// <param name="headerValue">The value to set in the X-Internal-Api-Key header, or null to omit the header.</param>
    /// <returns>The configured handler ready for authentication.</returns>
    private static async Task<InternalApiKeyAuthenticationHandler> CreateHandler(string? expectedKey, string? headerValue)
    {
        var configData = new Dictionary<string, string?>();
        if (expectedKey != null)
        {
            configData["INTERNAL_API_KEY"] = expectedKey;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var options = new AuthenticationSchemeOptions();
        var optionsMonitor = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        optionsMonitor.Setup(o => o.Get(InternalApiKeyAuthenticationHandler.SchemeName))
            .Returns(options);

        var loggerFactory = NullLoggerFactory.Instance;
        var encoder = UrlEncoder.Default;

        var handler = new InternalApiKeyAuthenticationHandler(
            optionsMonitor.Object,
            loggerFactory,
            encoder,
            configuration);

        var httpContext = new DefaultHttpContext();
        if (headerValue != null)
        {
            httpContext.Request.Headers["X-Internal-Api-Key"] = headerValue;
        }

        var scheme = new AuthenticationScheme(
            InternalApiKeyAuthenticationHandler.SchemeName,
            displayName: null,
            handlerType: typeof(InternalApiKeyAuthenticationHandler));

        await handler.InitializeAsync(scheme, httpContext);

        return handler;
    }

    /// <summary>
    /// Property: For any non-empty configured API key K and a request header value V equal to K,
    /// the handler SHALL authenticate successfully.
    /// **Validates: Requirements 5.4, 5.5**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property MatchingKey_AuthenticatesSuccessfully()
    {
        // Generate non-empty, non-whitespace API key strings
        var keyGen = Gen.Elements(
            "my-secret-key",
            "abc123",
            "X9f!@#zQ",
            "a-very-long-api-key-that-is-still-valid-12345",
            "simple");

        return Prop.ForAll(
            Arb.From(keyGen),
            (string apiKey) =>
            {
                var handler = CreateHandler(apiKey, apiKey).GetAwaiter().GetResult();
                var result = handler.AuthenticateAsync().GetAwaiter().GetResult();

                return result.Succeeded.Label(
                    $"Expected authentication to succeed when header matches configured key '{apiKey}', " +
                    $"but got Succeeded={result.Succeeded}, Failure={result.Failure?.Message}");
            });
    }

    /// <summary>
    /// Property: For any non-empty configured API key K and a request header value V that does NOT
    /// equal K, the handler SHALL fail authentication.
    /// **Validates: Requirements 5.4, 5.5**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property NonMatchingKey_FailsAuthentication()
    {
        // Generate pairs of distinct non-empty strings for expected key and header value
        var expectedKeyGen = Gen.Elements(
            "my-secret-key",
            "abc123",
            "X9f!@#zQ");

        var headerValueGen = Gen.Elements(
            "different-key",
            "wrong-value",
            "not-the-right-key");

        var gen = expectedKeyGen.SelectMany(expected =>
            headerValueGen
                .Where(header => header != expected)
                .Select(header => (expected, header)));

        return Prop.ForAll(
            Arb.From(gen),
            ((string expected, string header) pair) =>
            {
                var (expectedKey, headerValue) = pair;
                var handler = CreateHandler(expectedKey, headerValue).GetAwaiter().GetResult();
                var result = handler.AuthenticateAsync().GetAwaiter().GetResult();

                return (!result.Succeeded).Label(
                    $"Expected authentication to fail when header='{headerValue}' " +
                    $"does not match configured key='{expectedKey}', but got Succeeded={result.Succeeded}");
            });
    }

    /// <summary>
    /// Property: For any non-empty configured API key K and a request without the X-Internal-Api-Key
    /// header, the handler SHALL fail authentication.
    /// **Validates: Requirements 5.4, 5.5**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property MissingHeader_FailsAuthentication()
    {
        // Generate non-empty API key strings for the configured expected value
        var keyGen = Gen.Elements(
            "my-secret-key",
            "abc123",
            "X9f!@#zQ",
            "another-key",
            "test-key-value");

        return Prop.ForAll(
            Arb.From(keyGen),
            (string apiKey) =>
            {
                // Pass null for headerValue to omit the header entirely
                var handler = CreateHandler(apiKey, null).GetAwaiter().GetResult();
                var result = handler.AuthenticateAsync().GetAwaiter().GetResult();

                return (!result.Succeeded).Label(
                    $"Expected authentication to fail when header is missing " +
                    $"(configured key='{apiKey}'), but got Succeeded={result.Succeeded}");
            });
    }
}
