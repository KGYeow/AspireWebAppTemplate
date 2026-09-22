using AspireWebAppTemplate.Scheduler.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace AspireWebAppTemplate.Scheduler;

/// <summary>
/// Selects and runs a single batch job against a built host, then maps the outcome to a process
/// exit code that Windows Task Scheduler records as the "Last Run Result".
/// </summary>
/// <remarks>
/// Encapsulates the run-time flow that is orthogonal to host composition: cancellation wiring
/// (Ctrl+C / SIGTERM), resolving the requested job by name from the command line, running it inside
/// a per-run DI scope, and translating success/cancellation/failure/usage into <see cref="ExitCodes"/>.
/// </remarks>
public static class JobRunner
{
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

        // Jobs depend on scoped services (DbContext, UserManager, feature services), so they must be
        // resolved from a DI scope, never the root provider. One scope covers the whole run.
        using var scope = host.Services.CreateScope();
        var registeredJobs = scope.ServiceProvider.GetServices<IScheduledJob>().ToList();

        if (string.IsNullOrWhiteSpace(jobName))
        {
            AnsiConsole.MarkupLine("[yellow]No job specified.[/] Usage: [grey]Scheduler.exe <job-name>[/]");
            JobConsole.PrintJobTable(registeredJobs);
            logger.LogWarning("Scheduler invoked with no job name.");
            return ExitCodes.InvalidUsage;
        }

        var job = registeredJobs.FirstOrDefault(j => string.Equals(j.Name, jobName, StringComparison.OrdinalIgnoreCase));
        if (job is null)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Unknown job:[/] '{jobName}'.");
            JobConsole.PrintJobTable(registeredJobs);
            logger.LogError("Unknown job requested: {JobName}", jobName);
            return ExitCodes.InvalidUsage;
        }

        logger.LogInformation("Starting job {JobName}", job.Name);
        AnsiConsole.Write(new Rule($"[blue]{job.Name}[/]").LeftJustified());

        try
        {
            // The job was resolved from the scope created above, so its scoped dependencies are valid.
            var exitCode = await job.RunAsync(cts.Token);

            logger.LogInformation("Job {JobName} finished with exit code {ExitCode}", job.Name, exitCode);
            AnsiConsole.MarkupLineInterpolated(
                $"[grey]Job[/] {job.Name} [grey]exited with code[/] {exitCode}.");
            return exitCode;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            logger.LogWarning("Job {JobName} was cancelled.", job.Name);
            AnsiConsole.MarkupLine("[yellow]Job cancelled.[/]");
            return ExitCodes.Cancelled;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobName} failed with an unhandled exception.", job.Name);
            AnsiConsole.MarkupLineInterpolated($"[red]Job failed:[/] {ex.Message}");
            return ExitCodes.JobFailed;
        }
    }
}
