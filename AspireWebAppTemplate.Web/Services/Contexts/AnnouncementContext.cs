using AspireWebAppTemplate.Application.Contracts.Announcements;
using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.Web.Abstractions;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// Per-circuit announcement state manager that caches the current user's active Banner-type
/// announcements and provides synchronous access for the TopBanner layout component.
/// </summary>
/// <remarks>
/// <para>
/// <b>Per-circuit caching strategy:</b> This service is registered as <b>scoped</b>, meaning each
/// Blazor Server SignalR circuit (user session) gets its own instance. Announcements are loaded
/// a single time via <see cref="InitializeAsync"/> and cached for the lifetime of the circuit.
/// Dismissals update the cache synchronously on confirmed API success to avoid stale state.
/// </para>
/// <para>
/// <b>No optimistic updates:</b> The local cache is only modified after the API confirms success.
/// On dismissal failure, the cache remains unchanged and the user sees the announcement until
/// a successful retry or page refresh.
/// </para>
/// <para>
/// <b>Failure tolerance:</b> Initialization failures are caught and logged. The context transitions
/// to the loaded state with an empty list so the UI exits its loading state gracefully.
/// </para>
/// </remarks>
public sealed class AnnouncementContext : IAnnouncementContext
{
    #region Constructor

    /// <summary>
    /// The typed HttpClient service for announcement API operations.
    /// </summary>
    private readonly ApiAnnouncementService _apiAnnouncementService;

    /// <summary>
    /// The logger for recording warnings and errors during API calls.
    /// </summary>
    private readonly ILogger<AnnouncementContext> _logger;

    /// <summary>
    /// The cached list of active, non-dismissed, Banner-type announcements (priority-ordered).
    /// </summary>
    private List<AnnouncementDto> _bannerAnnouncements = [];

    /// <summary>
    /// Tracks whether <see cref="InitializeAsync"/> has completed (success or failure).
    /// </summary>
    private bool _isLoaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnnouncementContext"/> class.
    /// </summary>
    /// <param name="apiAnnouncementService">The typed HttpClient for announcement API operations.</param>
    /// <param name="logger">The logger for recording warnings and errors.</param>
    public AnnouncementContext(
        ApiAnnouncementService apiAnnouncementService,
        ILogger<AnnouncementContext> logger)
    {
        _apiAnnouncementService = apiAnnouncementService;
        _logger = logger;
    }

    #endregion

    #region Properties and Events

    /// <inheritdoc />
    public IReadOnlyList<AnnouncementDto> BannerAnnouncements => _bannerAnnouncements;

    /// <inheritdoc />
    public bool IsLoaded => _isLoaded;

    /// <inheritdoc />
    public event Action? OnChange;

    #endregion

    #region Initialization

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        try
        {
            var result = await _apiAnnouncementService.GetActiveForUserAsync();

            if (result.Succeeded)
            {
                var announcements = result.Data ?? [];

                // Banner-type announcements only, already priority-ordered by the API
                // (Critical > Warning > Info, then by most recent CreatedAtUtc).
                _bannerAnnouncements = announcements
                    .Where(a => a.DisplayType == AnnouncementDisplayType.Banner)
                    .ToList();
            }
            else
            {
                _logger.LogWarning(
                    "Failed to load active announcements from API. Error: {Error}. " +
                    "Banner will show no announcements until next refresh.",
                    result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Exception occurred while loading active announcements. " +
                "Banner will show no announcements until next refresh.");
        }
        finally
        {
            // Always mark as loaded (even on failure) so the UI transitions out of
            // the loading state and doesn't leave components in a perpetual skeleton state.
            _isLoaded = true;
            OnChange?.Invoke();
        }
    }

    #endregion

    #region Dismissal

    /// <inheritdoc />
    public async Task DismissAsync(Guid announcementId)
    {
        try
        {
            var result = await _apiAnnouncementService.DismissAsync(announcementId);

            if (result.Succeeded)
            {
                // Remove from banner announcements cache on confirmed success.
                _bannerAnnouncements.RemoveAll(a => a.Id == announcementId);
                OnChange?.Invoke();
            }
            else
            {
                // API returned a non-success result — do NOT modify the cache.
                _logger.LogWarning(
                    "Failed to dismiss announcement {AnnouncementId}. Error: {Error}. " +
                    "Banner state remains unchanged.",
                    announcementId, result.Error);
            }
        }
        catch (Exception ex)
        {
            // Network failure or unexpected error — do NOT modify the cache.
            _logger.LogWarning(ex,
                "Exception occurred while dismissing announcement {AnnouncementId}. " +
                "Banner state remains unchanged.",
                announcementId);
        }
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Disposes the context when the circuit ends. No-op for this context as there are
    /// no hub connections or unmanaged resources to clean up.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    #endregion
}
