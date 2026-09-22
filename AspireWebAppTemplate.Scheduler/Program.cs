using AspireWebAppTemplate.Scheduler;

// -----------------------------------------------------------------------------------------------
// AspireWebAppTemplate.Scheduler
//
// A short-lived, Windows-Task-Scheduler-triggered batch runner. It builds a focused DI graph
// (only what its jobs consume — ApplicationDbContext + the audit-log retention service), selects
// ONE job by command-line name, runs it once, and exits with a process code that Task Scheduler
// records.
//
//   Windows Task Scheduler -> Scheduler.exe <job-name> -> run one job -> log -> exit code
//
// This is intentionally NOT a Worker Service: there is no continuous loop and no in-process
// scheduling — Task Scheduler owns timing, retries, and run history.
// -----------------------------------------------------------------------------------------------

/// <summary>
/// Process entry point for the Scheduler console host.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Builds and configures the host, resolves the requested job, runs it once, and returns the
    /// resulting process exit code.
    /// </summary>
    /// <param name="args">Command-line arguments; the first non-empty value selects the job to run.</param>
    /// <returns>The process exit code recorded by Windows Task Scheduler as the "Last Run Result".</returns>
    private static async Task<int> Main(string[] args)
    {
        // 1. Build/configure the host (Aspire defaults, config, focused infrastructure, job registrations).
        using var host = SchedulerHostBuilder.Build(args);

        // 2. Resolve the requested job, 3. execute it once, and 4. return the mapped exit code.
        return await JobRunner.RunAsync(host, args);
    }
}
