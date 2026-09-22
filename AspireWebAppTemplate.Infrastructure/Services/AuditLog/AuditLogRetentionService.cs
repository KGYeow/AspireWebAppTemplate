using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AspireWebAppTemplate.Application.Features.AuditLog;
using AspireWebAppTemplate.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AspireWebAppTemplate.Infrastructure.Services.AuditLog;

/// <summary>
/// Implements the <see cref="IAuditLogRetentionService"/> interface to manage audit-log data
/// retention by purging entries older than the configured retention period from the
/// <c>AuditLogEntries</c> database table.
/// </summary>
/// <remarks>
/// Registered as a scoped service to align with the per-request <see cref="ApplicationDbContext"/>
/// lifetime. This service depends only on the database context and application configuration —
/// it carries no identity, display-name, or user-management concerns — so it can be composed into a
/// short-lived batch host without pulling in ASP.NET Core Identity. The <see cref="PurgeOldEntriesAsync"/>
/// method propagates exceptions so that calling code (e.g., background jobs) can implement retry logic.
/// </remarks>
public class AuditLogRetentionService : IAuditLogRetentionService
{
    #region Constructor

    /// <summary>
    /// The application database context used to delete expired audit log entries.
    /// </summary>
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// The application configuration used to read the audit-log retention settings.
    /// </summary>
    private readonly IConfiguration _configuration;

    /// <summary>
    /// The logger instance used to record informational messages and validation warnings.
    /// </summary>
    private readonly ILogger<AuditLogRetentionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogRetentionService"/> class.
    /// </summary>
    /// <param name="dbContext">The application database context for purging audit entries.</param>
    /// <param name="configuration">The application configuration for reading retention settings.</param>
    /// <param name="logger">The logger instance for recording informational and warning messages.</param>
    public AuditLogRetentionService(
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        ILogger<AuditLogRetentionService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    #endregion

    #region Retention

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

    #endregion

    #region Private Helpers

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

    #endregion
}
