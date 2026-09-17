using AspireWebAppTemplate.Application.Features.AuditLog;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace AspireWebAppTemplate.Scheduler.Jobs;

/// <summary>
/// Deletes audit-log entries older than the configured retention period by delegating to
/// <see cref="IAuditLogService.PurgeOldEntriesAsync"/>. This is a template-owned maintenance
/// job (no business-domain logic) that demonstrates the job pattern end to end.
/// </summary>
/// <remarks>
/// Retention is read from <c>AuditLog:RetentionDays</c> by the service layer. This job is a thin
/// trigger: it calls the Application service, reports the result, and maps success/failure to an
/// exit code. Run via <c>Scheduler.exe purge-audit-logs</c>.
/// </remarks>
public sealed class AuditLogRetentionJob : IScheduledJob
{
    #region Constructor

    /// <summary>The audit log service that performs the retention purge.</summary>
    private readonly IAuditLogService _auditLogService;

    /// <summary>The logger used for structured, Task-Scheduler-diagnosable output.</summary>
    private readonly ILogger<AuditLogRetentionJob> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogRetentionJob"/> class.
    /// </summary>
    /// <param name="auditLogService">The audit log service used to purge old entries.</param>
    /// <param name="logger">The logger instance.</param>
    public AuditLogRetentionJob(IAuditLogService auditLogService, ILogger<AuditLogRetentionJob> logger)
    {
        _auditLogService = auditLogService;
        _logger = logger;
    }

    #endregion

    #region IScheduledJob

    /// <inheritdoc />
    public string Name => "purge-audit-logs";

    /// <inheritdoc />
    public string Description => "Deletes audit-log entries older than the configured retention period.";

    /// <inheritdoc />
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            var purged = await _auditLogService.PurgeOldEntriesAsync();

            _logger.LogInformation("Audit-log retention purge completed. Entries purged: {PurgedCount}", purged);
            AnsiConsole.MarkupLineInterpolated($"[green]Purged[/] {purged} audit-log entrie(s).");

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit-log retention purge failed.");
            return ExitCodes.JobFailed;
        }
    }

    #endregion
}