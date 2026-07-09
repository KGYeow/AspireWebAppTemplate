// Feature: aws-ai-integration, Property 1: Valid prompt produces mapped response
// Feature: aws-ai-integration, Property 2: Whitespace-only prompts are rejected
// Feature: aws-ai-integration, Property 3: Known AWS errors map to descriptive exceptions
// Feature: aws-ai-integration, Property 4: Unexpected exceptions do not leak internal details
// Feature: aws-ai-integration, Property 8: Expired credentials produce descriptive exception
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using AspireWebAppTemplate.ApiService.Services;
using AspireWebAppTemplate.Core.Contracts.Ai;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace AspireWebAppTemplate.Tests.AiIntegration;

/// <summary>
/// Property-based tests verifying AiService behavior:
/// - Valid prompt produces mapped response (Property 1)
/// - Whitespace-only prompts are rejected (Property 2)
/// - Known AWS errors map to descriptive exceptions (Property 3)
/// - Unexpected exceptions do not leak internal details (Property 4)
/// - Expired credentials produce descriptive exception (Property 8)
/// </summary>
/// <remarks>
/// **Validates: Requirements 1.1, 1.2, 3.1, 3.2, 3.3, 3.4, 2.8, 3.7**
/// </remarks>
public class AiServicePropertyTests
{
    /// <summary>
    /// Creates a mock IConfiguration that returns "us-east-1" for Ai:Region and no model ID.
    /// </summary>
    private static IConfiguration CreateMockConfiguration()
    {
        var configData = new Dictionary<string, string?> { ["Ai:Region"] = "us-east-1", ["Ai:ModelId"] = null };
        return new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
    }

