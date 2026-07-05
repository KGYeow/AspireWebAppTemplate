using AspireWebAppTemplate.Core.Contracts.Notifications;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.UI.Components.Shared;
using AspireWebAppTemplate.UI.Utilities;
using AspireWebAppTemplate.Web.Abstractions;
using AspireWebAppTemplate.Web.Models;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Layout.Topbar;

/// <summary>
/// Notification bell icon with unread count badge and dropdown popover.
/// Displays the 10 most recent notifications with title, category icon, and relative timestamp.
/// Subscribes to <see cref="INotificationContext.OnChange"/> for badge updates and
/// <see cref="INotificationContext.OnNotificationReceived"/> for toast/dropdown reactions.
/// </summary>
/// <remarks>
/// The hub connection lifecycle is managed by <see cref="INotificationContext"/>.
/// This component handles only UI concerns: rendering the badge, showing the dropdown,
/// displaying snackbar toasts, and prepending new notifications to the dropdown list.
/// </remarks>
public partial class NotificationBell : ComponentBase, IDisposable
{
    #region Injected Services

    /// <summary>
    /// Per-circuit notification context providing cached unread count, hub connectivity,
    /// and change/notification events.
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

    /// <summary>
    /// Logger for recording component-level warnings.
    /// </summary>
    [Inject]
    private ILogger<NotificationBell> Logger { get; set; } = default!;

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
    /// Initializes the notification context (loads unread count + starts hub connection),
    /// subscribes to events, and loads recent notifications for the dropdown.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        // Subscribe to context events.
        NotificationContext.OnChange += HandleContextChanged;
        NotificationContext.OnNotificationReceived += HandleNotificationReceived;

        // Initialize the context if not already loaded (loads count + starts hub).
        if (!NotificationContext.IsLoaded)
        {
            var hubUrl = NavigationManager.ToAbsoluteUri("/hubs/notifications");
            await NotificationContext.InitializeAsync(hubUrl);
        }

        _unreadCount = NotificationContext.UnreadCount;

        // Load recent notifications for the dropdown.
        await LoadRecentNotifications();
    }

    /// <summary>
    /// Unsubscribes from context events to prevent memory leaks.
    /// </summary>
    public void Dispose()
    {
        NotificationContext.OnChange -= HandleContextChanged;
        NotificationContext.OnNotificationReceived -= HandleNotificationReceived;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles the <see cref="INotificationContext.OnChange"/> event.
    /// Updates the local unread count and triggers a re-render via InvokeAsync for thread safety.
    /// </summary>
    private void HandleContextChanged()
    {
        InvokeAsync(() =>
        {
            _unreadCount = NotificationContext.UnreadCount;
            StateHasChanged();
        });
    }

    /// <summary>
    /// Handles the <see cref="INotificationContext.OnNotificationReceived"/> event.
    /// Prepends the new notification to the dropdown list and shows a snackbar toast
    /// with a deep-link click handler.
    /// </summary>
    /// <param name="args">The notification event arguments containing title, message, category, and notification ID.</param>
    private void HandleNotificationReceived(NotificationReceivedEventArgs args)
    {
        InvokeAsync(() =>
        {
            // Prepend to dropdown if notifications are already loaded.
            if (_recentNotifications is not null)
            {
                var parsedCategory = Enum.TryParse<NotificationCategory>(args.Category, ignoreCase: true, out var cat)
                    ? cat
                    : NotificationCategory.System;

                _recentNotifications.Insert(0, new NotificationDto
                {
                    Id = args.NotificationId,
                    Title = args.Title,
                    Message = args.Message,
                    Category = parsedCategory,
                    IsRead = false,
                    CreatedAtUtc = DateTime.UtcNow
                });

                // Keep the list at a reasonable size (10 items max in dropdown).
                if (_recentNotifications.Count > 10)
                    _recentNotifications.RemoveAt(_recentNotifications.Count - 1);
            }

            ShowToast(args.Title, args.Message, args.Category, args.NotificationId);
            StateHasChanged();
        });
    }

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
    /// Displays a rich notification snackbar using the custom NotificationSnackbarContent component.
    /// Configures per-snackbar positioning to top-right and require-interaction dismiss.
    /// The snackbar's Onclick handler navigates to the deep-link URL for inline notification expansion.
    /// Suppresses display when the user has disabled notification popups.
    /// </summary>
    /// <param name="title">The notification title.</param>
    /// <param name="message">The notification message body.</param>
    /// <param name="category">The notification category string.</param>
    /// <param name="notificationId">The notification entity ID for deep-link URL construction.</param>
    private void ShowToast(string title, string message, string category, Guid notificationId)
    {
        if (!NotificationContext.NotificationPopupsEnabled)
            return;

        Snackbar.Add<NotificationSnackbarContent>(new Dictionary<string, object>
        {
            { nameof(NotificationSnackbarContent.Title), title },
            { nameof(NotificationSnackbarContent.Message), message },
            { nameof(NotificationSnackbarContent.Category), category }
        }, Severity.Normal, config =>
        {
            //config.VisibleStateDuration = 5000;
            config.RequireInteraction = true;
            config.ShowCloseIcon = true;
            config.SnackbarVariant = Variant.Text;
            config.HideIcon = true;
            config.OnClick = _ =>
            {
                NavigationManager.NavigateTo($"/account/notifications?id={notificationId}");
                return Task.CompletedTask;
            };
        });
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
