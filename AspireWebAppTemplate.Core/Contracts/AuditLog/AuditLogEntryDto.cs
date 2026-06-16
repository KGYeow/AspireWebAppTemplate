using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.Core.Utilities;

namespace AspireWebAppTemplate.Core.Contracts.AuditLog;

public sealed class AuditLogEntryDto
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = "";

    [ExportColumn(1)]
    [Display(Name = "User")]
    public string UserDisplayName { get; set; } = "";

    [ExportColumn(2)]
    [Display(Name = "Action Type")]
    public AuditActionType ActionType { get; set; }

    [ExportColumn(3)]
    [Display(Name = "Entity Type")]
    public AuditEntityType EntityType { get; set; }

    [ExportColumn(4)]
    [Display(Name = "Entity ID")]
    public string EntityId { get; set; } = "";

    [ExportColumn(5)]
    [Display(Name = "Entity Name")]
    public string EntityName { get; set; } = "";

    [ExportColumn(6)]
    [Display(Name = "Description")]
    public string Description { get; set; } = "";

    [ExportColumn(7, NullText = "")]
    [Display(Name = "Old Values")]
    public string? OldValues { get; set; }

    [ExportColumn(8, NullText = "")]
    [Display(Name = "New Values")]
    public string? NewValues { get; set; }

    [ExportColumn(9, NullText = "")]
    [Display(Name = "IP Address")]
    public string? IpAddress { get; set; }

    [ExportColumn(10)]
    [Display(Name = "Timestamp")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}")]
    public DateTime Timestamp { get; set; }
}
