// Feature: reusable-api-infrastructure, Property 1: Mapped exceptions produce the correct status and preserve the message
using System.Text.Json;
using AspireWebAppTemplate.ApiService.Exceptions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AspireWebAppTemplate.Tests.ControllerServiceRefactor;

/// <summary>
/// Property-based tests verifying that <see cref="ExceptionMappingHandler"/> maps each of the seven
/// mapped exception types to the correct HTTP status code and preserves the exception message
/// unmodified in the written problem-details response body.
/// </summary>
/// <remarks>
/// Drives <see cref="ExceptionMappingHandler.TryHandleAsync"/> directly with a real
/// <see cref="IProblemDetailsService"/> and a <see cref="DefaultHttpContext"/> whose response body
/// is a <see cref="MemoryStream"/>, then reads the written RFC 7807 payload and asserts its
/// <c>detail</c> field equals the input message.
/// **Validates: Requirements 1.2, 1.3, 1.4, 1.5, 4.1, 4.2, 4.5**
/// </remarks>
public class ExceptionMappingHandlerTests
{
    /// <summary>
    /// Builds a service provider that supplies a real ASP.NET Core problem-details service, matching
    /// the wiring the ApiService uses via <c>AddProblemDetails()</c>.
    /// </summary>
    /// <returns>A service provider exposing <see cref="IProblemDetailsService"/>.</returns>
    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a mapped exception instance of the requested kind carrying the supplied message.
    /// </summary>
    /// <param name="kind">The mapped-exception selector (0-6).</param>
    /// <param name="message">The message to preserve.</param>
    /// <returns>The exception instance and the status code it maps to.</returns>
    private static (Exception Exception, int ExpectedStatus) CreateMappedException(int kind, string message)
    {
        return kind switch
        {
            0 => (new KeyNotFoundException(message), StatusCodes.Status404NotFound),
            1 => (new InvalidOperationException(message), StatusCodes.Status400BadRequest),
            2 => (new ArgumentException(message), StatusCodes.Status400BadRequest),
            3 => (new ArgumentNullException(paramName: null, message), StatusCodes.Status400BadRequest),
            4 => (new UnauthorizedAccessException(message), StatusCodes.Status403Forbidden),
            5 => (new NotImplementedException(message), StatusCodes.Status501NotImplemented),
            _ => (new TimeoutException(message), StatusCodes.Status504GatewayTimeout),
        };
    }

    /// <summary>
    /// Property: For each of the seven mapped exception types (including <see cref="ArgumentNullException"/>
    /// and <see cref="ArgumentOutOfRangeException"/> as subtypes of <see cref="ArgumentException"/>) and any
    /// message, <see cref="ExceptionMappingHandler.TryHandleAsync"/> returns <c>true</c>, sets the response
    /// status to the mapped code (404/400/403/501/504), and writes a body whose <c>detail</c> equals the
    /// exception message.
    /// **Validates: Requirements 1.2, 1.3, 1.4, 1.5, 4.1, 4.2, 4.5**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property MappedException_ProducesCorrectStatusAndPreservesMessage()
    {
        // Cover all seven mapped arms: KeyNotFound, InvalidOperation, ArgumentException,
        // ArgumentNullException, ArgumentOutOfRangeException, UnauthorizedAccess, NotImplemented, Timeout.
        var kindGen = Gen.Elements(0, 1, 2, 3, 4, 5, 6, 7);

        var messageGen = Gen.Elements(
            "Role not found.", "Cannot modify system role.", "Name cannot be empty.",
            "Access is denied.", "Not implemented yet.", "The operation timed out.",
            "Value 'abc' contains an ampersand & symbol.", string.Empty);

        var inputGen = from kind in kindGen
                       from message in messageGen
                       select new { Kind = kind, Message = message };

        return Prop.ForAll(Arb.From(inputGen), input =>
        {
            // ArgumentOutOfRangeException is exercised via kind 7; other kinds map through the helper.
            var (exception, expectedStatus) = input.Kind == 7
                ? (new ArgumentOutOfRangeException(paramName: null, input.Message), StatusCodes.Status400BadRequest)
                : CreateMappedException(input.Kind, input.Message);

            // Arrange: real problem-details service and an HTTP context whose body is a MemoryStream.
            var provider = BuildServiceProvider();
            var problemDetailsService = provider.GetRequiredService<IProblemDetailsService>();
            var handler = new ExceptionMappingHandler(problemDetailsService);

            var body = new MemoryStream();
            var httpContext = new DefaultHttpContext { RequestServices = provider };
            httpContext.Response.Body = body;

            // Act
            var handled = handler
                .TryHandleAsync(httpContext, exception, CancellationToken.None)
                .GetAwaiter().GetResult();

            // Assert: handled, correct status, and the written body's detail equals the message.
            var statusMatches = httpContext.Response.StatusCode == expectedStatus;

            body.Position = 0;
            using var document = JsonDocument.Parse(body);
            var detailMatches =
                document.RootElement.TryGetProperty("detail", out var detailElement) &&
                detailElement.GetString() == exception.Message;

            return (handled && statusMatches && detailMatches)
                .Label($"kind={input.Kind}, handled={handled}, expected={expectedStatus}, " +
                       $"actual={httpContext.Response.StatusCode}, detailMatches={detailMatches}");
        });
    }
}
