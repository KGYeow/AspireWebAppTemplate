using Microsoft.Extensions.Logging;
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Application.Extensions;
using AspireWebAppTemplate.Domain.Constants;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Features.AuditLog;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AspireWebAppTemplate.Infrastructure.Services.AuditLog;

/// <summary>
/// Implements the <see cref="IAuditLogService"/> interface to record significant user and system
/// actions into the <c>AuditLogEntries</c> database table and to query, filter, and export
/// audit log entries.
/// </summary>
/// <remarks>
/// Registered as a scoped service to align with the per-request <see cref="ApplicationDbContext"/>
/// lifetime in Blazor Server circuits. The <see cref="LogAsync"/> method swallows database errors
/// to ensure audit failures never disrupt the primary user operation.
/// </remarks>
public class AuditLogService : IAuditLogService
{
    #region Constructor

    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AuditLogService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogService"/> class.
    /// </summary>
    /// <param name="dbContext">The application database context for persisting audit entries.</param>
    /// <param name="userManager">The ASP.NET Core Identity user manager for resolving user display names.</param>
    /// <param name="logger">The logger instance for recording errors, warnings, and informational messages.</param>
    public AuditLogService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<AuditLogService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
    }

    #endregion

    #region Write Operations

    /// <inheritdoc />
    public async Task LogAsync(AuditLogRequest request)
    {
        try
        {
            // Resolve user display name: existing user → DisplayName, unknown → userId, null → empty string
            var displayName = await ResolveDisplayNameAsync(request.UserId);

            var entry = new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId ?? string.Empty,
                UserDisplayName = displayName,
                ActionType = request.ActionType,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                EntityName = request.EntityName,
                Description = request.Description,
                OldValues = request.OldValues,
                NewValues = request.NewValues,
                IpAddress = request.IpAddress,
                Timestamp = DateTime.UtcNow
            };

            _dbContext.AuditLogEntries.Add(entry);
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Swallow database exceptions to ensure audit failures never disrupt the primary operation.
            // Log at Error level with enough context to diagnose the issue later.
            _logger.LogError(
                ex,
                "Failed to persist audit log entry. ActionType: {ActionType}, EntityType: {EntityType}, EntityId: {EntityId}",
                request.ActionType,
                request.EntityType,
                request.EntityId);
        }
    }

    #endregion

    #region Query Operations

    /// <inheritdoc />
    public async Task<PagedResult<AuditLogEntryDto>> SearchAsync(AuditLogQueryParams queryParams)
    {
        var query = _dbContext.AuditLogEntries.AsNoTracking().AsQueryable();

        query = ApplyFilters(query, queryParams);

        var totalCount = await query.CountAsync();

        var entries = await query
            .ApplySort(queryParams.SortBy, queryParams.SortDescending, q => q.OrderByDescending(e => e.Timestamp))
            .Skip(queryParams.Page * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .Select(e => new AuditLogEntryDto
            {
                Id = e.Id,
                UserId = e.UserId,
                UserDisplayName = e.UserDisplayName,
                ActionType = e.ActionType,
                EntityType = e.EntityType,
                EntityId = e.EntityId,
                EntityName = e.EntityName,
                Description = e.Description,
                OldValues = e.OldValues,
                NewValues = e.NewValues,
                IpAddress = e.IpAddress,
                Timestamp = e.Timestamp
            })
            .ToListAsync();

        return new PagedResult<AuditLogEntryDto>
        {
            Items = entries,
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<AuditLogEntryDto> GetByIdAsync(Guid id)
    {
        var entry = await _dbContext.AuditLogEntries
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new AuditLogEntryDto
            {
                Id = e.Id,
                UserId = e.UserId,
                UserDisplayName = e.UserDisplayName,
                ActionType = e.ActionType,
                EntityType = e.EntityType,
                EntityId = e.EntityId,
                EntityName = e.EntityName,
                Description = e.Description,
                OldValues = e.OldValues,
                NewValues = e.NewValues,
                IpAddress = e.IpAddress,
                Timestamp = e.Timestamp
            })
            .FirstOrDefaultAsync();

        if (entry is null)
            throw new KeyNotFoundException($"Audit log entry with ID '{id}' was not found.");

        return entry;
    }

    /// <inheritdoc />
    public async Task<List<AuditLogEntryDto>> GetForExportAsync(AuditLogQueryParams queryParams)
    {
        var query = _dbContext.AuditLogEntries.AsNoTracking().AsQueryable();

        query = ApplyFilters(query, queryParams);

        return await query
            .OrderByDescending(e => e.Timestamp)
            .Take(ExportDefaults.MaxExportRows)
            .Select(e => new AuditLogEntryDto
            {
                Id = e.Id,
                UserId = e.UserId,
                UserDisplayName = e.UserDisplayName,
                ActionType = e.ActionType,
                EntityType = e.EntityType,
                EntityId = e.EntityId,
                EntityName = e.EntityName,
                Description = e.Description,
                OldValues = e.OldValues,
                NewValues = e.NewValues,
                IpAddress = e.IpAddress,
                Timestamp = e.Timestamp
            })
            .ToListAsync();
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Resolves the display name for the given user ID.
    /// Returns the user's <see cref="ApplicationUser.DisplayName"/> if the user exists,
    /// the userId string itself if the user cannot be found, or an empty string if userId is null.
    /// </summary>
    /// <param name="userId">The user identifier to resolve, or null for system events.</param>
    /// <returns>The resolved display name string.</returns>
    private async Task<string> ResolveDisplayNameAsync(string? userId)
    {
        // Null userId means a system event with no associated user
        if (userId is null)
        {
            return string.Empty;
        }

        // Attempt to find the user by their ID
        var user = await _userManager.FindByIdAsync(userId);

        if (user is not null)
        {
            // Existing user found — use their DisplayName (falling back to empty if null)
            return user.DisplayName ?? string.Empty;
        }

        // User not found in the system — use the userId string as the display name
        return userId;
    }

    /// <summary>
    /// Applies all optional filter criteria from <paramref name="queryParams"/> to the query.
    /// Consolidates search term (case-insensitive partial match against UserDisplayName, EntityName,
    /// Description, and EntityId), action type, entity type, and date range filters into a single method.
    /// </summary>
    /// <param name="query">The base queryable to apply filters to.</param>
    /// <param name="queryParams">The query parameters containing optional filter criteria.</param>
    /// <returns>The filtered queryable with all applicable predicates applied.</returns>
    private static IQueryable<AuditLogEntry> ApplyFilters(IQueryable<AuditLogEntry> query, AuditLogQueryParams queryParams)
    {
        if (!string.IsNullOrWhiteSpace(queryParams.SearchTerm))
        {
            var term = queryParams.SearchTerm.ToLower();
            query = query.Where(e =>
                e.UserDisplayName.ToLower().Contains(term) ||
                e.EntityName.ToLower().Contains(term) ||
                e.Description.ToLower().Contains(term) ||
                e.EntityId.ToLower().Contains(term));
        }

        if (queryParams.ActionType.HasValue)
            query = query.Where(e => e.ActionType == queryParams.ActionType.Value);

        if (queryParams.EntityType.HasValue)
            query = query.Where(e => e.EntityType == queryParams.EntityType.Value);

        if (queryParams.DateStart.HasValue)
            query = query.Where(e => e.Timestamp >= queryParams.DateStart.Value);

        if (queryParams.DateEnd.HasValue)
            query = query.Where(e => e.Timestamp <= queryParams.DateEnd.Value);

        return query;
    }

    #endregion
}
