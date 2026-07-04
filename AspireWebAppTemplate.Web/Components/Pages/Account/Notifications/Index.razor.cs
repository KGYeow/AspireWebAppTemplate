using AspireWebAppTemplate.Core.Contracts.Notifications;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.UI.Components.Shared;
using AspireWebAppTemplate.UI.Utilities;
using AspireWebAppTemplate.Web.Abstractions;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Notifications;

/// <summary>
/// Full notification management page at /account/notifications.
/// Displays notifications in a responsive master-detail layout:
/// desktop shows a two-column split (list + detail panel),
/// mobile shows a single column with navigation between list and detail views.
/// </summary>
/// <remarks>
/// Uses <see cref="ApiNotificationService"/> for data fetching and
/// <see cref="INotificationContext"/> for keeping the topbar badge in sync.
/// </remarks>
[Authorize]
public partial class Index : ComponentBase, IAsyncDisposable
{
    #region Injected Services

    /// <summary>
    /// Typed HttpClient service for notification API operations.
    /// </summary>
    [Inject]
    private ApiNotificationService ApiNotificationService { get; set; } = default!;

    /// <summary>
    /// Per-circuit notification context for keeping the unread count badge in sync.
    /// </summary>
    [Inject]
    private INotificationContext NotificationContext { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions for programmatic page navigation.
    /// </summary>
    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

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
    /// Query parameter for deep-linking to a specific notification from the bell dropdown.
    /// </summary>
    [SupplyParameterFromQuery(Name = "id")]
    private string? HighlightedNotificationId { get; set; }

    #endregion

    #region State

    /// <summary>
    /// Whether the page is performing its initial data load. Controls the PageContent loading wrapper.
    /// </summary>
    private bool _isLoading = true;

    /// <summary>
    /// Whether additional notifications are being loaded via infinite scroll.
    /// </summary>
    private bool _isLoadingMore;

    /// <summary>
    /// The list of notifications currently displayed on the page.
    /// Appended to as the user scrolls down (infinite scroll).
    /// </summary>
    private List<NotificationDto> _notifications = [];

    /// <summary>
    /// The currently selected category filter. Null means "All" categories.
    /// </summary>
    private NotificationCategory? _selectedCategory;

    /// <summary>
    /// The currently selected read status filter. Null means "All", false means "Unread", true means "Read".
    /// </summary>
    private bool? _selectedReadStatus;

    /// <summary>
    /// The current page number for pagination (1-based).
    /// </summary>
    private int _currentPage = 1;

    /// <summary>
    /// Whether there are more notifications available beyond the current page.
    /// Controls visibility of the infinite scroll sentinel.
    /// </summary>
    private bool _hasNextPage;

    /// <summary>
    /// The number of notifications to fetch per page.
    /// </summary>
    private const int PageSize = 10;

    /// <summary>
    /// Tracks selected notification IDs for bulk dismiss operations.
    /// </summary>
    private HashSet<Guid> _selectedIds = [];

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

    /// <summary>
    /// The ID of the currently selected notification (showing full detail in the detail panel).
    /// Null when no notification is selected.
    /// </summary>
    private Guid? _expandedNotificationId;

    /// <summary>
    /// Indicates whether the current viewport is below the medium breakpoint.
    /// When true, the layout switches to a single-column mobile view.
    /// </summary>
    private bool _isSmallScreen => CurrentBreakpoint < Breakpoint.Md;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads the initial set of notifications from the API on page initialization.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await LoadNotificationsAsync(resetList: true);
        _isLoading = false;
    }

    /// <summary>
    /// Handles parameter changes including query string updates.
    /// If a notification ID is provided via query parameter, selects and marks it as read.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        if (Guid.TryParse(HighlightedNotificationId, out var notificationId))
        {
            _expandedNotificationId = notificationId;
            await MarkExpandedAsRead(notificationId);
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
            // Circuit disconnected during disposal (e.g., page refresh) — safe to ignore.
        }

        _dotNetRef?.Dispose();
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles category filter chip selection change.
    /// Reloads notifications from page 1 with the new filter applied.
    /// </summary>
    /// <param name="category">The selected category, or null for "All".</param>
    private async Task OnCategoryFilterChanged(NotificationCategory? category)
    {
        _selectedCategory = category;
        _currentPage = 1;
        await LoadNotificationsAsync(resetList: true);
    }

    /// <summary>
    /// Handles read status filter chip selection change.
    /// Reloads notifications from page 1 with the new filter applied.
    /// </summary>
    /// <param name="isRead">The selected read status filter, or null for "All".</param>
    private async Task OnReadStatusFilterChanged(bool? isRead)
    {
        _selectedReadStatus = isRead;
        _currentPage = 1;
        await LoadNotificationsAsync(resetList: true);
    }

