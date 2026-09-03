using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.Template.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspireWebAppTemplate.ApiService.Controllers;

/// <summary>
/// Provides AI text generation endpoints. This controller is intentionally thin — it handles
/// HTTP concerns only (request parsing, status code mapping) and delegates all business logic
/// to <see cref="IAiService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Exception-to-HTTP-status mapping:
/// <list type="bullet">
///   <item><see cref="ArgumentException"/> → 400 Bad Request (invalid prompt text)</item>
///   <item><see cref="InvalidOperationException"/> → 400 Bad Request (configuration, timeout, or Bedrock errors)</item>
/// </list>
/// </para>
/// </remarks>
[Route("api/ai")]
[Authorize]
public class AiController : BaseController
{
    #region Constructor

    /// <summary>
    /// The AI service for processing prompt requests against the configured foundation model.
    /// </summary>
    private readonly IAiService _aiService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiController"/> class.
    /// </summary>
    /// <param name="aiService">The AI service for processing prompt requests against the configured foundation model.</param>
    public AiController(IAiService aiService)
    {
        _aiService = aiService;
    }

    #endregion

    #region Prompt Operations

    /// <summary>
    /// Sends a natural language prompt to the configured AI foundation model and returns
    /// the generated text response.
    /// </summary>
    /// <param name="request">The prompt request containing the user's natural language text.</param>
    /// <returns>An <see cref="AiResponseDto"/> containing the AI-generated text.</returns>
    /// <response code="200">The prompt was processed successfully and the generated text is returned.</response>
    /// <response code="400">The prompt is invalid (empty, whitespace, exceeds length) or an AI service error occurred.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("prompt")]
    [ProducesResponseType(typeof(AiResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AiResponseDto>> SendPrompt([FromBody] AiPromptRequest request)
    {
        try
        {
            var result = await _aiService.SendPromptAsync(request);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    #endregion
}
