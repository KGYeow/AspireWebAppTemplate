using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using AspireWebAppTemplate.ApiService.Abstractions;
using AspireWebAppTemplate.Core.Contracts.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.ApiService.Services;

/// <summary>
/// Implements <see cref="IAiService"/> by sending user prompts to Amazon Bedrock via the
/// Converse API and returning the generated text response. Handles prompt validation,
/// model configuration, credential-expired scenarios, known AWS exception mapping, and
/// sanitization of unexpected errors to prevent internal detail leakage.
/// </summary>
/// <remarks>
/// <para>
/// The service reads the model identifier from <c>Ai:ModelId</c> configuration, defaulting
/// to <c>amazon.nova-2-lite-v1:0</c> when not specified. All Bedrock invocations are subject
/// to a 60-second cancellation timeout.
/// </para>
/// <para>
/// Registered as a scoped service to align with the per-request service lifetime pattern
/// used throughout the application.
/// </para>
/// </remarks>
public class AiService : IAiService
{
    #region Constructor

    /// <summary>
    /// The maximum allowed length for prompt text at the service layer (10,000 characters).
    /// This is separate from the 4000-character DTO validation at the API contract level.
    /// </summary>
    private const int MaxPromptLength = 10000;

    /// <summary>
    /// The default model identifier used when <c>Ai:ModelId</c> is not configured.
    /// </summary>
    private const string DefaultModelId = "us.amazon.nova-2-lite-v1:0";

    /// <summary>
    /// The timeout duration for Bedrock model invocation requests.
    /// </summary>
    private static readonly TimeSpan InvocationTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The Amazon Bedrock Runtime client used to invoke the foundation model.
    /// </summary>
    private readonly AmazonBedrockRuntimeClient _bedrockClient;

    /// <summary>
    /// Application configuration for reading AI service settings (model ID, region, credentials).
    /// </summary>
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Logger for recording error-level events when unexpected exceptions occur during model invocation.
    /// </summary>
    private readonly ILogger<AiService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiService"/> class with the required dependencies.
    /// </summary>
    /// <param name="bedrockClient">The Amazon Bedrock Runtime client for model invocation.</param>
    /// <param name="configuration">Application configuration for reading AI settings.</param>
    /// <param name="logger">Logger for error-level exception logging.</param>
    public AiService(
        AmazonBedrockRuntimeClient bedrockClient,
        IConfiguration configuration,
        ILogger<AiService> logger)
    {
        _bedrockClient = bedrockClient;
        _configuration = configuration;
        _logger = logger;
    }

    #endregion

    #region Prompt Operations

