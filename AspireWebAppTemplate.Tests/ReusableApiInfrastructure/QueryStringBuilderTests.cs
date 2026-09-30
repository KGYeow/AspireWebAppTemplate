// Feature: reusable-api-infrastructure, Property 4: Query-string builder output equals the current hand-built string
using AspireWebAppTemplate.Application.Features.Announcements;
using AspireWebAppTemplate.Application.Features.Notifications;
using AspireWebAppTemplate.Application.Features.Users;
using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.Web.Utilities;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace AspireWebAppTemplate.Tests.ReusableApiInfrastructure;

/// <summary>
/// Property-based tests verifying that <see cref="QueryStringBuilder"/> reproduces the query strings
/// the typed API clients build by hand today, byte-for-byte, for the users, notifications, and
/// announcements list shapes.
/// </summary>
/// <remarks>
/// Each reference implementation mirrors the exact interpolation, escaping, and omission rules present
/// in <c>ApiUserService.GetUsersAsync</c> / <c>GetAllUsersAsync</c>,
/// <c>ApiNotificationService.GetNotificationsAsync</c>, and
/// <c>ApiAnnouncementService.GetForListPageAsync</c>: optional parameters are appended only when set,
/// whitespace-only search terms are omitted, string values are escaped with
/// <see cref="Uri.EscapeDataString(string)"/>, value types use their default string form, parts are
/// joined with '&amp;', and the '?' separator is emitted only when at least one part is present (for the
/// all-optional users shape) or unconditionally (for the notifications/announcements shapes that always
/// include page and pageSize).
/// **Validates: Requirements 2.9**
/// </remarks>
public class QueryStringBuilderTests
{
    #region Generators

    /// <summary>
    /// Generates optional page indices, including the unset (null) case.
    /// </summary>
    private static Gen<int?> OptionalPageGen() =>
        Gen.Elements<int?>(null, 0, 1, 5, 42);

    /// <summary>
    /// Generates search terms spanning unset, empty, whitespace-only, and escape-worthy values.
    /// </summary>
    private static Gen<string?> SearchTermGen() =>
        Gen.Elements<string?>(
            null,
            "",
            "   ",
            "alice",
            "john doe",
            "a&b=c",
            "quote\"x",
            "50% off",
            "café/ünïcode");

    #endregion

    #region Reference Implementations

    /// <summary>
    /// Reproduces the current hand-built URL from <c>ApiUserService.GetUsersAsync</c>.
    /// </summary>
    private static string ReferenceGetUsersUrl(UserQueryParams queryParams)
    {
        var queryStringParts = new List<string>();
        if (queryParams.Page.HasValue)
            queryStringParts.Add($"page={queryParams.Page.Value}");
        if (queryParams.PageSize.HasValue)
            queryStringParts.Add($"pageSize={queryParams.PageSize.Value}");
        if (!string.IsNullOrWhiteSpace(queryParams.SearchTerm))
            queryStringParts.Add($"searchTerm={Uri.EscapeDataString(queryParams.SearchTerm)}");
        return queryStringParts.Count > 0 ? $"/api/users?{string.Join("&", queryStringParts)}" : "/api/users";
    }

    /// <summary>
    /// Reproduces the current hand-built URL from <c>ApiUserService.GetAllUsersAsync</c>.
    /// </summary>
    private static string ReferenceGetAllUsersUrl(string? searchTerm)
    {
        var url = "/api/users";
        if (!string.IsNullOrWhiteSpace(searchTerm))
            url += $"?searchTerm={Uri.EscapeDataString(searchTerm)}";
        return url;
    }

    /// <summary>
    /// Reproduces the current hand-built URL from <c>ApiNotificationService.GetNotificationsAsync</c>.
    /// </summary>
    private static string ReferenceGetNotificationsUrl(NotificationQueryParams queryParams)
    {
        var url = $"/api/notifications?page={queryParams.Page}&pageSize={queryParams.PageSize}";
        if (queryParams.Category.HasValue)
            url += $"&category={queryParams.Category.Value}";
        if (queryParams.IsRead.HasValue)
            url += $"&isRead={queryParams.IsRead.Value}";
        return url;
    }

    /// <summary>
    /// Reproduces the current hand-built URL from <c>ApiAnnouncementService.GetForListPageAsync</c>.
    /// </summary>
    private static string ReferenceGetAnnouncementsUrl(AnnouncementQueryParams queryParams)
    {
        var url = $"/api/announcements/list?page={queryParams.Page}&pageSize={queryParams.PageSize}";
        if (queryParams.Severity.HasValue)
            url += $"&severity={queryParams.Severity.Value}";
        return url;
    }

    #endregion

    #region Builder-Under-Test Wrappers

    /// <summary>
    /// Builds the users list URL via <see cref="QueryStringBuilder"/>, mirroring the intended migration.
    /// </summary>
    private static string BuilderGetUsersUrl(UserQueryParams queryParams) =>
        QueryStringBuilder.Build("/api/users", qs =>
        {
            qs.AddIfHasValue("page", queryParams.Page);
            qs.AddIfHasValue("pageSize", queryParams.PageSize);
            qs.AddIfNotWhiteSpace("searchTerm", queryParams.SearchTerm);
        });

