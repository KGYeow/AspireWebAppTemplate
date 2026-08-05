using System.ComponentModel.DataAnnotations;

namespace AspireWebAppTemplate.Application.Contracts.Email;

/// <summary>
/// Request DTO for updating an existing business notification email template.
/// This is the only mutation DTO — no create or delete request DTOs exist.
/// </summary>
public sealed class UpdateEmailTemplateRequest
{
    /// <summary>
    /// The updated human-readable display name (required, max 200 characters).
    /// </summary>
    [Required(ErrorMessage = "Display name is required.")]
    [MaxLength(200, ErrorMessage = "Display name must not exceed 200 characters.")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The updated email subject line template (required, max 500 characters).
    /// Supports {{placeholder}} syntax for variable substitution.
    /// </summary>
    [Required(ErrorMessage = "Subject is required.")]
    [MaxLength(500, ErrorMessage = "Subject must not exceed 500 characters.")]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// The updated HTML body template content (required).
    /// Supports {{placeholder}} syntax for variable substitution.
    /// </summary>
    [Required(ErrorMessage = "HTML body is required.")]
    public string HtmlBody { get; set; } = string.Empty;

    /// <summary>
    /// The updated comma-separated list of available placeholder variable names (optional, max 1000 characters).
    /// Displayed in the admin UI as guidance for editors.
    /// </summary>
    [MaxLength(1000, ErrorMessage = "Placeholder hints must not exceed 1000 characters.")]
    public string PlaceholderHints { get; set; } = string.Empty;

    /// <summary>
    /// Whether the template should be active and available for sending.
    /// </summary>
    public bool IsActive { get; set; }
}
