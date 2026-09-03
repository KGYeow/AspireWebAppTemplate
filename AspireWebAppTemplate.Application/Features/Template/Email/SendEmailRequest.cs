using AspireWebAppTemplate.Domain.Enums;

namespace AspireWebAppTemplate.Application.Features.Template.Email;

/// <summary>
/// Request payload for sending an email of a specific type to a recipient.
/// The template is resolved from the database by EmailType and rendered
/// with the provided variables.
/// </summary>
public sealed class SendEmailRequest
{
    /// <summary>
    /// The email type that determines which template is resolved from the database.
    /// </summary>
    public EmailType EmailType { get; set; }

    /// <summary>
    /// The recipient's email address.
    /// </summary>
    public string RecipientEmail { get; set; } = "";

    /// <summary>
    /// Dictionary of placeholder names to values for template rendering.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = [];
}
