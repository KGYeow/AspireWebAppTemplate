using AspireWebAppTemplate.ApiService.Abstractions;
using AspireWebAppTemplate.ApiService.Data;
using AspireWebAppTemplate.ApiService.Data.Entities;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Contracts.Notifications;
using AspireWebAppTemplate.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AspireWebAppTemplate.ApiService.Services;

/// <summary>
/// Implements the <see cref="INotificationService"/> interface to manage in-app notification
/// creation, retrieval, status transitions, bulk operations, and user delivery preferences.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Create behavior:</strong> <see cref="CreateNotificationAsync"/> validates that the target
/// user exists and that InAppEnabled is true for the requested category (defaulting to true when no
/// preference record exists). If either check fails, the notification is silently discarded. All
/// exceptions are caught, logged at Error level, and never propagated — ensuring notification creation
/// never disrupts the caller's primary operation.
/// </para>
/// <para>
/// <strong>Ownership enforcement:</strong> All query and mutation methods scope operations to the
/// specified userId. Notifications belonging to other users are invisible and inaccessible.
/// </para>
/// <para>
/// <strong>Idempotency:</strong> <see cref="MarkAsReadAsync"/> is idempotent — marking an already-read
/// notification succeeds without modifying the ReadAtUtc timestamp.
/// </para>
/// <para>
/// Registered as a scoped service to align with the per-request <see cref="ApplicationDbContext"/>
/// lifetime.
/// </para>
/// </remarks>
public class NotificationService : INotificationService
{
    #region Constructor

    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<NotificationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationService"/> class.
    /// </summary>
    /// <param name="dbContext">The application database context for querying and persisting notification data.</param>
    /// <param name="logger">The logger instance for recording warnings and errors during notification operations.</param>
    public NotificationService(ApplicationDbContext dbContext, ILogger<NotificationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    #endregion

    #region Creation

    /// <inheritdoc />
    public async Task CreateNotificationAsync(CreateNotificationRequest request)
    {
        try
        {
            // Validate that the target user exists in the system.
            var userExists = await _dbContext.Users.AnyAsync(u => u.Id == request.UserId);

            if (!userExists)
            {
                _logger.LogWarning(
                    "Notification discarded: user '{UserId}' does not exist.",
                    request.UserId);
                return;
            }

            // Check the user's InAppEnabled preference for this category.
            // If no preference record exists, default to InAppEnabled = true.
            var preference = await _dbContext.NotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == request.UserId && p.Category == request.Category);

            var inAppEnabled = preference?.InAppEnabled ?? true;

            if (!inAppEnabled)
            {
                _logger.LogInformation(
                    "Notification discarded: user '{UserId}' has InAppEnabled=false for category '{Category}'.",
                    request.UserId,
                    request.Category);
                return;
            }

            // Create the notification entity.
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Category = request.Category,
                Title = request.Title,
                Message = request.Message,
                IsRead = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Notification '{NotificationId}' created for user '{UserId}' in category '{Category}'.",
                notification.Id,
                request.UserId,
                request.Category);
        }
        catch (Exception ex)
        {
            // Never propagate exceptions from notification creation — log and discard.
            _logger.LogError(
                ex,
                "Failed to create notification for user '{UserId}' in category '{Category}'. The notification was discarded.",
                request.UserId,
                request.Category);
        }
    }

    #endregion

    #region Retrieval

    /// <inheritdoc />
    public async Task<PagedResult<NotificationDto>> GetNotificationsAsync(string userId, NotificationQueryParams queryParams)
    {
        // Start with the user's notifications.
        var query = _dbContext.Notifications.AsNoTracking().Where(n => n.UserId == userId);

        // Apply optional category filter.
        if (queryParams.Category.HasValue)
        {
            query = query.Where(n => n.Category == queryParams.Category.Value);
        }

        // Apply optional read status filter.
        if (queryParams.IsRead.HasValue)
        {
            query = query.Where(n => n.IsRead == queryParams.IsRead.Value);
        }

        // Order by CreatedAtUtc descending (newest first).
        query = query.OrderByDescending(n => n.CreatedAtUtc);

        // Get total count for pagination metadata.
        var totalCount = await query.CountAsync();

        // Apply pagination (skip/take).
        var pageSize = Math.Clamp(queryParams.PageSize, 1, 100);
        var page = Math.Max(queryParams.Page, 1);
        var skip = (page - 1) * pageSize;

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .Select(n => MapToDto(n))
            .ToListAsync();

        return new PagedResult<NotificationDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _dbContext.Notifications.AsNoTracking().CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    /// <inheritdoc />
    public async Task<List<NotificationDto>> GetRecentAsync(string userId, int count = 5)
    {
        return await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(count)
            .Select(n => MapToDto(n))
            .ToListAsync();
    }

    #endregion

    #region Status Management

    /// <inheritdoc />
    public async Task<bool> MarkAsReadAsync(string userId, Guid notificationId)
    {
        // Find the notification by ID and ensure it belongs to the specified user.
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification is null)
            return false;

        // Idempotent: if already read, don't modify the ReadAtUtc timestamp.
        if (notification.IsRead)
            return true;

        notification.IsRead = true;
        notification.ReadAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return true;
    }

    /// <inheritdoc />
    public async Task<int> MarkAllAsReadAsync(string userId)
    {
        // Query all unread notifications for the user.
        var unreadNotifications = await _dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        if (unreadNotifications.Count == 0)
            return 0;

        var now = DateTime.UtcNow;

        // Mark each notification as read with the current UTC timestamp.
        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = now;
        }

        await _dbContext.SaveChangesAsync();

        return unreadNotifications.Count;
    }

    #endregion

    #region Bulk Operations

    /// <inheritdoc />
    public async Task<int> BulkDismissAsync(string userId, List<Guid> notificationIds)
    {
        // Query only notifications that exist AND belong to the specified user.
        // IDs that don't exist or belong to other users are silently ignored.
        var notificationsToDelete = await _dbContext.Notifications
            .Where(n => notificationIds.Contains(n.Id) && n.UserId == userId)
            .ToListAsync();

        if (notificationsToDelete.Count == 0)
            return 0;

        _dbContext.Notifications.RemoveRange(notificationsToDelete);
        await _dbContext.SaveChangesAsync();

        return notificationsToDelete.Count;
    }

    #endregion

    #region Preferences

    /// <inheritdoc />
    public async Task<List<NotificationPreferenceDto>> GetPreferencesAsync(string userId)
    {
        // Get all stored preference records for the user.
        var storedPreferences = await _dbContext.NotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync();

        // Build a complete list covering all categories, filling defaults for missing ones.
        var allCategories = Enum.GetValues<NotificationCategory>();
        var result = new List<NotificationPreferenceDto>(allCategories.Length);

        foreach (var category in allCategories)
        {
            var existing = storedPreferences.FirstOrDefault(p => p.Category == category);

            result.Add(new NotificationPreferenceDto
            {
                Category = category,
                InAppEnabled = existing?.InAppEnabled ?? true,
                EmailEnabled = existing?.EmailEnabled ?? true
            });
        }

        return result;
    }

    /// <inheritdoc />
    public async Task UpdatePreferenceAsync(string userId, UpdateNotificationPreferenceRequest request)
    {
        // Find existing preference for this user-category pair.
        var existing = await _dbContext.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Category == request.Category);

        if (existing is not null)
        {
            // Update existing record.
            existing.InAppEnabled = request.InAppEnabled;
            existing.EmailEnabled = request.EmailEnabled;
        }
        else
        {
            // Create new preference record.
            var preference = new NotificationPreference
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Category = request.Category,
                InAppEnabled = request.InAppEnabled,
                EmailEnabled = request.EmailEnabled
            };

            _dbContext.NotificationPreferences.Add(preference);
        }

        await _dbContext.SaveChangesAsync();
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Maps a <see cref="Notification"/> entity to a <see cref="NotificationDto"/> response object.
    /// </summary>
    /// <param name="notification">The notification entity to map.</param>
    /// <returns>A <see cref="NotificationDto"/> with all fields populated from the entity.</returns>
    private static NotificationDto MapToDto(Notification notification)
    {
        return new NotificationDto
        {
            Id = notification.Id,
            Category = notification.Category,
            Title = notification.Title,
            Message = notification.Message,
            IsRead = notification.IsRead,
            CreatedAtUtc = notification.CreatedAtUtc,
            ReadAtUtc = notification.ReadAtUtc
        };
    }

    #endregion
}
