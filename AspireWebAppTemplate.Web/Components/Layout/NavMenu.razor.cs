using BlazorWebAppTemplate.Core.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;

namespace BlazorWebAppTemplate.Components.Layout;

/// <summary>
/// Renders a navigation menu (headers, links, dividers, and groups) from a single, unified model
/// defined in Core. Supports nested groups and per-item authorization visibility.
/// </summary>
public partial class NavMenu : ComponentBase, IDisposable
{
    #region Injected Services

    /// <summary>
    /// Navigation services (base URI, current URI, navigate to, etc.)
    /// Used here only to refresh active states when the URL changes.
    /// </summary>
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;

    #endregion

    #region Parameters

    /// <summary>
    /// Unified list of navigation items to render (headers, links, dividers, groups).
    /// Defaults to an empty list. If a parent assigns <c>null</c> at runtime, this property
    /// may become <c>null</c>; callers should null‑coalesce when enumerating.
    /// </summary>
    [Parameter]
    public IReadOnlyList<NavItem> Items { get; set; } = [];

    /// <summary>
    /// Whether the side drawer is open; affects header visibility and paddings.
    /// Also used as the default expansion state for groups when <see cref="AppNavItem.Expanded"/> is null.
    /// </summary>
    [Parameter] public bool DrawerOpen { get; set; }

    /// <summary>
    /// Default CSS class used for <see cref="AppNavItemType.Divider"/> items when the item does not specify a class.
    /// </summary>
    [Parameter] public string DividerClassDefault { get; set; } = "my-2";

    #endregion

    #region Fields / Properties

    /// <summary>
    /// CSS class applied to the active <see cref="MudNavLink"/>.
    /// </summary>
    protected string ActiveClass { get; } = "indigo lighten-1";

    /// <summary>
    /// Default icon color for <see cref="MudNavLink"/> and <see cref="MudNavGroup"/> icons.
    /// Only applied when a non-empty icon is provided on the item.
    /// </summary>
    protected Color DefaultIconColor { get; } = Color.Surface;

    /// <summary>
    /// Inline style for smooth transitions.
    /// </summary>
    protected string NavMenuStyle { get; } = "transition: all 200ms ease-in-out";

    /// <summary>
    /// Computed class for the outer <see cref="MudNavMenu"/>.
    /// </summary>
    protected string NavMenuClass => $"overflow-auto py-2 {(DrawerOpen ? "px-2" : "")}";

    #endregion

    #region State

    private string? _currentUrl;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the component: starts listening to URL changes to refresh active link states.
    /// </summary>
    protected override void OnInitialized()
    {
        _currentUrl = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
        NavigationManager.LocationChanged += OnLocationChanged;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles URL changes to refresh the active state on nav links.
    /// </summary>
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        _currentUrl = NavigationManager.ToBaseRelativePath(e.Location);
        _ = InvokeAsync(StateHasChanged);
    }

    #endregion

    #region Render Helpers (recursive)

