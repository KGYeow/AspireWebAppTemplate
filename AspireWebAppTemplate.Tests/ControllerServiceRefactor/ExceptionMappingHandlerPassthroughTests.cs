// Feature: reusable-api-infrastructure, Property 2: Unmapped exceptions are left to the default handler
using System.Text;
using AspireWebAppTemplate.ApiService.Exceptions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Moq;

namespace AspireWebAppTemplate.Tests.ControllerServiceRefactor;

/// <summary>
/// Property-based test verifying that <see cref="ExceptionMappingHandler"/> leaves exceptions
/// outside its seven mapped types to the default handler: <c>TryHandleAsync</c> returns
/// <c>false</c> and writes no response body, so ASP.NET Core falls back to producing a 500 with
/// no exception detail exposed.
/// </summary>
/// <remarks>
/// **Validates: Requirements 1.6, 4.6**
/// </remarks>
public class ExceptionMappingHandlerPassthroughTests
{
    /// <summary>
    /// Creates a fresh <see cref="ExceptionMappingHandler"/> backed by a mock
    /// <see cref="IProblemDetailsService"/> whose <c>TryWriteAsync</c> invocations are recorded,
    /// so the test can assert that no problem-details body was written for an unmapped exception.
    /// </summary>
    /// <returns>A tuple of the handler and the mock problem-details service.</returns>
    private static (ExceptionMappingHandler Handler, Mock<IProblemDetailsService> ProblemDetails) CreateHandler()
    {
        var problemDetails = new Mock<IProblemDetailsService>();

        // A mapped exception would call TryWriteAsync; an unmapped one must never reach it.
        problemDetails
            .Setup(p => p.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .ReturnsAsync(true);

        return (new ExceptionMappingHandler(problemDetails.Object), problemDetails);
    }

    /// <summary>
    /// Builds a <see cref="DefaultHttpContext"/> whose response body is captured in a
    /// <see cref="MemoryStream"/> so the test can assert that nothing was written.
    /// </summary>
    /// <param name="bodyStream">The stream capturing any written response body.</param>
    /// <returns>The configured HTTP context.</returns>
    private static DefaultHttpContext CreateHttpContext(out MemoryStream bodyStream)
    {
        bodyStream = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Response.Body = bodyStream;
        return context;
    }

    /// <summary>
    /// Verifies the mock problem-details service was never asked to write a response, indicating
    /// the handler did not attempt to produce a body for the unmapped exception.
    /// </summary>
    /// <param name="problemDetails">The mock problem-details service to inspect.</param>
    /// <returns><c>true</c> when <c>TryWriteAsync</c> was never invoked; otherwise <c>false</c>.</returns>
    private static bool TryWriteNeverCalled(Mock<IProblemDetailsService> problemDetails)
    {
        try
        {
            problemDetails.Verify(p => p.TryWriteAsync(It.IsAny<ProblemDetailsContext>()), Times.Never);
            return true;
        }
        catch (MockException)
        {
            return false;
        }
    }

    /// <summary>
    /// Property: For any exception type outside the seven mapped types, <c>TryHandleAsync</c>
    /// returns <c>false</c> and writes no response body, so the default handler produces the
    /// unmapped 500 with no detail.
    /// **Validates: Requirements 1.6, 4.6**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property UnmappedExceptions_AreLeftToDefaultHandler()
    {
        var messageGen = Gen.Elements(
            "Something failed.", "Unexpected error.", "Boom.", "Internal failure detail.");

        // Factories for exception types NOT in the mapped set
        // (Exception, FormatException, NullReferenceException, IOException, DivideByZeroException,
        // NotSupportedException). None derive from any of the seven mapped types.
        var factoryGen = Gen.Elements<Func<string, Exception>>(
            m => new Exception(m),
            m => new FormatException(m),
            m => new NullReferenceException(m),
            m => new System.IO.IOException(m),
            m => new DivideByZeroException(m),
            m => new NotSupportedException(m));

        var inputGen = from message in messageGen
                       from factory in factoryGen
                       select new { Message = message, Exception = factory(message) };

        return Prop.ForAll(Arb.From(inputGen), input =>
        {
            // Arrange
            var (handler, problemDetails) = CreateHandler();
            var httpContext = CreateHttpContext(out var bodyStream);

            // Act
            var handled = handler
                .TryHandleAsync(httpContext, input.Exception, CancellationToken.None)
                .GetAwaiter().GetResult();

            bodyStream.Flush();
            var writtenBody = Encoding.UTF8.GetString(bodyStream.ToArray());

            // Assert: the handler declines the exception (returns false), never writes a
            // problem-details body, and leaks no response body — so the default handler is
            // left to produce the 500 with no exception detail.
            var returnedFalse = !handled;
            var wroteNoProblemDetails = TryWriteNeverCalled(problemDetails);
            var wroteNothing = writtenBody.Length == 0;
            var noMessageLeak = !writtenBody.Contains(input.Message, StringComparison.Ordinal);

            return (returnedFalse && wroteNoProblemDetails && wroteNothing && noMessageLeak)
                .Label($"type={input.Exception.GetType().Name}, returnedFalse={returnedFalse}, " +
                       $"wroteNoProblemDetails={wroteNoProblemDetails}, wroteNothing={wroteNothing}, " +
                       $"noMessageLeak={noMessageLeak}, body='{writtenBody}'");
        });
    }
}
