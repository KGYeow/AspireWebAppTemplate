using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.ApiService.Data;
using AspireWebAppTemplate.ApiService.Data.Entities;
using AspireWebAppTemplate.Core.Contracts.AuditLog;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AspireWebAppTemplate.ApiService.Services;

/// <summary>
/// Implements the <see cref="IAuditLogService"/> interface to record significant user and system
/// actions into the <c>AuditLogEntries</c> database table and manage data retention through
/// periodic purging of old entries.
/// </summary>
/// <remarks>
/// Registered as a scoped service to align with the per-request <see cref="ApplicationDbContext"/>
/// lifetime in Blazor Server circuits. The <see cref="LogAsync"/> method swallows database errors
/// to ensure audit failures never disrupt the primary user operation. The <see cref="PurgeOldEntriesAsync"/>
/// method propagates exceptions so that calling code (e.g., background jobs) can implement retry logic.
/// </remarks>
public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AuditLogService> _logger;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogService"/> class.
    /// </summary>
    /// <param name="dbContext">The application database context for persisting audit entries.</param>
    /// <param name="userManager">The ASP.NET Core Identity user manager for resolving user display names.</param>
    /// <param name="logger">The logger instance for recording errors, warnings, and informational messages.</param>
    /// <param name="configuration">The application configuration for reading retention settings.</param>
    public AuditLogService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<AuditLogService> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
        _configuration = configuration;
    }

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

    /// <inheritdoc />
    public async Task<int> PurgeOldEntriesAsync()
    {
        // Read retention days from configuration with validation (1–3650 range, fallback to 365)
        var retentionDays = GetValidatedRetentionDays();

        // Calculate the cutoff date: entries older than this will be purged
        var cutoffDate = DateTime.UtcNow - TimeSpan.FromDays(retentionDays);

        // Delete all entries with a Timestamp older than the retention cutoff.
        // Unlike LogAsync, database exceptions are propagated so the caller can handle retry logic.
        var purgedCount = await _dbContext.AuditLogEntries
            .Where(e => e.Timestamp < cutoffDate)
            .ExecuteDeleteAsync();

        _logger.LogInformation(
            "Purged {PurgedCount} audit log entries older than {RetentionDays} days (cutoff: {CutoffDate:O})",
            purgedCount,
            retentionDays,
            cutoffDate);

        return purgedCount;
    }

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
    /// Reads and validates the <c>AuditLog:RetentionDays</c> configuration value.
    /// Returns the configured value if it is a valid integer within the range 1–3650;
    /// otherwise logs a warning and falls back to the default of 365 days.
    /// </summary>
    /// <returns>A validated retention period in days (1–3650).</returns>
    private int GetValidatedRetentionDays()
    {
        const int defaultRetentionDays = 365;
        const int minRetentionDays = 1;
        const int maxRetentionDays = 3650;

        var configValue = _configuration["AuditLog:RetentionDays"];

        // Missing configuration value — use default
        if (string.IsNullOrWhiteSpace(configValue))
        {
            _logger.LogWarning(
                "AuditLog:RetentionDays configuration is missing. Using default value of {DefaultDays} days.",
                defaultRetentionDays);
            return defaultRetentionDays;
        }

        // Non-numeric value — use default
        if (!int.TryParse(configValue, out var retentionDays))
        {
            _logger.LogWarning(
                "AuditLog:RetentionDays configuration value '{ConfigValue}' is not a valid integer. Using default value of {DefaultDays} days.",
                configValue,
                defaultRetentionDays);
            return defaultRetentionDays;
        }

        // Out of valid range — use default
        if (retentionDays < minRetentionDays || retentionDays > maxRetentionDays)
        {
            _logger.LogWarning(
                "AuditLog:RetentionDays configuration value '{RetentionDays}' is outside the valid range ({Min}–{Max}). Using default value of {DefaultDays} days.",
                retentionDays,
                minRetentionDays,
                maxRetentionDays,
                defaultRetentionDays);
            return defaultRetentionDays;
        }

        return retentionDays;
    }
}
