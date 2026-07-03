using System.Net.Http.Json;

namespace AspireWebAppTemplate.ApiService.Services;

/// <summary>
/// Typed HttpClient for calling the Web project's notification callback endpoint.
/// Registered with Aspire service discovery using the "webfrontend" base address.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fire-and-forget semantics:</b> All failures (timeout, network error, non-success HTTP status)
/// are caught, logged at Warning level, and never propagated to the caller. The notification is
/// already persisted in the database — real-time delivery is best-effort.
/// </para>
/// <para>
/// The internal API key header is attached automatically by the
/// <see cref="Handlers.InternalApiKeyDelegatingHandler"/> configured on the HttpClient pipeline.
/// </para>
/// </remarks>
public class WebCallbackClient
{
    #region Constructor

    /// <summary>
    /// The internal endpoint path for pushing notification events to the Web project.
    /// </summary>
    private const string CallbackPath = "/internal/notifications/push";

    private readonly HttpClient _httpClient;
    private readonly ILogger<WebCallbackClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebCallbackClient"/> class.
    /// </summary>
    /// <param name="httpClient">The configured HttpClient with Aspire service discovery base address.</param>
    /// <param name="logger">The logger for recording callback warnings.</param>
    public WebCallbackClient(HttpClient httpClient, ILogger<WebCallbackClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    #endregion

    #region Notification Callback

    /// <summary>
    /// Notifies the Web project that a notification was created so it can push
    /// the event to the target user's connected circuits via SignalR.
    /// </summary>
    /// <param name="userId">The target user's unique identifier.</param>
    /// <param name="title">The notification title for display.</param>
    /// <param name="category">The notification category as a string (NotificationCategory enum name).</param>
    /// <param name="unreadCount">The user's current total unread notification count.</param>
    /// <remarks>
    /// Failures are logged at Warning level but never propagate to the caller.
    /// The notification is already persisted in the database.
    /// </remarks>
    public async Task NotifyAsync(string userId, string title, string category, int unreadCount)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                CallbackPath,
                new { userId, title, category, unreadCount });

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Notification callback to Web failed with status {StatusCode} for user '{UserId}'.",
                    response.StatusCode, userId);
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Notification callback timed out for user '{UserId}'.", userId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,"Notification callback network error for user '{UserId}'.", userId);
        }
    }

    #endregion
}
