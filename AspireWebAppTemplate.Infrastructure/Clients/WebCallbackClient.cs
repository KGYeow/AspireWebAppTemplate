using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using AspireWebAppTemplate.Application.Features.Template.Notifications;

namespace AspireWebAppTemplate.Infrastructure.Clients;

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
/// <see cref="AspireWebAppTemplate.Infrastructure.Handlers.InternalApiKeyDelegatingHandler"/> configured on the HttpClient pipeline.
/// </para>
/// </remarks>
public class WebCallbackClient
{
    #region Constructor

    /// <summary>
    /// The internal endpoint path for pushing notification events to the Web project.
    /// </summary>
    private const string CallbackPath = "/internal/notifications/push";

    /// <summary>
    /// The configured HttpClient with Aspire service discovery base address for the Web project.
    /// </summary>
    private readonly HttpClient _httpClient;

    /// <summary>
    /// The logger for recording callback warnings and errors.
    /// </summary>
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
    /// <param name="request">The notification push request containing user ID, title, message, category, and unread count.</param>
    /// <remarks>
    /// Failures are logged at Warning level but never propagate to the caller.
    /// The notification is already persisted in the database.
    /// </remarks>
    public async Task NotifyAsync(NotificationPushRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(CallbackPath, request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Notification callback to Web failed with status {StatusCode} for user '{UserId}'.",
                    response.StatusCode, request.UserId);
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Notification callback timed out for user '{UserId}'.", request.UserId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Notification callback network error for user '{UserId}'.", request.UserId);
        }
    }

    #endregion
}
