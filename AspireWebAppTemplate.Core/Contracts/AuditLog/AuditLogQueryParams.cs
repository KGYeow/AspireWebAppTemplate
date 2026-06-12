using AspireWebAppTemplate.Core.Domain.Enums;

namespace AspireWebAppTemplate.Core.Contracts.AuditLog;

public sealed class AuditLogQueryParams
{
    public int Page { get; set; } = 0;
    public int PageSize { get; set; } = 10;
    public string? SearchTerm { get; set; }
    public AuditActionType? ActionType { get; set; }
    public AuditEntityType? EntityType { get; set; }
    public DateTime? DateStart { get; set; }
    public DateTime? DateEnd { get; set; }
}
