using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Web.Services.ApiClients;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Layout.Sidebar;

/// <summary>
/// Renders a navigation menu (headers, links, dividers, and groups) from a pre-filtered tree
/// fetched from the API. NavMenu is a pure renderer — it performs NO filtering logic.
/// The API's NavigationService handles all authentication filtering, permission filtering,
/// group visibility resolution, and orphan decoration removal.
/// </summary>
/// <remarks>
/// <para><b>Lifecycle:</b></para>
/// <list type="number">
///   <item>OnInitializedAsync calls <see cref="ApiNavigationService.GetFilteredNavigationAsync"/>.</item>
///   <item>While the call is in-flight, a loading skeleton (5 MudSkeleton elements) is shown.</item>
///   <item>On success, the received tree is rendered directly without any filtering.</item>
///   <item>On failure, an empty navigation state (zero items) is rendered.</item>
/// </list>
/// </remarks>
public partial class NavMenu : ComponentBase, IDisposable
{
    #region Injected Services

    /// <summary>
    /// Navigation services (base URI, current URI, navigate to, etc.)
    /// Used here only to refresh active states when the URL changes.
    /// </summary>
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Typed HTTP client service for fetching the pre-filtered navigation tree from the API.
    /// The API applies authentication filtering, permission filtering, group visibility,
    /// and orphan decoration removal — NavMenu simply renders the result.
    /// </summary>
    [Inject] private ApiNavigationService ApiNavigationService { get; set; } = default!;

    #endregion

    #region Parameters

    /// <summary>
    /// Whether the side drawer is open; affects header visibility and paddings.
    /// Also used as the default expansion state for groups when <see cref="NavItem.Expanded"/> is null.
    /// </summary>
    [Parameter] public bool DrawerOpen { get; set; }

    /// <summary>
    /// Default CSS class used for <see cref="NavItemType.Divider"/> items when the item does not specify a class.
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

    /// <summary>
    /// The current relative URL, tracked for active link highlighting.
    /// </summary>
    private string? _currentUrl;

    /// <summary>
    /// Whether the API call is currently in-flight. While true, a loading skeleton is shown.
    /// </summary>
    private bool _isLoading = true;

    /// <summary>
    /// The pre-filtered navigation items received from the API.
    /// Every item in this list is guaranteed to be visible — the render loop requires no additional checks.
    /// On API failure, this remains an empty list (zero items rendered).
    /// </summary>
    private List<NavItem> _navItems = [];

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

    /// <summary>
    /// Fetches the pre-filtered navigation tree from the API on component initialization.
    /// On success, stores the received items for direct rendering.
    /// On failure, renders empty navigation (zero items).
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        var result = await ApiNavigationService.GetFilteredNavigationAsync();

        if (result.Succeeded && result.Data is not null)
        {
            _navItems = result.Data;
        }
        // On failure: _navItems remains empty list (zero items rendered)

        _isLoading = false;
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

    #region Render Helpers

    /// <summary>
    /// Renders a single pre-approved navigation item. No permission checks are performed here —
    /// every item in <see cref="_navItems"/> has already passed all API-side filtering.
    /// </summary>
    /// <param name="item">The navigation item to render.</param>
    /// <param name="isRoot">True if rendering at the top level (affects header visibility only).</param>
    protected RenderFragment RenderItem(NavItem item, bool isRoot) => builder =>
    {
        var seq = 0;

        switch (item.Type)
        {
            case NavItemType.Header:
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
                builder.OpenComponent<MudNavLink>(seq++);
                builder.AddAttribute(seq++, "ActiveClass", ActiveClass);
                builder.AddAttribute(seq++, "Href", item.Href);
                builder.AddAttribute(seq++, "Match", MapMatch(item.Match));
                if (!string.IsNullOrWhiteSpace(item.Icon))
                {
                    builder.AddAttribute(seq++, "Icon", item.Icon);
                    builder.AddAttribute(seq++, "IconColor", DefaultIconColor);
                }
                builder.AddAttribute(seq++, "title", item.Title ?? item.Text);
                builder.AddAttribute(seq++, "ChildContent", (RenderFragment)(cc => cc.AddContent(0, item.Text)));
                builder.CloseComponent();
                break;

            case NavItemType.Group:
                var expanded = item.Expanded ?? true;
                builder.OpenComponent<MudNavGroup>(seq++);
                builder.AddAttribute(seq++, "Title", item.Text);
                builder.AddAttribute(seq++, "Expanded", expanded);
                builder.AddAttribute(seq++, "HideExpandIcon", !DrawerOpen);
                if (!string.IsNullOrWhiteSpace(item.Icon))
                {
                    builder.AddAttribute(seq++, "Icon", item.Icon);
                    builder.AddAttribute(seq++, "IconColor", DefaultIconColor);
                }
                builder.AddAttribute(seq++, "ChildContent", (RenderFragment)(childrenBuilder =>
                {
                    if (item.Children is { Count: > 0 })
                    {
                        foreach (var child in item.Children)
                        {
                            childrenBuilder.AddContent(0, RenderItem(child, isRoot: false));
                        }
                    }
                }));
                builder.CloseComponent();
                break;
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
