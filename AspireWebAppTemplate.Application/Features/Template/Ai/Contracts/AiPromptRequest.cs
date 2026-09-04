using System.ComponentModel.DataAnnotations;

namespace AspireWebAppTemplate.Application.Features.Template.Ai;

/// <summary>
/// Request DTO containing the user's natural language prompt for AI text generation.
/// </summary>
public sealed class AiPromptRequest
{
    /// <summary>
    /// The natural language prompt text to send to the AI model (required, max 4000 characters).
    /// </summary>
    [Required(ErrorMessage = "Prompt is required.")]
    [StringLength(4000, ErrorMessage = "Prompt must not exceed 4000 characters.")]
    public string Prompt { get; set; } = string.Empty;
}
