using System.Text.RegularExpressions;
using AspireWebAppTemplate.Application.Features.Template.Announcements;
using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.Web.Abstractions;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Announcements;

/// <summary>
/// Public announcement browsing page at /announcements.
/// Displays announcements in a responsive master-detail layout matching the Notification page pattern:
/// desktop shows a two-column split (list + detail panel),
/// mobile shows a single column with navigation between list and detail views.
/// Supports severity filtering and infinite scroll via server-side pagination.
/// </summary>
[Authorize]
public partial class Index : ComponentBase, IAsyncDisposable
{
    #region Injected Services

    /// <summary>
    /// Typed HttpClient service for announcement API operations.
    /// </summary>
    [Inject]
    private ApiAnnouncementService ApiAnnouncementService { get; set; } = default!;

    /// <summary>
    /// Provides user-aware datetime formatting in the viewer's configured time zone.
    /// </summary>
    [Inject]
    private IUserTimeZoneContext UserTimeZone { get; set; } = default!;

    /// <summary>
    /// JavaScript runtime for infinite scroll Intersection Observer setup.
    /// </summary>
    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    #endregion

    #region Parameters

    /// <summary>
    /// The current viewport breakpoint, cascaded from MainLayout via MudBreakpointProvider.
    /// Used to determine responsive layout behavior (list-detail vs single-column).
    /// </summary>
    [CascadingParameter]
    private Breakpoint CurrentBreakpoint { get; set; }

    /// <summary>
    /// Query parameter for deep-linking to a specific announcement (e.g., from a notification snackbar).
    /// </summary>
    [SupplyParameterFromQuery(Name = "id")]
    private string? HighlightedAnnouncementId { get; set; }

    #endregion

    #region State

    /// <summary>
    /// Whether the page is performing its initial data load. Controls the PageContent loading wrapper.
    /// </summary>
    private bool _isLoading = true;

    /// <summary>
    /// Whether additional announcements are being loaded via infinite scroll.
    /// </summary>
    private bool _isLoadingMore;

    /// <summary>
    /// The list of announcements currently displayed on the page.
    /// Appended to as the user scrolls down (infinite scroll).
    /// </summary>
    private List<AnnouncementDto> _announcements = [];

    /// <summary>
    /// The currently selected severity filter. Null means "All" severities.
    /// </summary>
    private AnnouncementSeverity? _selectedSeverity;

    /// <summary>
    /// The current page number for pagination (1-based).
    /// </summary>
    private int _currentPage = 1;

    /// <summary>
    /// Whether there are more announcements available beyond the current page.
    /// Controls visibility of the infinite scroll sentinel.
    /// </summary>
    private bool _hasNextPage;

    /// <summary>
    /// The number of announcements to fetch per page.
    /// </summary>
    private const int PageSize = 15;

    /// <summary>
    /// The ID of the currently selected announcement (showing full detail in the detail panel).
    /// Null when no announcement is selected.
    /// </summary>
    private Guid? _selectedAnnouncementId;

    /// <summary>
    /// Indicates whether the current viewport is below the medium breakpoint.
    /// When true, the layout switches to a single-column mobile view.
    /// </summary>
    private bool _isSmallScreen => CurrentBreakpoint < Breakpoint.Md;

    /// <summary>
    /// Compiled regex for stripping HTML tags from announcement content.
    /// </summary>
    private static readonly Regex HtmlTagRegex = new("<[^>]*>", RegexOptions.Compiled);

    /// <summary>
    /// Element reference for the infinite scroll sentinel div.
    /// </summary>
    private ElementReference _scrollSentinel;

    /// <summary>
    /// JavaScript module reference for infinite scroll Intersection Observer.
    /// </summary>
    private IJSObjectReference? _jsModule;

