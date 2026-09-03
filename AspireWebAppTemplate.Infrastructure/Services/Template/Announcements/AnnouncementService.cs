using Microsoft.Extensions.Logging;
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Utilities;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Features.Template.Announcements;
using AspireWebAppTemplate.Application.Features.Template.AuditLog;
using AspireWebAppTemplate.Application.Features.Template.Notifications;
using AspireWebAppTemplate.Domain.Enums;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;

namespace AspireWebAppTemplate.Infrastructure.Services.Template.Announcements;

/// <summary>
/// Implements the <see cref="IAnnouncementService"/> interface to manage announcement
/// creation, retrieval, status filtering, HTML content sanitization, notification delivery,
/// and per-user dismissal management.
/// </summary>
/// <remarks>
/// <para>
/// <strong>HTML sanitization:</strong> Both <see cref="CreateAsync"/> and <see cref="UpdateAsync"/>
/// sanitize the Message field using an allowlist-based <see cref="HtmlSanitizer"/> before persistence,
/// protecting against XSS attacks regardless of input source.
/// </para>
/// <para>
/// <strong>Notification delivery:</strong> When NotifyUsers is true and the announcement is immediately
/// active, a notification is created for each active user via <see cref="INotificationService"/>.
/// Notification failures are logged and swallowed — they never disrupt announcement operations.
/// </para>
/// <para>
/// <strong>Audit logging:</strong> Admin operations (create, update, delete) are audited via
/// <see cref="IAuditLogService"/> with old/new value tracking for updates. Audit failures are
/// swallowed and logged at Error level.
/// </para>
/// <para>
/// Registered as a scoped service to align with the per-request <see cref="ApplicationDbContext"/>
/// lifetime.
/// </para>
/// </remarks>
public class AnnouncementService : IAnnouncementService
{
    #region Constructor

    /// <summary>
    /// The application database context for querying and persisting announcement data.
    /// </summary>
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// Provides the authenticated user's identity (UserId, UserName, IpAddress) for audit metadata.
    /// </summary>
    private readonly ICurrentUserAccessor _currentUserAccessor;

    /// <summary>
    /// The audit log service for recording admin operations on announcements.
    /// </summary>
    private readonly IAuditLogService _auditLogService;

    /// <summary>
    /// The notification service for delivering user notifications when announcements become active.
    /// </summary>
    private readonly INotificationService _notificationService;

    /// <summary>
    /// The logger instance for recording warnings and errors during announcement operations.
    /// </summary>
    private readonly ILogger<AnnouncementService> _logger;

    /// <summary>
    /// The HTML sanitizer configured with an allowlist of permitted tags and attributes
    /// for cleaning announcement content before persistence.
    /// </summary>
    private readonly HtmlSanitizer _htmlSanitizer;

