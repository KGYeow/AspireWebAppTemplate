using System.Text.RegularExpressions;
using AspireWebAppTemplate.Core.Contracts.Announcements;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.Web.Abstractions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Layout.Topbar;

/// <summary>
/// Persistent, dismissible banner rendered at the top of the MainLayout.
/// Displays the highest-priority active, non-dismissed, Banner-type announcement for the current user.
/// </summary>
/// <remarks>
/// <para>
/// The component subscribes to <see cref="IAnnouncementContext.OnChange"/> and re-renders
/// whenever the cached announcement state changes (e.g., after a dismissal or initialization).
/// </para>
/// <para>
/// Severity-based styling: Info (blue), Warning (amber), Critical (red).
/// When multiple banner announcements exist, a "N more" link navigates to the full list page.
/// </para>
/// </remarks>
public partial class AnnouncementBanner : ComponentBase, IDisposable
{
    #region Injected Services

    /// <summary>
    /// Per-circuit announcement context providing cached banner announcements and dismissal API.
    /// </summary>
    [Inject]
    private IAnnouncementContext AnnouncementContext { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// The highest-priority announcement currently displayed in the banner.
    /// Null when no active, non-dismissed, Banner-type announcements exist.
    /// </summary>
    private AnnouncementDto? _currentAnnouncement;

    /// <summary>
    /// The plain-text excerpt of the current announcement content (HTML stripped, truncated to 150 chars).
    /// </summary>
    private string _contentExcerpt = string.Empty;

    /// <summary>
    /// The count of remaining banner announcements beyond the one currently displayed.
    /// Zero when only one (or no) announcement exists.
    /// </summary>
    private int _moreCount;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Subscribes to the announcement context change event and refreshes the displayed state.
    /// </summary>
    protected override void OnInitialized()
    {
        AnnouncementContext.OnChange += HandleContextChanged;
        RefreshState();
    }

    /// <summary>
    /// Unsubscribes from the announcement context change event to prevent memory leaks.
    /// </summary>
    public void Dispose()
    {
        AnnouncementContext.OnChange -= HandleContextChanged;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles the announcement context state change event.
    /// Refreshes the displayed announcement and triggers a re-render.
    /// </summary>
    private void HandleContextChanged()
    {
        InvokeAsync(() =>
        {
            RefreshState();
            StateHasChanged();
        });
    }

    /// <summary>
    /// Dismisses the currently displayed announcement via the context.
    /// On success, the context removes it from cache and fires OnChange,
    /// which triggers <see cref="HandleContextChanged"/> to show the next announcement.
    /// </summary>
    private async Task HandleDismiss()
    {
        if (_currentAnnouncement is null) return;
        await AnnouncementContext.DismissAsync(_currentAnnouncement.Id);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Refreshes the local state from the announcement context's cached banner announcements.
    /// Selects the first (highest-priority) announcement and computes the content excerpt.
    /// </summary>
    private void RefreshState()
    {
        var bannerAnnouncements = AnnouncementContext.BannerAnnouncements;

        if (bannerAnnouncements.Count == 0)
        {
            _currentAnnouncement = null;
            _contentExcerpt = string.Empty;
            _moreCount = 0;
            return;
        }

        _currentAnnouncement = bannerAnnouncements[0];
        _contentExcerpt = StripHtmlAndTruncate(_currentAnnouncement.Message, 150);
        _moreCount = bannerAnnouncements.Count - 1;
    }

    /// <summary>
    /// Strips HTML tags from the content and truncates to the specified maximum length.
    /// Appends "..." when the content is truncated.
    /// </summary>
    /// <param name="html">The HTML content to process.</param>
    /// <param name="maxLength">The maximum character length for the output.</param>
    /// <returns>A plain-text excerpt of the content.</returns>
    private static string StripHtmlAndTruncate(string html, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var plainText = Regex.Replace(html, "<[^>]*>", "").Trim();

        if (plainText.Length <= maxLength)
            return plainText;

        return plainText[..maxLength] + "...";
    }

    /// <summary>
    /// Returns the CSS class for the banner based on the current announcement's severity.
    /// </summary>
    /// <returns>A CSS class string with severity-appropriate background color.</returns>
    private string GetBannerClass()
    {
        var severityClass = _currentAnnouncement?.Severity switch
        {
            AnnouncementSeverity.Critical => "mud-theme-error",
            AnnouncementSeverity.Warning => "mud-theme-warning",
            _ => "mud-theme-info"
        };

        return $"top-banner {severityClass}";
    }

    /// <summary>
    /// Returns the Material icon for the current announcement's severity level.
    /// </summary>
    /// <returns>A Material icon string.</returns>
    private string GetSeverityIcon()
    {
        return _currentAnnouncement?.Severity switch
        {
            AnnouncementSeverity.Critical => Icons.Material.Rounded.Error,
            AnnouncementSeverity.Warning => Icons.Material.Rounded.Warning,
            _ => Icons.Material.Rounded.Info
        };
    }

    #endregion
}
