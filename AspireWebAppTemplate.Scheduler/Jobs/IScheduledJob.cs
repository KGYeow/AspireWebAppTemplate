namespace AspireWebAppTemplate.Scheduler.Jobs;

/// <summary>
/// A single batch job that the Scheduler can execute. Each job is selected by its
/// <see cref="Name"/> from the command line, runs once, and returns a process exit code.
/// </summary>
/// <remarks>
/// Jobs are the Scheduler''s only unit of work. A job is a <em>trigger</em> into the
/// Application/Infrastructure service layer — it must NOT contain business logic or access
/// the database directly. Instead it calls existing Application service interfaces
/// (e.g., <c>IAuditLogService</c>), keeping all business rules in the service layer.
/// Jobs are resolved from DI within a per-run scope, so they may depend on scoped services
/// such as <c>DbContext</c> and the feature services.
/// </remarks>
public interface IScheduledJob
{
    /// <summary>
    /// The unique, case-insensitive name used to select this job from the command line
    /// (e.g., <c>Scheduler.exe purge-audit-logs</c>). Use kebab-case.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// A short human-readable description shown in the job listing.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Executes the job exactly once.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancellation token signalled on Ctrl+C / SIGTERM. Long-running jobs should observe it.
    /// </param>
    /// <returns>
    /// A process exit code: <c>0</c> for success, non-zero for failure. The returned value
    /// becomes the Scheduler''s exit code, which Windows Task Scheduler records as the
    /// "Last Run Result".
    /// </returns>
    Task<int> RunAsync(CancellationToken cancellationToken);
}