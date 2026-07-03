using AspireWebAppTemplate.Core.Contracts.Notifications;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.Web.Authentication;
using AspireWebAppTemplate.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AspireWebAppTemplate.Web.Endpoints;

/// <summary>
/// Internal HTTP endpoint that receives notification-created events from the API service
/// and forwards them to the target user's SignalR group via <see cref="NotificationHub"/>.
/// </summary>
/// <remarks>
/// <para>
/// This endpoint is protected by <c>InternalApiPolicy</c> which requires the
/// <c>X-Internal-Api-Key</c> header, preventing external access.
/// </para>
/// <para>
/// If the target user has no active SignalR connections, the hub's SendAsync is a no-op —
/// the endpoint still returns 200 OK as the notification is already persisted in the database.
/// </para>
/// </remarks>
public static class NotificationCallbackEndpoint
{
    /// <summary>
    /// Maps the internal notification push endpoint at <c>POST /internal/notifications/push</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapNotificationCallback(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/notifications/push", HandlePush)
            .RequireAuthorization("InternalApiPolicy");

        return endpoints;
    }

    /// <summary>
    /// Validates the incoming push request and delivers the notification event to the target
    /// user's SignalR group via the <see cref="NotificationHub"/>.
    /// </summary>
    private static async Task<IResult> HandlePush(NotificationPushRequest request, IHubContext<NotificationHub> hubContext)
    {
        // Validate required fields.
        if (string.IsNullOrWhiteSpace(request.UserId))
            return Results.BadRequest("UserId is required.");

        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest("Title is required.");

        if (request.Title.Length > 200)
            return Results.BadRequest("Title must not exceed 200 characters.");

        if (string.IsNullOrWhiteSpace(request.Category) ||
            !Enum.TryParse<NotificationCategory>(request.Category, ignoreCase: true, out _))
            return Results.BadRequest("Category must be a valid NotificationCategory value.");

        if (request.UnreadCount < 0)
            return Results.BadRequest("UnreadCount must be >= 0.");

        // Deliver to the user's SignalR group (no-op if user has no active connections).
        await hubContext.Clients.Group(request.UserId)
            .SendAsync("ReceiveNotification", request.Title, request.Category, request.UnreadCount);

        return Results.Ok();
    }
}