    /// <summary>
    /// Handles clicking a notification row. Selects the notification in the detail panel
    /// and marks it as read if currently unread.
    /// </summary>
    /// <param name="notification">The notification that was clicked.</param>
    private async Task HandleNotificationClick(NotificationDto notification)
    {
        _expandedNotificationId = notification.Id;
        await MarkExpandedAsRead(notification.Id);
    }

    /// <summary>
    /// Marks a notification as unread. Updates the item in the list and increments the
    /// NotificationContext cached unread count.
    /// </summary>
    /// <param name="notification">The notification to mark as unread.</param>
    private async Task HandleMarkAsUnread(NotificationDto notification)
    {
        var result = await ApiNotificationService.MarkAsUnreadAsync(notification.Id);

        if (result.Succeeded)
        {
            notification.IsRead = false;
            notification.ReadAtUtc = null;
            await NotificationContext.RefreshAsync();
            Snackbar.Add("Notification marked as unread.", Severity.Success);
        }
        else
        {
            Snackbar.Add("Failed to mark notification as unread.", Severity.Error);
        }
    }

    /// <summary>
    /// Returns to the notification list view on small screens.
    /// Clears the selected notification so the list is displayed again.
    /// </summary>
    private void HandleBackToList()
    {
        _expandedNotificationId = null;
    }

    /// <summary>
    /// Marks a notification as read by its ID if it's currently unread.
    /// Used when selecting a notification (either via click or query parameter deep-link).
    /// </summary>
    /// <param name="notificationId">The ID of the notification to mark as read.</param>
    private async Task MarkExpandedAsRead(Guid notificationId)
    {
        var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
        if (notification is not null && !notification.IsRead)
        {
            var result = await ApiNotificationService.MarkAsReadAsync(notification.Id);
            if (result.Succeeded)
            {
                notification.IsRead = true;
                notification.ReadAtUtc = DateTime.UtcNow;
                NotificationContext.DecrementCount();
            }
        }
    }

