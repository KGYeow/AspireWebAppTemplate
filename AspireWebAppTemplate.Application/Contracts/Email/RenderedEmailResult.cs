namespace AspireWebAppTemplate.Application.Contracts.Email;

/// <summary>
/// Represents the output of rendering an email template — the resolved subject line
/// and fully-rendered HTML body ready for sending.
/// </summary>
public sealed class RenderedEmailResult
{
    /// <summary>
    /// The rendered subject line with all placeholders replaced.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// The rendered HTML body with all placeholders replaced.
    /// </summary>
    public string HtmlBody { get; set; } = string.Empty;
}
