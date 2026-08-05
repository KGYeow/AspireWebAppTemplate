namespace AspireWebAppTemplate.Application.Contracts.Email;

/// <summary>
/// Request DTO for previewing a rendered email template with sample placeholder values.
/// Used by the admin UI to display a preview of how the email will look.
/// </summary>
public sealed class PreviewTemplateRequest
{
    /// <summary>
    /// Dictionary of sample placeholder names to values used during preview rendering.
    /// Keys correspond to template placeholder names (e.g., "UserName", "ResetLink").
    /// </summary>
    public Dictionary<string, string> SampleData { get; set; } = new();
}
