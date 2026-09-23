namespace AspireWebAppTemplate.Scheduler.Constants;

/// <summary>
/// The lifecycle status of a scheduled job run, used for structured console output and log messages.
/// </summary>
/// <remarks>
/// The terminal statuses map 1:1 to <see cref="ExitCodes"/>:
/// <see cref="Success"/> -> <see cref="ExitCodes.Success"/> (0),
/// <see cref="Failed"/> -> <see cref="ExitCodes.JobFailed"/> (1),
/// <see cref="Cancelled"/> -> <see cref="ExitCodes.Cancelled"/> (3).
/// There is deliberately no <c>Running</c> status: the Scheduler runs one job to completion per launch,
/// so a live "running" state adds noise between <see cref="Starting"/> and the terminal result.
/// </remarks>
public enum JobStatus
{
    /// <summary>The job is about to begin execution.</summary>
    Starting,

    /// <summary>The job completed successfully (exit code 0).</summary>
    Success,

    /// <summary>The job failed — it threw or returned a non-zero exit code (exit code 1).</summary>
    Failed,

    /// <summary>The job run was cancelled (Ctrl+C / SIGTERM) before completion (exit code 3).</summary>
    Cancelled
}