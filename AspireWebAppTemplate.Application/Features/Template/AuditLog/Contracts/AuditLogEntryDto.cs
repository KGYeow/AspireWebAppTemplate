using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.Domain.Attributes;

namespace AspireWebAppTemplate.Application.Features.Template.AuditLog.Contracts;

/// <summary>
/// Data transfer object representing a single audit log entry.
/// Returned by the audit log query endpoint and used for Excel/CSV export.
/// </summary>
public sealed class AuditLogEntryDto
{
    /// <summary>
    /// The unique identifier of this audit log entry.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The identifier of the user who performed the action.
    /// </summary>
    public string UserId { get; set; } = "";

    /// <summary>
    /// The display name of the user who performed the action.
    /// </summary>
    [ExportColumn(1)]
    [Display(Name = "User")]
    public string UserDisplayName { get; set; } = "";

    /// <summary>
    /// The type of action performed (e.g., Create, Update, Delete).
    /// </summary>
    [ExportColumn(2)]
    [Display(Name = "Action Type")]
    public AuditActionType ActionType { get; set; }

    /// <summary>
    /// The type of entity affected by the action (e.g., User, Role).
    /// </summary>
    [ExportColumn(3)]
    [Display(Name = "Entity Type")]
    public AuditEntityType EntityType { get; set; }

    /// <summary>
    /// The identifier of the affected entity.
    /// </summary>
    [ExportColumn(4)]
    [Display(Name = "Entity ID")]
    public string EntityId { get; set; } = "";

    /// <summary>
    /// The human-readable name of the affected entity.
    /// </summary>
    [ExportColumn(5)]
    [Display(Name = "Entity Name")]
    public string EntityName { get; set; } = "";

    /// <summary>
    /// A human-readable description of what occurred.
    /// </summary>
    [ExportColumn(6)]
    [Display(Name = "Description")]
    public string Description { get; set; } = "";

    /// <summary>
    /// JSON-serialized previous values before the change, if applicable.
    /// </summary>
    [ExportColumn(7, NullText = "")]
    [Display(Name = "Old Values")]
    public string? OldValues { get; set; }

    /// <summary>
    /// JSON-serialized new values after the change, if applicable.
    /// </summary>
    [ExportColumn(8, NullText = "")]
    [Display(Name = "New Values")]
    public string? NewValues { get; set; }

    /// <summary>
    /// The IP address from which the action was performed.
    /// </summary>
    [ExportColumn(9, NullText = "")]
    [Display(Name = "IP Address")]
    public string? IpAddress { get; set; }

    /// <summary>
    /// The UTC timestamp when the action occurred.
    /// </summary>
    [ExportColumn(10)]
    [Display(Name = "Timestamp")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}")]
    public DateTime Timestamp { get; set; }
}