    /// <summary>
    /// Dismisses (deletes) a single notification after user confirmation.
    /// Removes it from the list and refreshes the NotificationContext.
    /// </summary>
    /// <param name="notification">The notification to dismiss.</param>
    private async Task HandleDismiss(NotificationDto notification)
    {
        var parameters = new DialogParameters<ConfirmationDialog>
        {
            { x => x.ContentText, $"Are you sure you want to dismiss this notification? This action cannot be undone." },
            { x => x.SubmitBtnText, "Dismiss" },
            { x => x.DialogIcon, Icons.Material.Rounded.DeleteForever },
            { x => x.DialogIconColor, Color.Error }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.ExtraSmall, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Dismiss Notification", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        var request = new BulkDismissRequest { NotificationIds = [notification.Id] };
        var dismissResult = await ApiNotificationService.BulkDismissAsync(request);

        if (dismissResult.Succeeded)
        {
            _notifications.Remove(notification);
            _selectedIds.Remove(notification.Id);

            if (_expandedNotificationId == notification.Id)
            {
                _expandedNotificationId = null;
            }

            await NotificationContext.RefreshAsync();
            Snackbar.Add("Notification dismissed.", Severity.Success);
        }
        else
        {
            Snackbar.Add("Failed to dismiss notification.", Severity.Error);
        }
    }

    /// <summary>
    /// Dismisses all currently selected notifications after user confirmation.
    /// Removes them from the list and refreshes the NotificationContext.
    /// </summary>
    private async Task HandleDismissSelected()
    {
        if (_selectedIds.Count == 0) return;

        var parameters = new DialogParameters<ConfirmationDialog>
        {
            { x => x.ContentText, $"Are you sure you want to dismiss {_selectedIds.Count} notification(s)? This action cannot be undone." },
            { x => x.SubmitBtnText, "Dismiss All" },
            { x => x.DialogIcon, Icons.Material.Rounded.DeleteForever },
            { x => x.DialogIconColor, Color.Error }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.ExtraSmall, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Dismiss Notifications", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        var request = new BulkDismissRequest { NotificationIds = _selectedIds.ToList() };
        var dismissResult = await ApiNotificationService.BulkDismissAsync(request);

        if (dismissResult.Succeeded)
        {
            _notifications.RemoveAll(n => _selectedIds.Contains(n.Id));

            if (_expandedNotificationId.HasValue && _selectedIds.Contains(_expandedNotificationId.Value))
            {
                _expandedNotificationId = null;
            }

            _selectedIds.Clear();
            await NotificationContext.RefreshAsync();
            Snackbar.Add("Selected notifications dismissed.", Severity.Success);
        }
        else
        {
            Snackbar.Add("Failed to dismiss selected notifications.", Severity.Error);
        }
    }

    /// <summary>
    /// Marks all unread notifications as read. Updates all items in the list and clears
    /// the NotificationContext cached unread count.
    /// </summary>
    private async Task HandleMarkAllAsRead()
    {
        var result = await ApiNotificationService.MarkAllAsReadAsync();

        if (result.Succeeded)
        {
            foreach (var notification in _notifications.Where(n => !n.IsRead))
            {
                notification.IsRead = true;
                notification.ReadAtUtc = DateTime.UtcNow;
            }

            NotificationContext.ClearCount();
            Snackbar.Add($"All notifications marked as read.", Severity.Success);
        }
        else
        {
            Snackbar.Add("Failed to mark all notifications as read.", Severity.Error);
        }
    }

    /// <summary>
    /// Toggles a notification ID in the selected set for bulk operations.
    /// </summary>
    /// <param name="notificationId">The notification ID to toggle.</param>
    /// <param name="isSelected">Whether the item is being selected (true) or deselected (false).</param>
    private void HandleSelectionChanged(Guid notificationId, bool isSelected)
    {
        if (isSelected)
            _selectedIds.Add(notificationId);
        else
            _selectedIds.Remove(notificationId);
    }

    /// <summary>
    /// Clears all selected notification IDs.
    /// </summary>
    private void ClearSelection()
    {
        _selectedIds.Clear();
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
    /// Called by the JS Intersection Observer when the sentinel element becomes visible.
    /// Triggers loading the next page of notifications.
    /// </summary>
    [JSInvokable]
    public async Task OnScrolledToBottom()
    {
        if (_isLoadingMore || !_hasNextPage) return;

        _currentPage++;
        _isLoadingMore = true;
        StateHasChanged();

        await LoadNotificationsAsync(resetList: false);

        _isLoadingMore = false;
        StateHasChanged();

        // Re-setup observer if there are more pages
        if (_hasNextPage && _jsModule is not null)
        {
            await _jsModule.InvokeVoidAsync("initialize", _scrollSentinel, _dotNetRef);
        }
    }

    /// <summary>
    /// Loads notifications from the API using current filter and pagination state.
    /// </summary>
    /// <param name="resetList">
    /// When true, replaces the notification list (used for initial load and filter changes).
    /// When false, appends to the existing list (used for infinite scroll pagination).
    /// </param>
    private async Task LoadNotificationsAsync(bool resetList)
    {
        var queryParams = new NotificationQueryParams
        {
            Page = _currentPage,
            PageSize = PageSize,
            Category = _selectedCategory,
            IsRead = _selectedReadStatus
        };

        var result = await ApiNotificationService.GetNotificationsAsync(queryParams);

        if (result.Succeeded && result.Data is not null)
        {
            if (resetList)
            {
                _notifications = result.Data.Items;
            }
            else
            {
                _notifications.AddRange(result.Data.Items);
            }

            // Determine if there are more pages available
            var totalPages = (int)Math.Ceiling((double)result.Data.TotalCount / result.Data.PageSize);
            _hasNextPage = result.Data.Page < totalPages;
        }
        else
        {
            if (resetList)
            {
                _notifications = [];
                _hasNextPage = false;
            }

            Snackbar.Add("Failed to load notifications.", Severity.Error);
        }
    }

    /// <summary>
    /// Returns the CSS class string for a notification item.
    /// Applies highlight for unread items and a selected state for the active notification.
    /// </summary>
    /// <param name="notification">The notification to style.</param>
    /// <returns>A CSS class string.</returns>
    private string GetNotificationItemClass(NotificationDto notification)
    {
        var baseClass = "mb-2";

        if (_expandedNotificationId == notification.Id)
        {
            baseClass += " notification-selected";
        }
        else if (!notification.IsRead)
        {
            baseClass += " notification-unread";
        }

        return baseClass;
    }

    /// <summary>
    /// Returns the Material Symbols icon string for a notification category.
    /// </summary>
    /// <param name="category">The notification category.</param>
    /// <returns>A Material Symbols icon string.</returns>
    private static string GetCategoryIcon(NotificationCategory category) =>
        NotificationCategoryHelper.GetIcon(category.ToString());

    /// <summary>
    /// Returns the MudBlazor CSS class for a notification category's color.
    /// </summary>
    /// <param name="category">The notification category.</param>
    /// <returns>A MudBlazor CSS class string.</returns>
    private static string GetCategoryColor(NotificationCategory category) =>
        NotificationCategoryHelper.GetColorClass(category.ToString());

    /// <summary>
    /// Formats a UTC timestamp as a human-readable relative time string.
    /// Examples: "just now", "5 min ago", "2 hours ago", "yesterday", "3 days ago".
    /// </summary>
    /// <param name="utcTimestamp">The UTC timestamp to format.</param>
    /// <returns>A relative time string.</returns>
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
