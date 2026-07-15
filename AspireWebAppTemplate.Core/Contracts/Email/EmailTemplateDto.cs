using AspireWebAppTemplate.Core.Domain.Enums;

namespace AspireWebAppTemplate.Core.Contracts.Email;

/// <summary>
/// Response DTO representing an email template.
/// Returned by template query and detail endpoints.
/// </summary>
public sealed class EmailTemplateDto
{
    /// <summary>
    /// The unique identifier of the email template.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The email type this template represents.
    /// </summary>
    public EmailType EmailType { get; set; }

    /// <summary>
    /// The human-readable display name shown in the admin UI.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The email subject line template with optional {{placeholder}} syntax.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// The HTML body template content.
    /// </summary>
    public string HtmlBody { get; set; } = string.Empty;

    /// <summary>
    /// The template category (System or Business).
    /// </summary>
    public EmailTemplateCategory Category { get; set; }

    /// <summary>
    /// Whether the template is currently active and available for sending.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Comma-separated list of available placeholder variable names for this template.
    /// </summary>
    public string PlaceholderHints { get; set; } = string.Empty;

    /// <summary>
    /// The UTC timestamp when the template was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// The UTC timestamp when the template was last updated. Null if never updated.
    /// </summary>
    public DateTime? UpdatedAtUtc { get; set; }
}
