using System.Collections.Generic;

namespace AspireWebAppTemplate.Application.Common;

/// <summary>
/// The kind of navigation item to render.
/// </summary>
public enum NavItemType
{
    /// <summary>
    /// Renders a small caption (section header).
    /// </summary>
    Header,

    /// <summary>
    /// Renders a clickable navigation link.
    /// </summary>
    Link,

    /// <summary>
    /// Renders a horizontal divider.
    /// </summary>
    Divider,

    /// <summary>
    /// Renders a collapsible group that contains child items.
    /// </summary>
    Group
}

/// <summary>
/// Link match behavior (UI-agnostic). UI layer will adapt this to control active link matching.
/// </summary>
public enum NavMatch
{
    /// <summary>
    /// Match prefix (starts with).
    /// </summary>
    Prefix,

    /// <summary>
    /// Match exact path only.
    /// </summary>
    Exact
}

/// <summary>
/// A single navigation item (header, link, divider, or group).
/// UI-agnostic model usable from any assembly or configuration binder.
/// </summary>
public sealed class NavItem
{
    /// <summary>
    /// Item type.
    /// </summary>
    public NavItemType Type { get; init; }

    /// <summary>
    /// Displayed text (header caption, link label, or group title). Ignored for Divider.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    // ---- Link-only ----
    /// <summary>
    /// Target URI for a link. Ignored for Header/Divider/Group.
    /// </summary>
    public string? Href { get; init; }

    /// <summary>
    /// Optional tooltip/title for the link. Falls back to <see cref="Text"/> if null.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Link match behavior; defaults to Exact.
    /// </summary>
    public NavMatch Match { get; init; } = NavMatch.Exact;

    /// <summary>
    /// Optional icon name (e.g., "material-symbols-rounded/home").
    /// </summary>
    public string? Icon { get; init; }

    // ---- Authorization visibility (for Link or Group) ----
    /// <summary>
    /// Show only to authenticated users.
    /// </summary>
    public bool AuthorizedOnly { get; init; }

    /// <summary>
    /// Show only to anonymous users.
    /// </summary>
    public bool NotAuthorizedOnly { get; init; }

    // ---- Divider-only ----
    /// <summary>
    /// Optional CSS class when rendered as a divider.
    /// </summary>
    public string? DividerClass { get; init; }

    // ---- Group-only ----
    /// <summary>
    /// Children to render inside a group (may include nested groups).
    /// </summary>
    public IReadOnlyList<NavItem>? Children { get; init; }

    /// <summary>
    /// Whether the group is expanded by default. If null, UI can decide (e.g., relative to drawer state).
    /// </summary>
    public bool? Expanded { get; init; }
}
