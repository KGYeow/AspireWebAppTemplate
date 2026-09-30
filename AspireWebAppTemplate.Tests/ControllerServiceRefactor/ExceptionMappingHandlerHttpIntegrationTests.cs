// Feature: reusable-api-infrastructure, Task 1.5: Integration test for the wired exception handler over real HTTP
using System.Text.Json;
using AspireWebAppTemplate.ApiService.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AspireWebAppTemplate.Tests.ControllerServiceRefactor;

/// <summary>
/// Integration tests that exercise <see cref="ExceptionMappingHandler"/> over real HTTP through the
/// same middleware wiring the ApiService uses: <c>AddProblemDetails()</c> +
/// <c>AddExceptionHandler&lt;ExceptionMappingHandler&gt;()</c> + <c>UseExceptionHandler()</c>.
/// A minimal self-hosted app maps test-only endpoints that throw each exception type, and the tests
/// assert the resulting HTTP status code and response body across the full pipeline.
/// </summary>
/// <remarks>
/// The test endpoints live entirely in this test host and are never added to the production ApiService.
/// They exist only to make service-layer exceptions propagate past a request handler so the wired
/// <see cref="ExceptionMappingHandler"/> observes them, mirroring how a controller action lets an
/// unhandled exception surface.
/// **Validates: Requirements 1.1, 4.1, 4.2, 4.4, 4.6**
/// </remarks>
public sealed class ExceptionMappingHandlerHttpIntegrationTests : IAsyncLifetime
{
    #region Fields

    /// <summary>
    /// The self-hosted minimal web application wired with the ApiService exception-handling pipeline.
    /// </summary>
    private WebApplication _app = default!;

    /// <summary>
    /// An HTTP client whose base address targets the self-hosted app, used to drive real requests.
    /// </summary>
    private HttpClient _client = default!;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Builds and starts the self-hosted app on a dynamically assigned loopback port with the exact
    /// exception-handling wiring under test, and maps the test-only throwing endpoints.
    /// </summary>
    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();

        // Mirror the ApiService exception-handling wiring exactly.
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ExceptionMappingHandler>();

        _app = builder.Build();

        // Bind to a dynamically assigned free loopback port so parallel test runs never collide.
        _app.Urls.Add("http://127.0.0.1:0");

        // The registered IExceptionHandler runs here when an endpoint lets an exception propagate.
        _app.UseExceptionHandler();

        // Test-only endpoints: each throws so the wired handler observes a propagated exception.
        // The explicit string return type binds the delegate to the minimal-API handler overload
        // (rather than the terminal RequestDelegate overload) and forces query-string binding of message.
        _app.MapGet("/throw/key-not-found",
            (string message) => ThrowFor(new KeyNotFoundException(message)));
        _app.MapGet("/throw/invalid-operation",
            (string message) => ThrowFor(new InvalidOperationException(message)));
        _app.MapGet("/throw/argument",
            (string message) => ThrowFor(new ArgumentException(message)));
        _app.MapGet("/throw/unmapped",
            (string message) => ThrowFor(new FormatException(message)));

        await _app.StartAsync();

        // Resolve the actual bound address (the ":0" port is replaced with the assigned port).
        var address = _app.Urls.First();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    /// <summary>
    /// Disposes the HTTP client and stops the self-hosted app.
    /// </summary>
    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    #endregion

    #region Mapped Exception Tests

    /// <summary>
    /// Verifies a propagated <see cref="KeyNotFoundException"/> yields HTTP 404 with the exception
    /// message preserved in the problem-details <c>detail</c> field.
    /// **Validates: Requirements 1.1, 4.1, 4.2**
    /// </summary>
    [Fact]
    public async Task KeyNotFoundException_ProducesNotFoundWithMessage()
    {
        const string message = "Role 'abc' was not found.";

        var response = await _client.GetAsync($"/throw/key-not-found?message={Uri.EscapeDataString(message)}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(message, await ReadProblemDetailAsync(response));
    }

    /// <summary>
    /// Verifies a propagated <see cref="InvalidOperationException"/> yields HTTP 400 with the exception
    /// message preserved in the problem-details <c>detail</c> field.
    /// **Validates: Requirements 1.1, 4.1, 4.2**
    /// </summary>
    [Fact]
    public async Task InvalidOperationException_ProducesBadRequestWithMessage()
    {
        const string message = "Cannot modify a system role.";

        var response = await _client.GetAsync($"/throw/invalid-operation?message={Uri.EscapeDataString(message)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(message, await ReadProblemDetailAsync(response));
    }

    /// <summary>
    /// Verifies a propagated <see cref="ArgumentException"/> yields HTTP 400 with the exception
    /// message preserved in the problem-details <c>detail</c> field.
    /// **Validates: Requirements 1.1, 4.1, 4.2**
    /// </summary>
    [Fact]
    public async Task ArgumentException_ProducesBadRequestWithMessage()
    {
        const string message = "Name cannot be empty.";

        var response = await _client.GetAsync($"/throw/argument?message={Uri.EscapeDataString(message)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(message, await ReadProblemDetailAsync(response));
    }

    #endregion

    #region Unmapped Exception Test

    /// <summary>
    /// Verifies a propagated unmapped exception (<see cref="FormatException"/>) yields HTTP 500 and does
    /// not leak the exception message into the response body, preserving the default-handler behavior.
    /// **Validates: Requirements 4.6**
    /// </summary>
    [Fact]
    public async Task UnmappedException_ProducesServerErrorWithoutDetail()
    {
        const string message = "Sensitive internal failure detail.";

        var response = await _client.GetAsync($"/throw/unmapped?message={Uri.EscapeDataString(message)}");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(message, body, StringComparison.Ordinal);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Throws the supplied exception. Declaring a non-<see cref="HttpContext"/> return type lets the
    /// minimal-API endpoint lambdas bind to the request-handler overload while still always throwing,
    /// so the exception propagates to the wired exception handler.
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    /// <returns>Never returns; always throws.</returns>
    private static string ThrowFor(Exception exception) => throw exception;

    /// <summary>
    /// Reads the RFC 7807 problem-details response body and returns its <c>detail</c> field, which
    /// carries the preserved exception message for a mapped exception.
    /// </summary>
    /// <param name="response">The HTTP response to read.</param>
    /// <returns>The <c>detail</c> string, or null when the field is absent.</returns>
    private static async Task<string?> ReadProblemDetailAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("detail", out var detail)
            ? detail.GetString()
            : null;
    }

    #endregion
}
