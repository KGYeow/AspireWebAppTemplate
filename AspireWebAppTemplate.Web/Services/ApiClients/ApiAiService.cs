using System.Net.Http.Json;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Contracts.Ai;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// Typed HttpClient service for AI prompt operations.
/// Uses Aspire service discovery and <see cref="UserIdentityDelegatingHandler"/> for auth propagation.
/// Wraps calls to the ApiService's AiController endpoint.
/// </summary>
public class ApiAiService
{
    #region Constructor

    /// <summary>
    /// The API endpoint path for sending AI prompts.
    /// </summary>
    private const string PromptPath = "/api/ai/prompt";

    /// <summary>
    /// The underlying HttpClient configured with the ApiService base address.
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of <see cref="ApiAiService"/> with the configured HttpClient.
    /// </summary>
    /// <param name="http">The HttpClient instance configured via Aspire service discovery.</param>
    public ApiAiService(HttpClient http)
    {
        _http = http;
    }

    #endregion

    #region Prompt Operations

    /// <summary>
    /// Sends a natural language prompt to the AI model and returns the generated text response.
    /// Performs client-side validation before making the HTTP call.
    /// Calls POST /api/ai/prompt.
    /// </summary>
    /// <param name="prompt">The natural language prompt text to send to the AI model.</param>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the AI-generated response on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<AiResponseDto>> SendPromptAsync(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return ApiResult<AiResponseDto>.Failure("Prompt is required.");

        var request = new AiPromptRequest { Prompt = prompt };
        var response = await _http.PostAsJsonAsync(PromptPath, request);

        if (response.IsSuccessStatusCode)
            return ApiResult<AiResponseDto>.Success(
                await response.Content.ReadFromJsonAsync<AiResponseDto>()!);

        return ApiResult<AiResponseDto>.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion
}