    /// <inheritdoc />
    public async Task<AiResponseDto> SendPromptAsync(AiPromptRequest request)
    {
        ValidatePrompt(request.Prompt);

        var modelId = _configuration["Ai:ModelId"];
        if (string.IsNullOrWhiteSpace(modelId))
        {
            modelId = DefaultModelId;
        }

        var converseRequest = BuildConverseRequest(modelId, request.Prompt);

        using var cts = new CancellationTokenSource(InvocationTimeout);

        ConverseResponse response;
        try
        {
            response = await _bedrockClient.ConverseAsync(converseRequest, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogWarning("AI model invocation timed out after 60 seconds. ModelId: {ModelId}", modelId);
            throw new InvalidOperationException(
                "The AI model did not respond within the 60-second timeout.");
        }
        catch (AmazonServiceException ex) when (IsExpiredCredentialsException(ex))
        {
            _logger.LogWarning(ex, "AWS credentials expired during AI model invocation. ErrorCode: {ErrorCode}", ex.ErrorCode);
            throw new InvalidOperationException(
                "The AWS credentials have expired and need to be refreshed.", ex);
        }
        catch (ThrottlingException ex)
        {
            _logger.LogWarning(ex, "AI service request was throttled. ModelId: {ModelId}", modelId);
            throw new InvalidOperationException(
                "The AI service request was rate-limited. Please try again later.", ex);
        }
        catch (ResourceNotFoundException ex)
        {
            _logger.LogError(ex, "Configured AI model not found. ModelId: {ModelId}", modelId);
            throw new InvalidOperationException(
                "The configured AI model is unavailable.", ex);
        }
        catch (ServiceUnavailableException ex)
        {
            _logger.LogWarning(ex, "AWS Bedrock service unavailable. ModelId: {ModelId}", modelId);
            throw new InvalidOperationException(
                "The AI service is temporarily unavailable. Please try again later.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error invoking AI model. ModelId: {ModelId}, ExceptionType: {ExceptionType}, Message: {ErrorMessage}",
                modelId, ex.GetType().Name, ex.Message);
            throw new InvalidOperationException(
                "An unexpected error occurred while processing the AI request.", ex);
        }

        var generatedText = ExtractTextContent(response);

        return new AiResponseDto { GeneratedText = generatedText };
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Validates the prompt text, throwing <see cref="ArgumentException"/> if it is null,
    /// empty, whitespace-only, or exceeds the maximum allowed length of 10,000 characters.
    /// </summary>
    /// <param name="prompt">The prompt text to validate.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the prompt is null, empty, whitespace-only, or exceeds 10,000 characters.
    /// </exception>
    private static void ValidatePrompt(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt text is required and cannot be empty or whitespace.", nameof(prompt));
        }

        if (prompt.Length > MaxPromptLength)
        {
            throw new ArgumentException(
                $"Prompt text exceeds the maximum allowed length of {MaxPromptLength} characters.", nameof(prompt));
        }
    }

    /// <summary>
    /// Constructs a <see cref="ConverseRequest"/> with the specified model ID and user prompt
    /// formatted as a user message with text content.
    /// </summary>
    /// <param name="modelId">The Bedrock foundation model identifier.</param>
    /// <param name="prompt">The user's prompt text.</param>
    /// <returns>A configured <see cref="ConverseRequest"/> ready for invocation.</returns>
    private static ConverseRequest BuildConverseRequest(string modelId, string prompt)
    {
        return new ConverseRequest
        {
            ModelId = modelId,
            Messages = new List<Message>
            {
                new Message
                {
                    Role = ConversationRole.User,
                    Content = new List<ContentBlock>
                    {
                        new ContentBlock { Text = prompt }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Extracts the text content from the Bedrock <see cref="ConverseResponse"/>, throwing
    /// <see cref="InvalidOperationException"/> if the response contains no text content.
    /// </summary>
    /// <param name="response">The response from the Bedrock Converse API.</param>
    /// <returns>The generated text content from the model response.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the model response contains no text content.
    /// </exception>
    private static string ExtractTextContent(ConverseResponse response)
    {
        var message = response.Output?.Message;
        if (message?.Content == null || message.Content.Count == 0)
        {
            throw new InvalidOperationException("The AI model returned no content.");
        }

        var textBlock = message.Content.FirstOrDefault(block => !string.IsNullOrEmpty(block.Text));
        if (textBlock == null || string.IsNullOrEmpty(textBlock.Text))
        {
            throw new InvalidOperationException("The AI model returned no content.");
        }

        return textBlock.Text;
    }

    /// <summary>
    /// Determines whether an <see cref="AmazonServiceException"/> indicates that the AWS
    /// credentials have expired or are invalid. Checks both error codes and the exception
    /// message for "expired" to handle various credential expiry scenarios.
    /// </summary>
    /// <param name="ex">The AWS service exception to check.</param>
    /// <returns>
    /// <c>true</c> if the exception indicates expired or invalid credentials; otherwise, <c>false</c>.
    /// </returns>
    private static bool IsExpiredCredentialsException(AmazonServiceException ex)
    {
        var errorCode = ex.ErrorCode;

        if (!string.IsNullOrEmpty(errorCode))
        {
            if (errorCode.Equals("ExpiredTokenException", StringComparison.OrdinalIgnoreCase)
                || errorCode.Equals("ExpiredToken", StringComparison.OrdinalIgnoreCase)
                || errorCode.Equals("RequestExpired", StringComparison.OrdinalIgnoreCase)
                || errorCode.Equals("InvalidClientTokenId", StringComparison.OrdinalIgnoreCase)
                || errorCode.Equals("UnrecognizedClientException", StringComparison.OrdinalIgnoreCase)
                || errorCode.Equals("InvalidIdentityToken", StringComparison.OrdinalIgnoreCase)
                || errorCode.Equals("TokenRefreshRequired", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Fallback: check the exception message for "expired" when no matching error code is found.
        if (!string.IsNullOrEmpty(ex.Message)
            && ex.Message.Contains("expired", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    #endregion
}
