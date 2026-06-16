using AspireWebAppTemplate.Core.Domain.Enums;

namespace AspireWebAppTemplate.Core.Contracts.AuditLog;

/// <summary>
/// Query parameters for filtering and paging the audit log list.
/// Sent from the UI to the audit log query endpoint.
/// </summary>
public sealed class AuditLogQueryParams
{
    /// <summary>
    /// The zero-based page index to retrieve.
    /// </summary>
    public int Page { get; set; } = 0;

    /// <summary>
    /// The maximum number of entries per page.
    /// </summary>
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Optional free-text search term to filter entries by user name, entity name, or description.
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Optional filter by the type of action performed (e.g., Create, Update, Delete).
    /// </summary>
    public AuditActionType? ActionType { get; set; }

    /// <summary>
    /// Optional filter by the type of entity affected (e.g., User, Role).
    /// </summary>
    public AuditEntityType? EntityType { get; set; }

    /// <summary>
    /// Optional start date for the timestamp range filter (inclusive).
    /// </summary>
    public DateTime? DateStart { get; set; }

    /// <summary>
    /// Optional end date for the timestamp range filter (inclusive).
    /// </summary>
    public DateTime? DateEnd { get; set; }
}