    /// <summary>
    /// Creates a mock AmazonBedrockRuntimeClient that returns the specified text from ConverseAsync.
    /// </summary>
    private static AmazonBedrockRuntimeClient CreateMockBedrockClient(string responseText)
    {
        var config = new AmazonBedrockRuntimeConfig { RegionEndpoint = Amazon.RegionEndpoint.USEast1 };
        var mockClient = new Mock<AmazonBedrockRuntimeClient>(config);
        var converseResponse = new ConverseResponse
        {
            Output = new ConverseOutput
            {
                Message = new Message
                {
                    Role = ConversationRole.Assistant,
                    Content = new List<ContentBlock> { new ContentBlock { Text = responseText } }
                }
            }
        };
        mockClient.Setup(c => c.ConverseAsync(It.IsAny<ConverseRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(converseResponse);
        return mockClient.Object;
    }

    /// <summary>
    /// Property: For any non-empty, non-whitespace prompt and any non-empty mocked response,
    /// AiService.SendPromptAsync returns an AiResponseDto whose GeneratedText equals the mocked output.
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ValidPrompt_ReturnsMappedResponse()
    {
        var charGen = Gen.Elements('a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z',' ','1','2','3');
        var promptGen = Gen.Choose(1, 100)
            .SelectMany<int, string>(len => Gen.ArrayOf<char>(charGen, len).Select(chars => new string(chars)))
            .Where(s => !string.IsNullOrWhiteSpace(s));
        var responseTextGen = Gen.Elements("Hello, world!", "The answer is 42.", "Generated response.", "Short reply.", "Longer response text.");
        return Prop.ForAll(Arb.From(promptGen), Arb.From(responseTextGen), (string prompt, string expectedResponse) =>
        {
            var bedrockClient = CreateMockBedrockClient(expectedResponse);
            var service = new AiService(bedrockClient, CreateMockConfiguration(), Mock.Of<ILogger<AiService>>());
            var result = service.SendPromptAsync(new AiPromptRequest { Prompt = prompt }).GetAwaiter().GetResult();
            return (result.GeneratedText == expectedResponse).Label($"Expected '{expectedResponse}', got '{result.GeneratedText}'");
        });
    }

    /// <summary>
    /// Property: For any whitespace-only string, AiService.SendPromptAsync throws ArgumentException
    /// and the Bedrock client is never invoked.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property WhitespacePrompt_ThrowsArgumentException()
    {
        var whitespaceGen = Gen.Elements(" ", "  ", "\t", "\n", "\r\n", "   ", "\t\t", " \t \n ");
        return Prop.ForAll(Arb.From(whitespaceGen), (string whitespacePrompt) =>
        {
            var config = new AmazonBedrockRuntimeConfig { RegionEndpoint = Amazon.RegionEndpoint.USEast1 };
            var mockBedrockClient = new Mock<AmazonBedrockRuntimeClient>(config);
            var service = new AiService(mockBedrockClient.Object, CreateMockConfiguration(), Mock.Of<ILogger<AiService>>());
            try
            {
                service.SendPromptAsync(new AiPromptRequest { Prompt = whitespacePrompt }).GetAwaiter().GetResult();
                return false.Label("Expected ArgumentException but none was thrown.");
            }
            catch (ArgumentException)
            {
                mockBedrockClient.Verify(c => c.ConverseAsync(It.IsAny<ConverseRequest>(), It.IsAny<CancellationToken>()), Times.Never);
                return true.Label("ArgumentException thrown and Bedrock not invoked.");
            }
            catch (Exception ex) { return false.Label($"Got {ex.GetType().Name}: {ex.Message}"); }
        });
    }

    /// <summary>
    /// Property: For each known Bedrock error type (ThrottlingException, ResourceNotFoundException,
    /// ServiceUnavailableException), when thrown by the mocked Bedrock client, AiService.SendPromptAsync
    /// SHALL throw an InvalidOperationException with a descriptive message and the original as InnerException.
    /// **Validates: Requirements 3.1, 3.2, 3.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property KnownAwsError_MapsToDescriptiveException()
    {
        var charGen = Gen.Elements('a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z',' ','1','2','3');
        var promptGen = Gen.Choose(1, 100)
            .SelectMany<int, string>(len => Gen.ArrayOf<char>(charGen, len).Select(chars => new string(chars)))
            .Where(s => !string.IsNullOrWhiteSpace(s));
        return Prop.ForAll(Arb.From(promptGen), (string prompt) =>
        {
            var exceptions = new (Exception ex, string expectedFragment)[]
            {
                (new ThrottlingException("throttled"), "rate-limited"),
                (new ResourceNotFoundException("not found"), "unavailable"),
                (new ServiceUnavailableException("service down"), "temporarily unavailable"),
            };

            foreach (var (awsException, expectedFragment) in exceptions)
            {
                var config = new AmazonBedrockRuntimeConfig { RegionEndpoint = Amazon.RegionEndpoint.USEast1 };
                var mockBedrockClient = new Mock<AmazonBedrockRuntimeClient>(config);
                mockBedrockClient.Setup(c => c.ConverseAsync(It.IsAny<ConverseRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(awsException);
                var service = new AiService(mockBedrockClient.Object, CreateMockConfiguration(), Mock.Of<ILogger<AiService>>());
                try
                {
                    service.SendPromptAsync(new AiPromptRequest { Prompt = prompt }).GetAwaiter().GetResult();
                    return false.Label($"Expected InvalidOperationException for {awsException.GetType().Name} but none was thrown.");
                }
                catch (InvalidOperationException ex)
                {
                    var hasFragment = ex.Message.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase);
                    var innerOk = ReferenceEquals(ex.InnerException, awsException);
                    if (!hasFragment || !innerOk)
                        return false.Label($"For {awsException.GetType().Name}: fragment={hasFragment}, inner={innerOk}, msg='{ex.Message}'");
                }
                catch (Exception ex)
                {
                    return false.Label($"Expected InvalidOperationException for {awsException.GetType().Name} but got {ex.GetType().Name}: {ex.Message}");
                }
            }
            return true.Label("All known AWS errors mapped correctly.");
        });
    }

    /// <summary>
    /// Property: For any unexpected exception (generic Exception) thrown by the Bedrock client,
    /// the resulting InvalidOperationException message SHALL NOT contain the original exception's
    /// message or stack trace, and the InnerException SHALL preserve the original exception.
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property UnexpectedException_DoesNotLeakDetails()
    {
        var charGen = Gen.Elements('a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z',' ','1','2','3');
        var promptGen = Gen.Choose(1, 100)
            .SelectMany<int, string>(len => Gen.ArrayOf<char>(charGen, len).Select(chars => new string(chars)))
            .Where(s => !string.IsNullOrWhiteSpace(s));
        var secretMessageGen = Gen.Elements(
            "NullRef at Line 42", "Server=prod-db;Password=s3cr3t",
            "stack trace: MyApp.Services", "credential file not found",
            "Timeout 10.0.1.55:5432", "secret-api-key-12345",
            "pool exhausted", "internal error details");
        return Prop.ForAll(Arb.From(promptGen), Arb.From(secretMessageGen), (string prompt, string secretMessage) =>
        {
            var originalException = new Exception(secretMessage);
            var config = new AmazonBedrockRuntimeConfig { RegionEndpoint = Amazon.RegionEndpoint.USEast1 };
            var bedrockClientMock = new Mock<AmazonBedrockRuntimeClient>(config);
            bedrockClientMock.Setup(c => c.ConverseAsync(It.IsAny<ConverseRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(originalException);
            var service = new AiService(bedrockClientMock.Object, CreateMockConfiguration(), Mock.Of<ILogger<AiService>>());

            InvalidOperationException? caught = null;
            try { service.SendPromptAsync(new AiPromptRequest { Prompt = prompt }).GetAwaiter().GetResult(); }
            catch (InvalidOperationException ex) { caught = ex; }

            var wasThrown = caught is not null;
            var doesNotLeak = caught is not null && !caught.Message.Contains(secretMessage, StringComparison.Ordinal);
            var sanitized = caught?.Message == "An unexpected error occurred while processing the AI request.";
            var innerPreserved = ReferenceEquals(caught?.InnerException, originalException);
            return (wasThrown && doesNotLeak && sanitized && innerPreserved).Label(
                $"Thrown={wasThrown}, DoesNotLeak={doesNotLeak}, Sanitized={sanitized}, Inner={innerPreserved}");
        });
    }

    /// <summary>
    /// Property: For any AmazonServiceException with an expired credentials error code,
    /// AiService.SendPromptAsync throws InvalidOperationException whose message mentions
    /// "expired" and whose InnerException is the original AWS exception.
    /// **Validates: Requirements 2.8, 3.7**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ExpiredCredentials_ThrowsDescriptiveException()
    {
        var charGen = Gen.Elements('a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z',' ','1','2','3');
        var promptGen = Gen.Choose(1, 100)
            .SelectMany<int, string>(len => Gen.ArrayOf<char>(charGen, len).Select(chars => new string(chars)))
            .Where(s => !string.IsNullOrWhiteSpace(s));
        return Prop.ForAll(Arb.From(promptGen), (string prompt) =>
        {
            var expiredException = new AmazonServiceException("Token expired") { ErrorCode = "ExpiredTokenException" };
            var config = new AmazonBedrockRuntimeConfig { RegionEndpoint = Amazon.RegionEndpoint.USEast1 };
            var mockBedrockClient = new Mock<AmazonBedrockRuntimeClient>(config);
            mockBedrockClient.Setup(c => c.ConverseAsync(It.IsAny<ConverseRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(expiredException);
            var service = new AiService(mockBedrockClient.Object, CreateMockConfiguration(), Mock.Of<ILogger<AiService>>());
            try
            {
                service.SendPromptAsync(new AiPromptRequest { Prompt = prompt }).GetAwaiter().GetResult();
                return false.Label("Expected InvalidOperationException but none was thrown.");
            }
            catch (InvalidOperationException ex)
            {
                var hasExpired = ex.Message.Contains("expired", StringComparison.OrdinalIgnoreCase);
                var innerOk = ReferenceEquals(ex.InnerException, expiredException);
                return (hasExpired && innerOk).Label($"expired={hasExpired}, inner={innerOk}, msg='{ex.Message}'");
            }
            catch (Exception ex) { return false.Label($"Got {ex.GetType().Name}: {ex.Message}"); }
        });
    }
}
