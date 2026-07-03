using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AspireWebAppTemplate.Web.Hubs;

/// <summary>
/// SignalR hub for real-time notification delivery to connected Blazor Server circuits.
/// Connections are grouped by authenticated user ID for targeted message delivery.
/// </summary>
/// <remarks>
/// <para>
/// <b>Authentication:</b> The <c>[Authorize]</c> attribute rejects unauthenticated connections
/// at the transport level. Authentication state comes from the existing cookie auth shared
/// with the Blazor Server circuit.
/// </para>
/// <para>
/// <b>User isolation:</b> Each connection is added to a SignalR group keyed by the user's
/// <c>NameIdentifier</c> claim. The hub exposes no client-callable methods — it is
/// server-to-client only, preventing clients from subscribing to other users' events.
/// </para>
/// <para>
/// <b>Multi-tab support:</b> Multiple connections from the same user (multiple browser tabs)
/// all join the same group, so all tabs receive notification events simultaneously.
/// </para>
/// </remarks>
[Authorize]
public class NotificationHub : Hub
{
    #region Connection Lifecycle

    /// <summary>
    /// Adds the authenticated user's connection to their user-specific SignalR group.
    /// Aborts the connection if the user identity cannot be determined.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Removes the connection from the user's SignalR group on disconnect.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    #endregion
}
