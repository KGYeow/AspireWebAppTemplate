using System.Collections.Generic;
using BlazorWebAppTemplate.Core.Application.Abstractions;
using BlazorWebAppTemplate.Core.Common;

namespace BlazorWebAppTemplate.Core.Application.Services;

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
        new() { Type = NavItemType.Header, Text = "Administration" },
        new()
        {
            Type = NavItemType.Group,
            Text = "Administration",
            Icon = "material-symbols-rounded/admin_panel_settings",
            AuthorizedOnly = true,
            Roles = "Admin",
            Children =
            [
                new() { Type = NavItemType.Link, Text = "User Management", Href = "user-management", Icon = "material-symbols-rounded/group" },
                new() { Type = NavItemType.Link, Text = "Role Management", Href = "role-management", Icon = "material-symbols-rounded/assignment_ind" },
                new() { Type = NavItemType.Link, Text = "Audit Log",       Href = "audit-log",       Icon = "material-symbols-rounded/history" },
            ]
        },
    ];
}