using System.Collections.Generic;
using AspireWebAppTemplate.Core.Application.Abstractions;
using AspireWebAppTemplate.Core.Common;

namespace AspireWebAppTemplate.Core.Application.Services;

/// <summary>
/// Default, code-based navigation provider.
/// Move/extend this as your app grows, or replace with JSON/options-based provider.
/// </summary>
public sealed class DefaultNavigationProvider : INavigationProvider
{
    public IReadOnlyList<NavItem> GetMainMenuItems() =>
    [
        // Activity
        new() { Type = NavItemType.Header, Text = "Activity" },
        new() { Type = NavItemType.Link,   Text = "Home", Href = "", Icon = "material-symbols-rounded/home" },

        new() { Type = NavItemType.Divider, DividerClass = "my-2" },

        // Example
        new() { Type = NavItemType.Header, Text = "Example" },
        new()
        {
            Type = NavItemType.Group,
            Text = "Example",
            Icon = "material-symbols-rounded/apps",
            Children =
            [
                new() { Type = NavItemType.Link, Text = "Counter",     Href = "counter", Icon = "material-symbols-rounded/plus_one" },
                new() { Type = NavItemType.Link, Text = "Weather",     Href = "weather", Icon = "material-symbols-rounded/partly_cloudy_day" },
                new() { Type = NavItemType.Link, Text = "Auth Status", Href = "auth",    Icon = "material-symbols-rounded/lock" },
            ]
        },

        new() { Type = NavItemType.Divider, DividerClass = "my-2" },

        // Administration (Admin role only)
        new() { Type = NavItemType.Header, Text = "Administration", AuthorizedOnly = true },
        new() { Type = NavItemType.Link, Text = "User Management",  Href = "admin/user-management",  Icon = "material-symbols-rounded/group",           AuthorizedOnly = true },
        new() { Type = NavItemType.Link, Text = "Role Management",  Href = "admin/role-management",  Icon = "material-symbols-rounded/assignment_ind",  AuthorizedOnly = true },
        new() { Type = NavItemType.Link, Text = "Audit Log",        Href = "admin/audit-log",        Icon = "material-symbols-rounded/history",         AuthorizedOnly = true },
        new() { Type = NavItemType.Link, Text = "Page Permissions", Href = "admin/page-permissions", Icon = "material-symbols-rounded/lock",            AuthorizedOnly = true },
    ];
}