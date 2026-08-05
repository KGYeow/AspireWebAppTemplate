using AspireWebAppTemplate.Application.Contracts.Ai;

namespace AspireWebAppTemplate.Application.Abstractions;

/// <summary>
/// Defines the contract for AI text generation operations. The service sends user prompts
/// to a configured foundation model and returns generated text responses. All Bedrock
/// communication and error handling is encapsulated here — controllers delegate to this
/// service without touching AWS clients directly.
/// </summary>
/// <remarks>
/// <para>
/// Implementations validate prompt text (non-empty, non-whitespace, within length limit)
/// before forwarding to the model, and map all AWS/Bedrock errors to typed exceptions
/// with descriptive messages that do not leak internal details.
/// </para>
/// <para>
/// Implementations should be registered as scoped services to align with the per-request
/// service lifetime pattern used throughout the application.
/// </para>
/// </remarks>
public interface IAiService
{
    #region Prompt Operations

    /// <summary>
    /// Sends a natural language prompt to the configured AI foundation model and returns
    /// the generated text response.
    /// </summary>
    /// <param name="request">
    /// An <see cref="AiPromptRequest"/> containing the user's natural language prompt text
    /// to send to the AI model. The prompt must be non-empty, non-whitespace, and at most
    /// 4000 characters.
    /// </param>
    /// <returns>
    /// A task that resolves to an <see cref="AiResponseDto"/> containing the text generated
    /// by the AI model in response to the user's prompt.
    /// </returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when the prompt text is null, empty, composed entirely of whitespace characters,
    /// or exceeds the maximum allowed length of 4000 characters.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the AI model configuration is missing (model ID or region not set),
    /// the request times out (60-second limit), the configured model is unavailable,
    /// the service is being rate-limited, the AI service is temporarily unavailable,
    /// the AWS credentials have expired, or an unexpected error occurs during model invocation.
    /// The original exception is preserved as the inner exception.
    /// </exception>
    Task<AiResponseDto> SendPromptAsync(AiPromptRequest request);

    #endregion
}