    /// <summary>
    /// Recursively renders any nav item (header, divider, link, or group).
    /// </summary>
    /// <param name="item">The item to render.</param>
    /// <param name="isRoot">True if rendering at the top level (affects header visibility only).</param>
    protected RenderFragment RenderItem(NavItem item, bool isRoot) => builder =>
    {
        var seq = 0;

        switch (item.Type)
        {
            case NavItemType.Header:
                // Render header only when drawer is open
                if (DrawerOpen)
                {
                    builder.OpenComponent<MudText>(seq++);
                    builder.AddAttribute(seq++, "Typo", Typo.caption);
                    builder.AddAttribute(seq++, "Class", "d-block px-4 py-2 text-white-50");
                    builder.AddAttribute(seq++, "ChildContent", (RenderFragment)(cc => cc.AddContent(0, item.Text)));
                    builder.CloseComponent();
                }
                break;

            case NavItemType.Divider:
                builder.OpenComponent<MudDivider>(seq++);
                builder.AddAttribute(seq++, "Class", string.IsNullOrWhiteSpace(item.DividerClass) ? DividerClassDefault : item.DividerClass);
                builder.CloseComponent();
                break;

            case NavItemType.Link:
                {
                    var linkFragment = (RenderFragment)(lb =>
                    {
                        var s = 0;
                        lb.OpenComponent<MudNavLink>(s++);
                        lb.AddAttribute(s++, "ActiveClass", ActiveClass);
                        lb.AddAttribute(s++, "Href", item.Href);
                        lb.AddAttribute(s++, "Match", MapMatch(item.Match));
                        if (!string.IsNullOrWhiteSpace(item.Icon))
                        {
                            lb.AddAttribute(s++, "Icon", item.Icon);
                            lb.AddAttribute(s++, "IconColor", DefaultIconColor);
                        }
                        lb.AddAttribute(s++, "title", item.Title ?? item.Text);
                        lb.AddAttribute(s++, "ChildContent", (RenderFragment)(cc => cc.AddContent(0, item.Text)));
                        lb.CloseComponent();
                    });

                    var wrapped = WrapWithAuth(item.AuthorizedOnly, item.NotAuthorizedOnly, linkFragment, item.Roles);
                    builder.AddContent(seq++, wrapped);
                    break;
                }

            case NavItemType.Group:
                {
                    var expanded = item.Expanded ?? true;

                    var groupFragment = (RenderFragment)(gb =>
                    {
                        var s = 0;
                        gb.OpenComponent<MudNavGroup>(s++);
                        gb.AddAttribute(s++, "Title", item.Text);
                        gb.AddAttribute(s++, "Expanded", expanded);
                        gb.AddAttribute(s++, "HideExpandIcon", !DrawerOpen);
                        if (!string.IsNullOrWhiteSpace(item.Icon))
                        {
                            gb.AddAttribute(s++, "Icon", item.Icon);
                            gb.AddAttribute(s++, "IconColor", DefaultIconColor);
                        }

                        // Render children recursively (any type, including nested groups)
                        gb.AddAttribute(s++, "ChildContent", (RenderFragment)(childrenBuilder =>
                        {
                            if (item.Children is { Count: > 0 })
                            {
                                foreach (var child in item.Children)
                                {
                                    childrenBuilder.AddContent(0, RenderItem(child, isRoot: false));
                                }
                            }
                        }));
                        gb.CloseComponent();
                    });

                    var wrapped = WrapWithAuth(item.AuthorizedOnly, item.NotAuthorizedOnly, groupFragment, item.Roles);
                    builder.AddContent(seq++, wrapped);
                    break;
                }
        }
    };

    /// <summary>
    /// Wraps a fragment in <see cref="AuthorizeView"/> when authorized-only or not-authorized-only flags are set.
    /// Supports role-based filtering via <see cref="NavItem.Roles"/>.
    /// </summary>
    protected RenderFragment WrapWithAuth(bool authorizedOnly, bool notAuthorizedOnly, RenderFragment inner, string? roles = null)
        => (authorizedOnly || !string.IsNullOrEmpty(roles), notAuthorizedOnly) switch
        {
            // No wrapping needed
            (false, false) => inner,

            // Authorized-only (optionally with roles)
            (true, _) => builder =>
            {
                builder.OpenComponent<AuthorizeView>(0);
                if (!string.IsNullOrEmpty(roles))
                {
                    builder.AddAttribute(1, "Roles", roles);
                }
                builder.AddAttribute(2, "Authorized",
                    (RenderFragment<AuthenticationState>)(ctx => __builder =>
                    {
                        __builder.AddContent(0, inner);
                    }));
                builder.CloseComponent();
            }
            ,

            // NotAuthorized-only
            (false, true) => builder =>
            {
                builder.OpenComponent<AuthorizeView>(0);
                builder.AddAttribute(1, "NotAuthorized",
                    (RenderFragment<AuthenticationState>)(ctx => __builder =>
                    {
                        __builder.AddContent(0, inner);
                    }));
                builder.CloseComponent();
            }
        };

    /// <summary>
    /// Maps the Core model's <see cref="NavMatch"/> to MudBlazor's <see cref="NavLinkMatch"/>.
    /// </summary>
    private static NavLinkMatch MapMatch(NavMatch match)
        => match == NavMatch.Prefix ? NavLinkMatch.Prefix : NavLinkMatch.All;

    #endregion

    #region IDisposable

    /// <summary>
    /// Unsubscribes from navigation events to avoid memory leaks.
    /// </summary>
    public void Dispose()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
        GC.SuppressFinalize(this);
    }

    #endregion
}