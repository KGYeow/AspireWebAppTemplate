namespace AspireWebAppTemplate.Application.Common;

/// <summary>
/// Represents a paginated subset of query results.
/// Used by list endpoints that support server-side paging.
/// </summary>
/// <typeparam name="T">The type of items in the result set.</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>
    /// The items for the current page.
    /// </summary>
    public List<T> Items { get; set; } = [];

    /// <summary>
    /// The total number of items matching the query (across all pages).
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// The zero-based page index that was requested.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// The maximum number of items per page.
    /// </summary>
    public int PageSize { get; set; }
}
