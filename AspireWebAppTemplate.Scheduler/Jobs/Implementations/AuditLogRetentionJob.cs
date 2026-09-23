using AspireWebAppTemplate.Application.Features.AuditLog;
using AspireWebAppTemplate.Scheduler.Constants;
using AspireWebAppTemplate.Scheduler.Jobs;

namespace AspireWebAppTemplate.Scheduler.Jobs.Implementations;

/// <summary>
/// Deletes audit-log entries older than the configured retention period by delegating to
/// <see cref="IAuditLogRetentionService.PurgeOldEntriesAsync"/>. This is a template-owned maintenance
/// job (no business-domain logic) that demonstrates the job pattern end to end.
/// </summary>
/// <remarks>
/// Retention is read from <c>AuditLog:RetentionDays</c> by the service layer. This job is a thin
/// trigger: it reports its progress through <see cref="IJobProgress"/> and returns
/// <see cref="ExitCodes.Success"/> on completion. It does NOT catch exceptions or format the run
/// envelope — the runner owns status/timing/exit-code mapping and logs the full stack trace on
/// failure, so any exception here surfaces as a FAILED result. Run via <c>Scheduler.exe purge-audit-logs</c>.
/// </remarks>
public sealed class AuditLogRetentionJob : IScheduledJob
{
    #region Constructor

    /// <summary>The audit-log retention service that performs the retention purge.</summary>
    private readonly IAuditLogRetentionService _auditLogRetentionService;

    /// <summary>The per-run progress reporter (writes to both the logger and the console).</summary>
    private readonly IJobProgress _progress;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogRetentionJob"/> class.
    /// </summary>
    /// <param name="auditLogRetentionService">The audit-log retention service used to purge old entries.</param>
    /// <param name="progress">The per-run progress reporter.</param>
    public AuditLogRetentionJob(IAuditLogRetentionService auditLogRetentionService, IJobProgress progress)
    {
        _auditLogRetentionService = auditLogRetentionService;
        _progress = progress;
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
        int purged;

        // Phase usage is optional; shown here to demonstrate the pattern for future job authors.
        using (_progress.Phase("Purge old audit-log entries"))
        {
            purged = await _auditLogRetentionService.PurgeOldEntriesAsync();
        }

        _progress.Info($"Purged {purged:N0} audit-log entrie(s).");
        return ExitCodes.Success;
    }

    #endregion
}