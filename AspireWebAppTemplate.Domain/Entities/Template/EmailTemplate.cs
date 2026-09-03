using AspireWebAppTemplate.Domain.Enums;

namespace AspireWebAppTemplate.Domain.Entities.Template;

/// <summary>
/// Represents an email template stored in the database. All templates (system and business)
/// use the same structure with full Subject and HtmlBody content. The
/// <see cref="EmailTemplateCategory"/> determines editability at runtime — not storage location.
/// </summary>
/// <remarks>
/// Configured via <see cref="Configurations.EmailTemplateConfiguration"/> with:
/// <list type="bullet">
///   <item>Unique index on EmailType to enforce one template per EmailType.</item>
///   <item>Index on Category for efficient filtering in the admin list.</item>
///   <item>Enum properties stored as PascalCase strings to prevent data corruption if enum values are reordered.</item>
/// </list>
/// </remarks>
public class EmailTemplate
{
    /// <summary>
    /// Gets or sets the unique identifier for this email template.
    /// </summary>
    /// <remarks>
    /// Primary key. Generated as a new <see cref="Guid"/> upon creation.
    /// </remarks>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the email type this template represents.
    /// Each <see cref="EmailType"/> has exactly one template in the database.
    /// </summary>
    /// <remarks>
    /// Stored as a PascalCase string in the database via <c>HasConversion&lt;string&gt;()</c>
    /// to prevent data corruption if enum integer values are reordered. A unique index ensures
    /// the one-template-per-type invariant of the edit-only model.
    /// </remarks>
    public EmailType EmailType { get; set; }

    /// <summary>
    /// Gets or sets the human-readable display name shown in the admin UI.
    /// </summary>
    /// <remarks>
    /// Maximum length: 200 characters. Used as the label for the template in the admin
    /// data grid and edit dialogs.
    /// </remarks>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email subject line template. Supports <c>{{placeholder}}</c> syntax
    /// where placeholders are replaced with actual values at send time.
    /// </summary>
    /// <remarks>
    /// Maximum length: 500 characters.
    /// </remarks>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the HTML body template content. Supports <c>{{placeholder}}</c> syntax
    /// where placeholders are replaced with actual values at send time.
    /// All templates store full content in this field.
    /// </summary>
    /// <remarks>
    /// No maximum length constraint — allows full HTML email body content including
    /// inline styles, tables, and responsive layout markup.
    /// </remarks>
    public string HtmlBody { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the template category determining editability — not storage location.
    /// System = read-only at runtime, Business = admin-editable.
    /// </summary>
    /// <remarks>
    /// Stored as a PascalCase string in the database via <c>HasConversion&lt;string&gt;()</c>.
    /// <see cref="EmailTemplateCategory.System"/> templates are read-only at runtime.
    /// <see cref="EmailTemplateCategory.Business"/> templates are admin-editable at runtime.
    /// </remarks>
    public EmailTemplateCategory Category { get; set; }

    /// <summary>
    /// Gets or sets whether this template is active and available for use.
    /// Inactive templates cannot be used for sending emails.
    /// </summary>
    /// <remarks>
    /// Administrators can toggle this value via the admin UI to enable or disable
    /// business notification templates per deployment needs.
    /// </remarks>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets a comma-separated list of available placeholder variable names for this template.
    /// Displayed in the admin UI as guidance for editors.
    /// </summary>
    /// <remarks>
    /// Maximum length: 1000 characters. Example value: "UserName,ResetLink".
    /// These hints inform administrators which <c>{{placeholder}}</c> variables are
    /// available when editing the subject and body content.
    /// </remarks>
    public string PlaceholderHints { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the template was created.
    /// </summary>
    /// <remarks>
    /// Set once at creation time (during database seeding). Used for informational
    /// purposes in the admin UI.
    /// </remarks>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the template was last updated.
    /// Null if the template has never been updated after initial seeding.
    /// </summary>
    /// <remarks>
    /// Updated each time an administrator saves changes to the template via the admin UI.
    /// </remarks>
    public DateTime? UpdatedAtUtc { get; set; }
}
