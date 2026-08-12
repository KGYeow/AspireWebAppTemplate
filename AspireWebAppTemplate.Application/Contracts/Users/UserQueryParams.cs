namespace AspireWebAppTemplate.Application.Contracts.Users;

/// <summary>
/// Query parameters for paginated user search. Supports filtering by
/// a search term matched against username, display name, email,
/// first name, last name, and department fields.
/// </summary>
public sealed class UserQueryParams
{
    /// <summary>
    /// The zero-based page index to retrieve. When null, no pagination is applied.
    /// </summary>
    public int? Page { get; set; }

    /// <summary>
    /// The maximum number of users per page. When null, no pagination is applied.
    /// </summary>
    public int? PageSize { get; set; }

    /// <summary>
    /// Optional search term for case-insensitive partial matching against user fields.
    /// When null or empty, all users are returned.
    /// </summary>
    public string? SearchTerm { get; set; }
}