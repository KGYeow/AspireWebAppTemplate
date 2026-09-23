namespace AspireWebAppTemplate.Scheduler.Constants;

/// <summary>
/// Process exit codes returned to Windows Task Scheduler (recorded as "Last Run Result").
/// Keep this contract stable so operators can key alerts off specific codes.
/// </summary>
public static class ExitCodes
{
    /// <summary>The job completed successfully.</summary>
    public const int Success = 0;

    /// <summary>The job failed during execution (an unhandled exception was caught).</summary>
    public const int JobFailed = 1;

    /// <summary>No job name was supplied, or the supplied name did not match a known job.</summary>
    public const int InvalidUsage = 2;

    /// <summary>The run was cancelled (Ctrl+C / SIGTERM) before completion.</summary>
    public const int Cancelled = 3;
}