    /// <summary>
    /// Builds the "all users" URL via <see cref="QueryStringBuilder"/>, mirroring the intended migration.
    /// </summary>
    private static string BuilderGetAllUsersUrl(string? searchTerm) =>
        QueryStringBuilder.Build("/api/users", qs =>
            qs.AddIfNotWhiteSpace("searchTerm", searchTerm));

    /// <summary>
    /// Builds the notifications list URL via <see cref="QueryStringBuilder"/>, mirroring the intended migration.
    /// </summary>
    private static string BuilderGetNotificationsUrl(NotificationQueryParams queryParams) =>
        QueryStringBuilder.Build("/api/notifications", qs =>
        {
            qs.Add("page", queryParams.Page);
            qs.Add("pageSize", queryParams.PageSize);
            qs.AddIfHasValue("category", queryParams.Category);
            qs.AddIfHasValue("isRead", queryParams.IsRead);
        });

    /// <summary>
    /// Builds the announcements list URL via <see cref="QueryStringBuilder"/>, mirroring the intended migration.
    /// </summary>
    private static string BuilderGetAnnouncementsUrl(AnnouncementQueryParams queryParams) =>
        QueryStringBuilder.Build("/api/announcements/list", qs =>
        {
            qs.Add("page", queryParams.Page);
            qs.Add("pageSize", queryParams.PageSize);
            qs.AddIfHasValue("severity", queryParams.Severity);
        });

    #endregion

    #region Properties

    /// <summary>
    /// Property: for any combination of present/absent page, pageSize, and (escape-worthy) search term,
    /// the builder's users list URL equals the current hand-built <c>GetUsersAsync</c> URL.
    /// **Validates: Requirements 2.9**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property Users_GetUsers_MatchesHandBuiltString()
    {
        var gen = from page in OptionalPageGen()
                  from pageSize in OptionalPageGen()
                  from searchTerm in SearchTermGen()
                  select new UserQueryParams { Page = page, PageSize = pageSize, SearchTerm = searchTerm };

        return Prop.ForAll(Arb.From(gen), queryParams =>
        {
            var expected = ReferenceGetUsersUrl(queryParams);
            var actual = BuilderGetUsersUrl(queryParams);
            return (actual == expected).Label($"expected='{expected}', actual='{actual}'");
        });
    }

    /// <summary>
    /// Property: for any search term (including unset, empty, whitespace, and escape-worthy values), the
    /// builder's "all users" URL equals the current hand-built <c>GetAllUsersAsync</c> URL.
    /// **Validates: Requirements 2.9**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property Users_GetAllUsers_MatchesHandBuiltString()
    {
        return Prop.ForAll(Arb.From(SearchTermGen()), searchTerm =>
        {
            var expected = ReferenceGetAllUsersUrl(searchTerm);
            var actual = BuilderGetAllUsersUrl(searchTerm);
            return (actual == expected).Label($"expected='{expected}', actual='{actual}'");
        });
    }

    /// <summary>
    /// Property: for any page/pageSize with optional category and read-status filters, the builder's
    /// notifications list URL equals the current hand-built <c>GetNotificationsAsync</c> URL.
    /// **Validates: Requirements 2.9**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property Notifications_GetNotifications_MatchesHandBuiltString()
    {
        var pageGen = Gen.Elements(1, 2, 10, 100);
        var pageSizeGen = Gen.Elements(20, 50, 100);
        var categoryGen = Gen.Elements<NotificationCategory?>(
            null, NotificationCategory.System, NotificationCategory.Account, NotificationCategory.Activity);
        var isReadGen = Gen.Elements<bool?>(null, true, false);

        var gen = from page in pageGen
                  from pageSize in pageSizeGen
                  from category in categoryGen
                  from isRead in isReadGen
                  select new NotificationQueryParams
                  {
                      Page = page,
                      PageSize = pageSize,
                      Category = category,
                      IsRead = isRead
                  };

        return Prop.ForAll(Arb.From(gen), queryParams =>
        {
            var expected = ReferenceGetNotificationsUrl(queryParams);
            var actual = BuilderGetNotificationsUrl(queryParams);
            return (actual == expected).Label($"expected='{expected}', actual='{actual}'");
        });
    }

    /// <summary>
    /// Property: for any page/pageSize with an optional severity filter, the builder's announcements
    /// list URL equals the current hand-built <c>GetForListPageAsync</c> URL.
    /// **Validates: Requirements 2.9**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property Announcements_GetForListPage_MatchesHandBuiltString()
    {
        var pageGen = Gen.Elements(1, 2, 10, 100);
        var pageSizeGen = Gen.Elements(15, 30, 50);
        var severityGen = Gen.Elements<AnnouncementSeverity?>(
            null, AnnouncementSeverity.Info, AnnouncementSeverity.Warning, AnnouncementSeverity.Critical);

        var gen = from page in pageGen
                  from pageSize in pageSizeGen
                  from severity in severityGen
                  select new AnnouncementQueryParams { Page = page, PageSize = pageSize, Severity = severity };

        return Prop.ForAll(Arb.From(gen), queryParams =>
        {
            var expected = ReferenceGetAnnouncementsUrl(queryParams);
            var actual = BuilderGetAnnouncementsUrl(queryParams);
            return (actual == expected).Label($"expected='{expected}', actual='{actual}'");
        });
    }

    #endregion
}
