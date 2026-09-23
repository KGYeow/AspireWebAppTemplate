using AspireWebAppTemplate.Scheduler.Constants;
using AspireWebAppTemplate.Scheduler.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace AspireWebAppTemplate.Scheduler.Hosting;

/// <summary>
/// Selects and runs a single batch job against a built host, owns the structured console envelope
/// (header, status, timing, result footer) and the exception handling, and maps the outcome to a
/// process exit code that Windows Task Scheduler records as the "Last Run Result".
/// </summary>
/// <remarks>
/// The runner owns the run envelope so that jobs never format headers/footers or map exit codes
/// themselves: it generates a correlation run id, opens a logging scope, writes the header and the
/// STARTING/terminal status lines, times the run, and translates success/cancellation/failure/usage
/// into <see cref="ExitCodes"/>. A job reports only its own facts through <see cref="IJobProgress"/>
/// and returns <see cref="ExitCodes.Success"/>; letting exceptions propagate is how a job signals
/// failure (the runner logs the full stack trace and prints the human-readable error detail).
/// </remarks>
public static class JobRunner
{
    /// <summary>The application name shown in the console header.</summary>
    private const string AppName = "AspireWebAppTemplate.Scheduler";

    /// <summary>
    /// Resolves the job named on the command line, runs it once within a DI scope, and returns the
    /// mapped process exit code.
    /// </summary>
    /// <param name="host">The built host whose service provider resolves the registered jobs.</param>
    /// <param name="args">The command-line arguments; the first non-empty value selects the job.</param>
    /// <returns>
    /// The job's own exit code on success, or an <see cref="ExitCodes"/> value for the
    /// no-job/unknown-job (<see cref="ExitCodes.InvalidUsage"/>), cancelled
    /// (<see cref="ExitCodes.Cancelled"/>), and unhandled-failure (<see cref="ExitCodes.JobFailed"/>) paths.
    /// </returns>
    public static async Task<int> RunAsync(IHost host, string[] args)
    {
        // Cancellation: Ctrl+C / SIGTERM cancels the running job gracefully.
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Scheduler");

        // Resolve the requested job name (first non-empty argument).
        var jobName = args.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a));

        // Jobs depend on scoped services (DbContext, feature services), so they must be resolved from a
        // DI scope, never the root provider. One scope covers the whole run.
        using var scope = host.Services.CreateScope();
        var registeredJobs = scope.ServiceProvider.GetServices<IScheduledJob>().ToList();

        if (string.IsNullOrWhiteSpace(jobName))
        {
            AnsiConsole.MarkupLine("[yellow]No job specified.[/] Usage: [grey]Scheduler.exe <job-name>[/]");
            SchedulerConsole.PrintJobTable(registeredJobs);
            logger.LogWarning("Scheduler invoked with no job name.");
            return ExitCodes.InvalidUsage;
        }

        var job = registeredJobs.FirstOrDefault(j => string.Equals(j.Name, jobName, StringComparison.OrdinalIgnoreCase));
        if (job is null)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Unknown job:[/] '{jobName}'.");
            SchedulerConsole.PrintJobTable(registeredJobs);
            logger.LogError("Unknown job requested: {JobName}", jobName);
            return ExitCodes.InvalidUsage;
        }

        // Correlation id ties the console output to the structured logs for this run.
        var runId = Guid.NewGuid();
        var environment = host.Services.GetRequiredService<IHostEnvironment>().EnvironmentName;
        var startUtc = DateTime.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Open a logging scope so every log line for this run carries the run id.
        using var logScope = logger.BeginScope(new Dictionary<string, object> { ["RunId"] = runId });

        SchedulerConsole.WriteHeader(AppName, environment, job.Name, runId, startUtc);
        SchedulerConsole.WriteStatus(JobStatus.Starting, job.Name);
        logger.LogInformation("Starting job {JobName} (RunId {RunId}, Environment {Environment}).",
            job.Name, runId, environment);

        try
        {
            // The job was resolved from the scope created above, so its scoped dependencies are valid.
            var exitCode = await job.RunAsync(cts.Token);
            stopwatch.Stop();

            var status = exitCode == ExitCodes.Success ? JobStatus.Success : JobStatus.Failed;
            SchedulerConsole.WriteStatus(status, job.Name, stopwatch.Elapsed);
            SchedulerConsole.WriteResultFooter(status, exitCode, stopwatch.Elapsed);
            logger.LogInformation("Job {JobName} finished with status {Status} and exit code {ExitCode} in {DurationMs} ms.",
                job.Name, status, exitCode, stopwatch.ElapsedMilliseconds);
            return exitCode;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            stopwatch.Stop();
            SchedulerConsole.WriteStatus(JobStatus.Cancelled, job.Name, stopwatch.Elapsed);
            SchedulerConsole.WriteResultFooter(JobStatus.Cancelled, ExitCodes.Cancelled, stopwatch.Elapsed);
            logger.LogWarning("Job {JobName} was cancelled after {DurationMs} ms.", job.Name, stopwatch.ElapsedMilliseconds);
            return ExitCodes.Cancelled;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            SchedulerConsole.WriteStatus(JobStatus.Failed, job.Name, stopwatch.Elapsed);
            SchedulerConsole.WriteError(ex.Message, ex.GetType().FullName ?? ex.GetType().Name, runId);
            SchedulerConsole.WriteResultFooter(JobStatus.Failed, ExitCodes.JobFailed, stopwatch.Elapsed);
            logger.LogError(ex, "Job {JobName} failed with an unhandled exception after {DurationMs} ms.",
                job.Name, stopwatch.ElapsedMilliseconds);
            return ExitCodes.JobFailed;
        }
    }
}