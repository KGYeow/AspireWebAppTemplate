using AspireWebAppTemplate.Core.Contracts.Notifications;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.Web.Abstractions;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Notifications;

/// <summary>
/// Full notification management page at /notifications.
/// Displays notifications in a list/feed layout with category and read status filtering,
/// "Load More" pagination, and visual distinction for unread items.
/// </summary>
/// <remarks>
/// Uses <see cref="ApiNotificationService"/> for data fetching and
/// <see cref="INotificationContext"/> for keeping the topbar badge in sync.
/// </remarks>
[Authorize]
public partial class Notifications : ComponentBase
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

    #endregion

    #region State

    /// <summary>
    /// Whether the page is performing its initial data load. Controls the PageContent loading wrapper.
    /// </summary>
    private bool _isLoading = true;

    /// <summary>
    /// Whether additional notifications are being loaded via the "Load More" button.
    /// </summary>
    private bool _isLoadingMore;

    /// <summary>
    /// The list of notifications currently displayed on the page.
    /// Appended to when "Load More" is clicked.
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
    /// Controls visibility of the "Load More" button.
    /// </summary>
    private bool _hasNextPage;

    /// <summary>
    /// The number of notifications to fetch per page.
    /// </summary>
    private const int PageSize = 20;

    /// <summary>
    /// Tracks selected notification IDs for bulk dismiss operations.
    /// </summary>
    private HashSet<Guid> _selectedIds = [];

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
    /// Handles the "Load More" button click.
    /// Increments the page and appends the next set of notifications to the existing list.
    /// </summary>
    private async Task LoadMoreAsync()
    {
        _currentPage++;
        _isLoadingMore = true;
        await LoadNotificationsAsync(resetList: false);
        _isLoadingMore = false;
    }

    /// <summary>
    /// Marks a single notification as read. Updates the item in the list and decrements the
    /// NotificationContext cached unread count.
    /// </summary>
    /// <param name="notification">The notification to mark as read.</param>
    private async Task HandleMarkAsRead(NotificationDto notification)
    {
        var result = await ApiNotificationService.MarkAsReadAsync(notification.Id);

        if (result.Succeeded)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
            NotificationContext.DecrementCount();
            Snackbar.Add("Notification marked as read.", Severity.Success);
        }
        else
        {
            Snackbar.Add("Failed to mark notification as read.", Severity.Error);
        }
    }

    /// <summary>
    /// Dismisses (deletes) a single notification. Removes it from the list and refreshes
    /// the NotificationContext since dismissed items might be unread.
    /// </summary>
    /// <param name="notification">The notification to dismiss.</param>
    private async Task HandleDismiss(NotificationDto notification)
    {
        var request = new BulkDismissRequest { NotificationIds = [notification.Id] };
        var result = await ApiNotificationService.BulkDismissAsync(request);

        if (result.Succeeded)
        {
            _notifications.Remove(notification);
            _selectedIds.Remove(notification.Id);
            await NotificationContext.RefreshAsync();
            Snackbar.Add("Notification dismissed.", Severity.Success);
        }
        else
        {
            Snackbar.Add("Failed to dismiss notification.", Severity.Error);
        }
    }

    /// <summary>
    /// Dismisses all currently selected notifications. Removes them from the list and refreshes
    /// the NotificationContext since dismissed items might be unread.
    /// </summary>
    private async Task HandleDismissSelected()
    {
        if (_selectedIds.Count == 0) return;

        var request = new BulkDismissRequest { NotificationIds = _selectedIds.ToList() };
        var result = await ApiNotificationService.BulkDismissAsync(request);

        if (result.Succeeded)
        {
            _notifications.RemoveAll(n => _selectedIds.Contains(n.Id));
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

    #endregion

    #region Helpers

    /// <summary>
    /// Loads notifications from the API using current filter and pagination state.
    /// </summary>
    /// <param name="resetList">
    /// When true, replaces the notification list (used for initial load and filter changes).
    /// When false, appends to the existing list (used for "Load More").
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
    /// Unread items receive a subtle background highlight.
    /// </summary>
    /// <param name="notification">The notification to style.</param>
    /// <returns>A CSS class string.</returns>
    private static string GetNotificationItemClass(NotificationDto notification)
    {
        var baseClass = "mb-2";
        return notification.IsRead ? baseClass : $"{baseClass} mud-theme-primary" + " notification-unread";
    }

    /// <summary>
    /// Returns the appropriate MudBlazor icon for a notification category.
    /// </summary>
    /// <param name="category">The notification category.</param>
    /// <returns>A MudBlazor icon string.</returns>
    private static string GetCategoryIcon(NotificationCategory category) => category switch
    {
        NotificationCategory.Security => Icons.Material.Outlined.Security,
        NotificationCategory.UserManagement => Icons.Material.Outlined.People,
        NotificationCategory.System => Icons.Material.Outlined.Info,
        _ => Icons.Material.Outlined.Notifications
    };

    /// <summary>
    /// Returns the appropriate color for a notification category icon.
    /// </summary>
    /// <param name="category">The notification category.</param>
    /// <returns>A MudBlazor Color value.</returns>
    private static Color GetCategoryColor(NotificationCategory category) => category switch
    {
        NotificationCategory.Security => Color.Error,
        NotificationCategory.UserManagement => Color.Primary,
        NotificationCategory.System => Color.Info,
        _ => Color.Default
    };

    /// <summary>
    /// Formats a UTC timestamp as a human-readable relative time string.
    /// Examples: "just now", "5 min ago", "2 hours ago", "yesterday", "3 days ago".
    /// Same logic as <see cref="Layout.Topbar.NotificationBell"/>.
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
