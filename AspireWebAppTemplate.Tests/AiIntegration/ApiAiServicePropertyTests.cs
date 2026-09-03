// Feature: aws-ai-integration, Property 6: HTTP error responses map to failed ApiResult
// Feature: aws-ai-integration, Property 7: Client-side prompt validation rejects invalid input
using System.Net;
using AspireWebAppTemplate.Application.Features.Template.Ai;
using AspireWebAppTemplate.Web.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Moq;
using Moq.Protected;

namespace AspireWebAppTemplate.Tests.AiIntegration;

/// <summary>
/// Property-based tests verifying ApiAiService behavior:
/// - HTTP error responses map to failed ApiResult (Property 6)
/// - Client-side prompt validation rejects invalid input (Property 7)
/// </summary>
/// <remarks>
/// **Validates: Requirements 6.4, 6.5**
/// </remarks>
public class ApiAiServicePropertyTests
{
    /// <summary>
    /// Property: For any HTTP response with a non-success status code (4xx or 5xx) and any response body string,
    /// ApiAiService.SendPromptAsync SHALL return an ApiResult where Succeeded is false and Error contains
    /// the response body text.
    /// **Validates: Requirements 6.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property HttpError_MapsToFailedApiResult()
    {
        var statusCodeGen = Gen.Elements(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.BadGateway,
            HttpStatusCode.ServiceUnavailable);

        var errorBodyGen = Gen.Elements(
            "Prompt is required.",
            "Model not found.",
            "Rate limit exceeded.",
            "Internal server error occurred.",
            "Service temporarily unavailable.",
            "Unauthorized access.",
            "Forbidden resource.");

        return Prop.ForAll(Arb.From(statusCodeGen), Arb.From(errorBodyGen), (HttpStatusCode statusCode, string errorBody) =>
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(errorBody)
                });

            var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
            var service = new ApiAiService(httpClient);

            var result = service.SendPromptAsync("valid prompt").GetAwaiter().GetResult();

            var succeededIsFalse = !result.Succeeded;
            var errorContainsBody = result.Error != null && result.Error.Contains(errorBody, StringComparison.Ordinal);

            return (succeededIsFalse && errorContainsBody).Label(
                $"StatusCode={statusCode}, Succeeded={result.Succeeded}, Error='{result.Error}', ExpectedBody='{errorBody}'");
        });
    }

    /// <summary>
    /// Property: For any string that is null, empty, or composed entirely of whitespace,
    /// ApiAiService.SendPromptAsync SHALL return a failed ApiResult without making an HTTP request.
    /// **Validates: Requirements 6.5**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ClientSideValidation_RejectsInvalidInput()
    {
        var invalidPromptGen = Gen.Elements<string?>(null, "", " ", "  ", "\t", "\n", "\r\n", "   ");
        return Prop.ForAll(Arb.From(invalidPromptGen), (string? invalidPrompt) =>
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("HTTP call should not have been made"));

            var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
            var service = new ApiAiService(httpClient);

            var result = service.SendPromptAsync(invalidPrompt!).GetAwaiter().GetResult();

            var succeededIsFalse = !result.Succeeded;
            var hasErrorMessage = result.Error != null && result.Error.Contains("Prompt is required.", StringComparison.Ordinal);

            return (succeededIsFalse && hasErrorMessage).Label(
                $"Input='{invalidPrompt ?? "null"}', Succeeded={result.Succeeded}, Error='{result.Error}'");
        });
    }
}
