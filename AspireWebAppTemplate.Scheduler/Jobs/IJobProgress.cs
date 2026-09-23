namespace AspireWebAppTemplate.Scheduler.Jobs;

/// <summary>
/// Per-run progress reporter passed to a running <see cref="IScheduledJob"/>. A job reports its
/// domain facts through this abstraction instead of writing to the console or logger directly.
/// </summary>
/// <remarks>
/// Each call writes to BOTH the structured <c>ILogger</c> (the durable diagnostic record) and the
/// human-readable console (a projection of the run) from a single call, so the two channels never
/// diverge. The job envelope — header, STARTING/SUCCESS/FAILED status, timing, exit-code mapping, and
/// the result footer — is owned by the runner; a job only reports informational messages, warnings,
/// and (optionally) phase boundaries. Phase usage is opt-in: jobs with no meaningful phases simply
/// call <see cref="Info"/>.
/// </remarks>
public interface IJobProgress
{
    /// <summary>Reports an informational message (logged and shown on the console).</summary>
    /// <param name="message">The message to report.</param>
    void Info(string message);

    /// <summary>Reports a non-fatal warning (logged and shown on the console).</summary>
    /// <param name="message">The warning message to report.</param>
    void Warn(string message);

    /// <summary>
    /// Begins a named, timed phase. Reports the phase start immediately and, when the returned handle
    /// is disposed, reports the phase end with its measured duration. Use with a <c>using</c> block so
    /// the phase is timed correctly even if the phase throws.
    /// </summary>
    /// <param name="name">The phase name (e.g., "Query eligible records").</param>
    /// <returns>A handle whose disposal marks the end of the phase.</returns>
    IDisposable Phase(string name);
}