    /// <summary>
    /// .NET object reference passed to JS for callback invocation.
    /// </summary>
    private DotNetObjectReference<Index>? _dotNetRef;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads the initial set of announcements from the API on page initialization.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await LoadAnnouncementsAsync(resetList: true);
        _isLoading = false;
    }

    /// <summary>
    /// Handles parameter changes including query string updates.
    /// If an announcement ID is provided via query parameter, selects it.
    /// </summary>
    protected override void OnParametersSet()
    {
        if (Guid.TryParse(HighlightedAnnouncementId, out var announcementId))
        {
            _selectedAnnouncementId = announcementId;
        }
    }

    /// <summary>
    /// Sets up the Intersection Observer for infinite scroll after the component renders.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_hasNextPage && _jsModule is null)
        {
            await SetupInfiniteScroll();
        }
    }

    /// <summary>
    /// Disposes the JS module and .NET object reference.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_jsModule is not null)
            {
                await _jsModule.InvokeVoidAsync("dispose");
                await _jsModule.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected during disposal — safe to ignore.
        }

        _dotNetRef?.Dispose();
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles severity filter selection change.
    /// Reloads announcements from page 1 with the new filter applied.
    /// </summary>
    private async Task OnSeverityFilterChanged(AnnouncementSeverity? severity)
    {
        _selectedSeverity = severity;
        _currentPage = 1;
        await LoadAnnouncementsAsync(resetList: true);
    }

    /// <summary>
    /// Handles clicking an announcement in the list.
    /// Selects it for detail view.
    /// </summary>
    private void HandleAnnouncementClick(AnnouncementDto announcement)
    {
        _selectedAnnouncementId = announcement.Id;
    }

    /// <summary>
    /// Returns to the announcement list view on small screens.
    /// </summary>
    private void HandleBackToList()
    {
        _selectedAnnouncementId = null;
    }

    /// <summary>
    /// Called by the JS Intersection Observer when the sentinel element becomes visible.
    /// Triggers loading the next page of announcements.
    /// </summary>
    [JSInvokable]
    public async Task OnScrolledToBottom()
    {
        if (_isLoadingMore || !_hasNextPage) return;

        _currentPage++;
        _isLoadingMore = true;
        StateHasChanged();

        await LoadAnnouncementsAsync(resetList: false);

        _isLoadingMore = false;
        StateHasChanged();

        // Re-setup observer if there are more pages
        if (_hasNextPage && _jsModule is not null)
        {
            await _jsModule.InvokeVoidAsync("initialize", _scrollSentinel, _dotNetRef);
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Sets up the Intersection Observer for infinite scroll on the sentinel element.
    /// </summary>
    private async Task SetupInfiniteScroll()
    {
        _dotNetRef = DotNetObjectReference.Create(this);
        _jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/infinite-scroll.js");
        await _jsModule.InvokeVoidAsync("initialize", _scrollSentinel, _dotNetRef);
    }

    /// <summary>
    /// Loads announcements from the API using current filter and pagination state.
    /// </summary>
    /// <param name="resetList">
    /// When true, replaces the announcement list (used for initial load and filter changes).
    /// When false, appends to the existing list (used for infinite scroll pagination).
    /// </param>
    private async Task LoadAnnouncementsAsync(bool resetList)
    {
        var queryParams = new AnnouncementQueryParams
        {
            Page = _currentPage,
            PageSize = PageSize,
            Severity = _selectedSeverity
        };

        var result = await ApiAnnouncementService.GetForListPageAsync(queryParams);

        if (result.Succeeded && result.Data is not null)
        {
            if (resetList)
            {
                _announcements = result.Data.Items;
            }
            else
            {
                _announcements.AddRange(result.Data.Items);
            }

            // Determine if there are more pages available
            var totalPages = (int)Math.Ceiling((double)result.Data.TotalCount / result.Data.PageSize);
            _hasNextPage = result.Data.Page < totalPages;
        }
        else
        {
            if (resetList)
            {
                _announcements = [];
                _hasNextPage = false;
            }

            Snackbar.Add("Failed to load announcements.", Severity.Error);
        }

        // Auto-select first item if nothing is selected
        if (resetList && _selectedAnnouncementId is null && _announcements.Count > 0)
        {
            _selectedAnnouncementId = _announcements[0].Id;
        }
    }

    /// <summary>
    /// Returns the CSS class string for an announcement list item.
    /// Applies the notification-selected pattern for the active selection.
    /// </summary>
    private string GetAnnouncementItemClass(AnnouncementDto announcement)
    {
        var baseClass = "mb-2";

        if (_selectedAnnouncementId == announcement.Id)
        {
            baseClass += " notification-selected";
        }

        return baseClass;
    }

    /// <summary>
    /// Returns the MudBlazor color for an announcement's severity level.
    /// </summary>
    private static Color GetSeverityColor(AnnouncementSeverity severity) => severity switch
    {
        AnnouncementSeverity.Info => Color.Info,
        AnnouncementSeverity.Warning => Color.Warning,
        AnnouncementSeverity.Critical => Color.Error,
        _ => Color.Default
    };

    /// <summary>
    /// Determines whether an announcement is expired based on its Status field.
    /// </summary>
    private static bool IsExpired(AnnouncementDto announcement) =>
        announcement.Status.Equals("Expired", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Strips HTML tags from content to produce a plain-text excerpt.
    /// </summary>
    private static string StripHtml(string html) =>
        HtmlTagRegex.Replace(html, "").Trim();

    /// <summary>
    /// Formats a UTC timestamp as a human-readable relative time string.
    /// </summary>
    private static string FormatRelativeTime(DateTime utcTimestamp)
    {
        var elapsed = DateTime.UtcNow - utcTimestamp;

        if (elapsed.TotalSeconds < 60)
            return "just now";

        if (elapsed.TotalMinutes < 60)
            return $"{(int)elapsed.TotalMinutes} min ago";

        if (elapsed.TotalHours < 24)
        {
            var hours = (int)elapsed.TotalHours;
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }

        if (elapsed.TotalDays < 2)
            return "yesterday";

        if (elapsed.TotalDays < 7)
            return $"{(int)elapsed.TotalDays} days ago";

        if (elapsed.TotalDays < 30)
        {
            var weeks = (int)(elapsed.TotalDays / 7);
            return weeks == 1 ? "1 week ago" : $"{weeks} weeks ago";
        }

        if (elapsed.TotalDays < 365)
        {
            var months = (int)(elapsed.TotalDays / 30);
            return months == 1 ? "1 month ago" : $"{months} months ago";
        }

        var years = (int)(elapsed.TotalDays / 365);
        return years == 1 ? "1 year ago" : $"{years} years ago";
    }

    #endregion
}
