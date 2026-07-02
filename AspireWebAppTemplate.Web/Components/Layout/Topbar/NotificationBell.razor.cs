using AspireWebAppTemplate.Core.Contracts.Notifications;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.Web.Abstractions;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Layout.Topbar;

/// <summary>
/// Notification bell icon with unread count badge and dropdown popover.
/// Displays the 10 most recent notifications with title, category icon, and relative timestamp.
/// Subscribes to <see cref="INotificationContext.OnChange"/> for real-time badge updates.
/// </summary>
public partial class NotificationBell : ComponentBase, IDisposable
{
    #region Injected Services

    /// <summary>
    /// Per-circuit notification context providing cached unread count and change notifications.
    /// </summary>
    [Inject]
    private INotificationContext NotificationContext { get; set; } = default!;

    /// <summary>
    /// Typed HttpClient service for notification API operations (fetching recent, marking as read).
    /// </summary>
    [Inject]
    private ApiNotificationService ApiNotificationService { get; set; } = default!;

    /// <summary>
    /// Provides navigation utilities for programmatic page navigation.
    /// </summary>
    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// The cached unread notification count displayed in the badge.
    /// </summary>
    private int _unreadCount;

    /// <summary>
    /// Reference to the MudMenu component for programmatic close on navigation.
    /// </summary>
    private MudMenu _menuRef = default!;

    /// <summary>
    /// The list of most recent notifications displayed in the dropdown.
    /// Null before the first load.
    /// </summary>
    private List<NotificationDto>? _recentNotifications;

    /// <summary>
    /// Whether recent notifications are currently being loaded from the API.
    /// </summary>
    private bool _isLoadingRecent;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the notification context (loads unread count from API), subscribes
    /// to change notifications, and loads recent notifications for the dropdown.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        // Subscribe to context changes so the badge updates in real-time
        NotificationContext.OnChange += HandleContextChanged;

        // Initialize the context if not already loaded (loads unread count from API).
        if (!NotificationContext.IsLoaded)
        {
            await NotificationContext.InitializeAsync();
        }

        _unreadCount = NotificationContext.UnreadCount;

        // Load recent notifications for the dropdown
        await LoadRecentNotifications();
    }

    /// <summary>
    /// Unsubscribes from <see cref="INotificationContext.OnChange"/> to prevent memory leaks.
    /// </summary>
    public void Dispose()
    {
        NotificationContext.OnChange -= HandleContextChanged;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles clicking an individual notification in the dropdown.
    /// Marks the notification as read (if unread) and navigates to the notifications page
    /// with the notification ID as a query parameter for inline expansion.
    /// </summary>
    /// <param name="notification">The notification that was clicked.</param>
    private async Task HandleNotificationClick(NotificationDto notification)
    {
        if (!notification.IsRead)
        {
            var result = await ApiNotificationService.MarkAsReadAsync(notification.Id);

            if (result.Succeeded)
            {
                notification.IsRead = true;
                NotificationContext.DecrementCount();
            }
        }

        await _menuRef.CloseMenuAsync();
        NavigationManager.NavigateTo($"/account/notifications?id={notification.Id}");
    }

    /// <summary>
    /// Marks all notifications as read and refreshes the list.
    /// </summary>
    private async Task HandleMarkAllAsRead()
    {
        var result = await ApiNotificationService.MarkAllAsReadAsync();

        if (result.Succeeded)
        {
            NotificationContext.ClearCount();

            if (_recentNotifications is not null)
            {
                foreach (var n in _recentNotifications)
                    n.IsRead = true;
            }
        }
    }

    /// <summary>
    /// Navigates to the full notifications page.
    /// </summary>
    private async Task NavigateToNotifications()
    {
        await _menuRef.CloseMenuAsync();
        NavigationManager.NavigateTo("/account/notifications");
    }

    /// <summary>
    /// Handles the <see cref="INotificationContext.OnChange"/> event.
    /// Updates the local unread count and triggers a re-render via InvokeAsync for thread safety.
    /// </summary>
    private void HandleContextChanged()
    {
        InvokeAsync(async () =>
        {
            _unreadCount = NotificationContext.UnreadCount;
            await LoadRecentNotifications();
            StateHasChanged();
        });
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Loads the 10 most recent notifications from the API for the dropdown display.
    /// </summary>
    private async Task LoadRecentNotifications()
    {
        _isLoadingRecent = true;

        var result = await ApiNotificationService.GetRecentAsync();

        if (result.Succeeded)
            _recentNotifications = result.Data;
        else
            _recentNotifications = [];

        _isLoadingRecent = false;
    }

    /// <summary>
    /// Returns the appropriate MudBlazor icon for a notification category.
    /// </summary>
    /// <param name="category">The notification category.</param>
    /// <returns>A MudBlazor icon string.</returns>
    private static string GetCategoryIcon(NotificationCategory category) => category switch
    {
        NotificationCategory.Account => Icons.Material.Outlined.Security,
        NotificationCategory.Activity => Icons.Material.Outlined.People,
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
        NotificationCategory.Account => Color.Error,
        NotificationCategory.Activity => Color.Primary,
        NotificationCategory.System => Color.Info,
        _ => Color.Default
    };

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