    /// <summary>
    /// Defines the fields captured in audit log snapshots for announcement update operations.
    /// Used by <see cref="AuditChangeHelper.Snapshot{T}"/> to compute old/new value diffs.
    /// </summary>
    private static readonly (string Key, Func<Announcement, object?> Getter)[] AnnouncementAuditFields =
    [
        ("Title", a => a.Title),
        ("Content", a => a.Content),
        ("DisplayType", a => a.DisplayType.ToString()),
        ("Severity", a => a.Severity.ToString()),
        ("StartsAtUtc", a => a.StartsAtUtc),
        ("ExpiresAtUtc", a => a.ExpiresAtUtc),
        ("IsActive", a => a.IsActive),
        ("NotifyUsers", a => a.NotifyUsers)
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="AnnouncementService"/> class.
    /// </summary>
    /// <param name="dbContext">The application database context for querying and persisting announcement data.</param>
    /// <param name="currentUserAccessor">Provides the authenticated user's identity for audit metadata.</param>
    /// <param name="auditLogService">The audit log service for recording admin operations.</param>
    /// <param name="notificationService">The notification service for delivering user notifications.</param>
    /// <param name="logger">The logger instance for recording warnings and errors.</param>
    /// <param name="htmlSanitizer">The HTML sanitizer for cleaning announcement content before persistence.</param>
    public AnnouncementService(
        ApplicationDbContext dbContext,
        ICurrentUserAccessor currentUserAccessor,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        ILogger<AnnouncementService> logger,
        HtmlSanitizer htmlSanitizer)
    {
        _dbContext = dbContext;
        _currentUserAccessor = currentUserAccessor;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
        _logger = logger;
        _htmlSanitizer = htmlSanitizer;
    }

    #endregion

    #region CRUD Operations

    /// <inheritdoc />
    public async Task<AnnouncementDto> CreateAsync(CreateAnnouncementRequest request)
    {
        // Validate input fields
        if (request.Title.Length > 200)
            throw new ArgumentException("Title must not exceed 200 characters.");

        if (request.Message.Length > 10000)
            throw new ArgumentException("Content must not exceed 10000 characters.");

        if (request.StartsAtUtc.HasValue && request.ExpiresAtUtc.HasValue
            && request.StartsAtUtc.Value >= request.ExpiresAtUtc.Value)
            throw new ArgumentException("Start date must be before expiry date.");

        // Sanitize HTML content before persistence
        var sanitizedContent = _htmlSanitizer.Sanitize(request.Message);

        var utcNow = DateTime.UtcNow;

        // Create the announcement entity
        var announcement = new Announcement
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Content = sanitizedContent,
            DisplayType = request.DisplayType,
            Severity = request.Severity,
            StartsAtUtc = request.StartsAtUtc,
            ExpiresAtUtc = request.ExpiresAtUtc,
            IsActive = request.IsActive,
            NotifyUsers = request.NotifyUsers,
            CreatedByUserId = _currentUserAccessor.UserId!,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        _dbContext.Announcements.Add(announcement);
        await _dbContext.SaveChangesAsync();

        // Create notifications for active users when NotifyUsers=true and announcement is immediately active
        if (request.NotifyUsers && request.IsActive
            && (!request.StartsAtUtc.HasValue || request.StartsAtUtc.Value <= utcNow))
        {
            try
            {
                var activeUsers = await _dbContext.Users
                    .Where(u => u.IsActive)
                    .Select(u => u.Id)
                    .ToListAsync();

                foreach (var userId in activeUsers)
                {
                    try
                    {
                        await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
                        {
                            UserId = userId,
                            Category = NotificationCategory.System,
                            Title = "New Announcement",
                            Message = announcement.Title
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to create notification for user {UserId} for announcement {AnnouncementId}.",
                            userId, announcement.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query active users for notification delivery for announcement {AnnouncementId}.",
                    announcement.Id);
            }
        }

        // Audit log the creation
        try
        {
            await _auditLogService.LogAsync(new AuditLogRequest
            {
                UserId = _currentUserAccessor.UserId,
                ActionType = AuditActionType.AnnouncementCreated,
                EntityType = AuditEntityType.Announcement,
                EntityId = announcement.Id.ToString(),
                EntityName = announcement.Title,
                Description = $"Created announcement '{announcement.Title}'.",
                IpAddress = _currentUserAccessor.IpAddress
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to audit log creation of announcement {AnnouncementId}.", announcement.Id);
        }

        return MapToDto(announcement, utcNow);
    }

    /// <inheritdoc />
    public async Task<AnnouncementDto> UpdateAsync(Guid id, UpdateAnnouncementRequest request)
    {
        // Validate input fields (same constraints as CreateAsync)
        if (request.Title.Length > 200)
            throw new ArgumentException("Title must not exceed 200 characters.");

        if (request.Message.Length > 10000)
            throw new ArgumentException("Content must not exceed 10000 characters.");

        if (request.StartsAtUtc.HasValue && request.ExpiresAtUtc.HasValue
            && request.StartsAtUtc.Value >= request.ExpiresAtUtc.Value)
            throw new ArgumentException("Start date must be before expiry date.");

        // Find the existing announcement
        var announcement = await _dbContext.Announcements.FindAsync(id);
        if (announcement is null)
            throw new KeyNotFoundException($"Announcement with ID '{id}' was not found.");

        // Snapshot the entity state before mutation for audit logging
        var before = AuditChangeHelper.Snapshot(announcement, AnnouncementAuditFields);

        // Sanitize HTML content before persistence
        var sanitizedContent = _htmlSanitizer.Sanitize(request.Message);

        var utcNow = DateTime.UtcNow;

        // Update entity fields
        announcement.Title = request.Title;
        announcement.Content = sanitizedContent;
        announcement.DisplayType = request.DisplayType;
        announcement.Severity = request.Severity;
        announcement.StartsAtUtc = request.StartsAtUtc;
        announcement.ExpiresAtUtc = request.ExpiresAtUtc;
        announcement.IsActive = request.IsActive;
        announcement.NotifyUsers = request.NotifyUsers;
        announcement.UpdatedAtUtc = utcNow;

        // Clear all dismissal records when ClearDismissals=true
        if (request.ClearDismissals)
        {
            var dismissals = await _dbContext.AnnouncementDismissals
                .Where(d => d.AnnouncementId == id)
                .ToListAsync();
            _dbContext.AnnouncementDismissals.RemoveRange(dismissals);
        }

        await _dbContext.SaveChangesAsync();

        // Create notifications for active users when NotifyUsers=true and announcement is currently active
        if (request.NotifyUsers && ComputeStatus(announcement, utcNow) == "Active")
        {
            try
            {
                var activeUsers = await _dbContext.Users
                    .Where(u => u.IsActive)
                    .Select(u => u.Id)
                    .ToListAsync();

                foreach (var userId in activeUsers)
                {
                    try
                    {
                        await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
                        {
                            UserId = userId,
                            Category = NotificationCategory.System,
                            Title = "Announcement Updated",
                            Message = announcement.Title
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to create notification for user {UserId} for announcement {AnnouncementId}.",
                            userId, announcement.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query active users for notification delivery for announcement {AnnouncementId}.",
                    announcement.Id);
            }
        }

        // Audit log the update with old/new value tracking
        try
        {
            var after = AuditChangeHelper.Snapshot(announcement, AnnouncementAuditFields);
            var (oldValues, newValues) = AuditChangeHelper.ComputeChanges(before, after);

            await _auditLogService.LogAsync(new AuditLogRequest
            {
                UserId = _currentUserAccessor.UserId,
                ActionType = AuditActionType.AnnouncementUpdated,
                EntityType = AuditEntityType.Announcement,
                EntityId = announcement.Id.ToString(),
                EntityName = announcement.Title,
                Description = $"Updated announcement '{announcement.Title}'.",
                OldValues = oldValues,
                NewValues = newValues,
                IpAddress = _currentUserAccessor.IpAddress
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to audit log update of announcement {AnnouncementId}.", announcement.Id);
        }

        return MapToDto(announcement, utcNow);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id)
    {
        var announcement = await _dbContext.Announcements.FindAsync(id);
        if (announcement is null)
            throw new KeyNotFoundException($"Announcement with ID '{id}' was not found.");

        _dbContext.Announcements.Remove(announcement);
        await _dbContext.SaveChangesAsync();

        // Audit log the deletion (dismissals cascade-delete automatically via EF config)
        try
        {
            await _auditLogService.LogAsync(new AuditLogRequest
            {
                UserId = _currentUserAccessor.UserId,
                ActionType = AuditActionType.AnnouncementDeleted,
                EntityType = AuditEntityType.Announcement,
                EntityId = announcement.Id.ToString(),
                EntityName = announcement.Title,
                Description = $"Deleted announcement '{announcement.Title}'.",
                IpAddress = _currentUserAccessor.IpAddress
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to audit log deletion of announcement {AnnouncementId}.", announcement.Id);
        }
    }

    #endregion

    #region Query Operations

    /// <inheritdoc />
    public async Task<List<AnnouncementDto>> GetAllAsync()
    {
        var utcNow = DateTime.UtcNow;

        var announcements = await _dbContext.Announcements
            .AsNoTracking()
            .Include(a => a.CreatedByUser)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync();

        return announcements.Select(a => MapToDto(a, utcNow)).ToList();
    }

    /// <inheritdoc />
    public async Task<List<AnnouncementDto>> GetActiveForUserAsync(string userId)
    {
        var utcNow = DateTime.UtcNow;

        // Query announcements that are currently active:
        // IsActive=true, (StartsAtUtc is null OR StartsAtUtc <= utcNow), (ExpiresAtUtc is null OR ExpiresAtUtc > utcNow)
        // Exclude announcements dismissed by this specific user (left join with AnnouncementDismissals)
        var announcements = await _dbContext.Announcements
            .AsNoTracking()
            .Include(a => a.CreatedByUser)
            .Where(a => a.IsActive
                && (!a.StartsAtUtc.HasValue || a.StartsAtUtc.Value <= utcNow)
                && (!a.ExpiresAtUtc.HasValue || a.ExpiresAtUtc.Value > utcNow))
            .Where(a => !_dbContext.AnnouncementDismissals
                .Any(d => d.AnnouncementId == a.Id && d.UserId == userId))
            .ToListAsync();

        // Map to DTOs and order by priority (Severity descending, CreatedAtUtc descending)
        var dtos = announcements.Select(a => MapToDto(a, utcNow));
        return OrderByPriority(dtos).ToList();
    }

    /// <inheritdoc />
    public async Task<PagedResult<AnnouncementDto>> GetForListPageAsync(AnnouncementQueryParams queryParams)
    {
        var utcNow = DateTime.UtcNow;
        var thirtyDaysAgo = utcNow.AddDays(-30);

        // Base query: active announcements + expired within 30 days
        var query = _dbContext.Announcements
            .AsNoTracking()
            .Include(a => a.CreatedByUser)
            .Where(a =>
                // Currently active: IsActive=true AND within date range
                (a.IsActive
                    && (!a.StartsAtUtc.HasValue || a.StartsAtUtc.Value <= utcNow)
                    && (!a.ExpiresAtUtc.HasValue || a.ExpiresAtUtc.Value > utcNow))
                ||
                // Expired within 30 days: ExpiresAtUtc is not null AND < utcNow AND >= 30 days ago
                (a.ExpiresAtUtc.HasValue
                    && a.ExpiresAtUtc.Value < utcNow
                    && a.ExpiresAtUtc.Value >= thirtyDaysAgo));

        // Apply severity filter
        if (queryParams.Severity.HasValue)
        {
            query = query.Where(a => a.Severity == queryParams.Severity.Value);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply ordering and pagination
        var pageSize = Math.Clamp(queryParams.PageSize, 1, 50);
        var page = Math.Max(queryParams.Page, 1);

        var announcements = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<AnnouncementDto>
        {
            Items = announcements.Select(a => MapToDto(a, utcNow)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    #endregion

    #region Dismissal

    /// <inheritdoc />
    public async Task DismissAsync(string userId, Guid announcementId)
    {
        // Idempotent: check if dismissal already exists for this user+announcement pair
        var existingDismissal = await _dbContext.AnnouncementDismissals
            .FindAsync(userId, announcementId);

        if (existingDismissal is not null)
            return;

        // Create AnnouncementDismissal record
        var dismissal = new AnnouncementDismissal
        {
            UserId = userId,
            AnnouncementId = announcementId,
            DismissedAtUtc = DateTime.UtcNow
        };

        _dbContext.AnnouncementDismissals.Add(dismissal);
        await _dbContext.SaveChangesAsync();
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Computes the effective status of an announcement based on its scheduling fields and the current UTC time.
    /// Evaluation order: Expired → Scheduled → Active → Draft.
    /// </summary>
    /// <param name="announcement">The announcement entity to evaluate.</param>
    /// <param name="utcNow">The reference UTC time for status computation.</param>
    /// <returns>A status string: "Expired", "Scheduled", "Active", or "Draft".</returns>
    private static string ComputeStatus(Announcement announcement, DateTime utcNow)
    {
        if (announcement.ExpiresAtUtc is not null && utcNow >= announcement.ExpiresAtUtc)
            return "Expired";

        if (announcement.StartsAtUtc is not null && utcNow < announcement.StartsAtUtc)
            return "Scheduled";

        if (announcement.IsActive)
            return "Active";

        return "Draft";
    }

    /// <summary>
    /// Maps an <see cref="Announcement"/> entity to an <see cref="AnnouncementDto"/>,
    /// including computing the Status field and mapping entity.Content to dto.Message.
    /// </summary>
    /// <param name="announcement">The announcement entity to map.</param>
    /// <param name="utcNow">The reference UTC time for status computation.</param>
    /// <returns>A fully populated <see cref="AnnouncementDto"/>.</returns>
    private static AnnouncementDto MapToDto(Announcement announcement, DateTime utcNow)
    {
        return new AnnouncementDto
        {
            Id = announcement.Id,
            Title = announcement.Title,
            Message = announcement.Content,
            DisplayType = announcement.DisplayType,
            Severity = announcement.Severity,
            StartsAtUtc = announcement.StartsAtUtc,
            ExpiresAtUtc = announcement.ExpiresAtUtc,
            IsActive = announcement.IsActive,
            NotifyUsers = announcement.NotifyUsers,
            Status = ComputeStatus(announcement, utcNow),
            CreatedAtUtc = announcement.CreatedAtUtc,
            UpdatedAtUtc = announcement.UpdatedAtUtc,
            CreatedByUserName = announcement.CreatedByUser?.UserName
        };
    }

    /// <summary>
    /// Orders announcements by priority: Severity descending (Critical > Warning > Info),
    /// then by CreatedAtUtc descending (newest first for ties within the same severity).
    /// </summary>
    /// <param name="announcements">The collection of announcement DTOs to order.</param>
    /// <returns>The announcements ordered by priority.</returns>
    private static IEnumerable<AnnouncementDto> OrderByPriority(IEnumerable<AnnouncementDto> announcements)
    {
        return announcements
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.CreatedAtUtc);
    }

    #endregion
}
