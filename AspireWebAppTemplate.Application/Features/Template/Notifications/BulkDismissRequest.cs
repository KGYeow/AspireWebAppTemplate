namespace AspireWebAppTemplate.Application.Features.Template.Notifications;

/// <summary>
/// Request DTO for bulk dismissing (deleting) multiple notifications.
/// Accepts a maximum of 100 notification IDs per request.
/// </summary>
public sealed class BulkDismissRequest
{
    /// <summary>
    /// The list of notification IDs to dismiss. Maximum 100 IDs per request.
    /// </summary>
    public List<Guid> NotificationIds { get; set; } = [];
}
