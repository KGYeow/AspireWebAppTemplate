using AspireWebAppTemplate.Domain.Enums;

namespace AspireWebAppTemplate.Application.Features.Template.Announcements;

/// <summary>
/// Query parameters for paginated announcement retrieval on the user-facing list page.
/// Sent from the UI to the announcement list endpoint.
/// </summary>
public sealed class AnnouncementQueryParams
{
    /// <summary>
    /// The one-based page index to retrieve. Defaults to 1.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// The maximum number of announcements per page. Defaults to 15, maximum 50.
    /// </summary>
    public int PageSize { get; set; } = 15;

    /// <summary>
    /// Optional filter by announcement severity. When null, all severities are returned.
    /// </summary>
    public AnnouncementSeverity? Severity { get; set; }
}
