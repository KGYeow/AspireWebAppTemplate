using AspireWebAppTemplate.Core.Contracts.Announcements;

namespace AspireWebAppTemplate.Web.Abstractions;

/// <summary>
/// Per-circuit scoped service that caches the current user's active Banner-type announcements
/// and provides synchronous access for the TopBanner layout component.
/// </summary>
/// <remarks>
/// <para>
/// Registered as <b>scoped</b> — one instance per SignalR circuit (user session).
/// Announcements are loaded once via <see cref="InitializeAsync"/> at circuit startup
/// and subsequently updated synchronously by the banner component after dismissal actions.
/// </para>
/// <para>
/// Subscribers (e.g., TopBanner in the layout) listen to <see cref="OnChange"/> and call
/// <c>StateHasChanged</c> to re-render when the cached state changes.
/// </para>
/// </remarks>
public interface IAnnouncementContext : IAsyncDisposable
{
    /// <summary>
    /// Gets the cached list of active, non-dismissed, Banner-type announcements for the current user.
    /// Ordered by priority: Critical > Warning > Info, then by most recent CreatedAtUtc.
    /// Returns an empty list before initialization completes.
    /// </summary>
    IReadOnlyList<AnnouncementDto> BannerAnnouncements { get; }

    /// <summary>
    /// Gets whether the context has been initialized (loaded from API).
    /// Returns <c>true</c> after <see cref="InitializeAsync"/> completes (successfully or with error),
    /// <c>false</c> before initialization.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Raised when the cached announcement state changes (e.g., after dismissal).
    /// Subscribers should call <c>StateHasChanged</c> to re-render with the updated state.
    /// </summary>
    event Action? OnChange;

    /// <summary>
    /// Loads active announcements from the API and separates them into banner and all-active views.
    /// Called once per circuit during initialization (typically from the root layout).
    /// </summary>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    /// <remarks>
    /// On API failure, both lists remain empty, <see cref="IsLoaded"/> is set to <c>true</c>,
    /// and a warning is logged. The UI exits the loading state regardless of success or failure.
    /// </remarks>
    Task InitializeAsync();

    /// <summary>
    /// Dismisses an announcement for the current user via the API. On success, removes the
    /// announcement from <see cref="BannerAnnouncements"/> and fires <see cref="OnChange"/>.
    /// On failure, logs a warning and leaves the cached state unchanged (no optimistic update).
    /// </summary>
    /// <param name="announcementId">The unique identifier of the announcement to dismiss.</param>
    /// <returns>A task representing the asynchronous dismissal operation.</returns>
    Task DismissAsync(Guid announcementId);
}
