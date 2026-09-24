using AspireWebAppTemplate.Scheduler.Constants;
using AspireWebAppTemplate.Scheduler.Jobs;
using Spectre.Console;

namespace AspireWebAppTemplate.Scheduler.Hosting;

/// <summary>
/// Owns all human-readable console formatting for the Scheduler: the execution header, per-line
/// status/info/phase output, the result footer, and the available-jobs table.
/// </summary>
/// <remarks>
/// This type is the single source of truth for console formatting so that jobs never format the
/// envelope themselves — <see cref="JobRunner"/> writes the header/footer/status and jobs report
/// progress through <see cref="IJobProgress"/>. Console output is a human-readable projection of the
/// run; the structured <c>ILogger</c> output remains the durable diagnostic record (the console is
/// largely absent when the Scheduler runs headless under Windows Task Scheduler). Uses Spectre.Console,
/// which degrades cleanly when output is redirected or non-interactive.
/// </remarks>
public static class SchedulerConsole
{
    #region Format constants

    /// <summary>Absolute timestamp format (UTC), used in the header and footer.</summary>
    private const string AbsoluteTimestampFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>Per-line timestamp prefix format (time only; the date lives in the header).</summary>
    private const string LineTimestampFormat = "HH:mm:ss";

    /// <summary>Duration format used consistently for job and phase durations.</summary>
    private const string DurationFormat = @"hh\:mm\:ss\.fff";

    /// <summary>Width of the primary (header/footer) separator rule.</summary>
    private const int SeparatorWidth = 60;

    /// <summary>Width the status label is padded to so body lines align.</summary>
    private const int StatusLabelWidth = 9;

    /// <summary>The heavy separator line for the header and footer boundaries.</summary>
    private static readonly string HeavySeparator = new('=', SeparatorWidth);

    /// <summary>The light separator line dividing the body from the result footer.</summary>
    private static readonly string LightSeparator = new('-', SeparatorWidth);

    #endregion

    #region Header & footer

    /// <summary>
    /// Writes the Scheduler execution header: application name, start time (UTC), environment,
    /// selected job, and the correlation run id.
    /// </summary>
    /// <param name="appName">The Scheduler application name.</param>
    /// <param name="environment">The hosting environment name (e.g., Production).</param>
    /// <param name="jobName">The name of the job selected for this run.</param>
    /// <param name="runId">The correlation id for this run (ties console output to structured logs).</param>
    /// <param name="startUtc">The UTC start time of the run.</param>
    public static void WriteHeader(string appName, string environment, string jobName, Guid runId, DateTime startUtc)
    {
        AnsiConsole.WriteLine(HeavySeparator);
        AnsiConsole.MarkupLineInterpolated($" [bold]{appName}[/]");
        AnsiConsole.WriteLine(HeavySeparator);
        AnsiConsole.MarkupLineInterpolated($" Start       : {startUtc.ToString(AbsoluteTimestampFormat)} UTC");
        AnsiConsole.MarkupLineInterpolated($" Environment : {environment}");
        AnsiConsole.MarkupLineInterpolated($" Job         : {jobName}");
        AnsiConsole.MarkupLineInterpolated($" Run Id      : {runId}");
        AnsiConsole.WriteLine(HeavySeparator);
    }

    /// <summary>
    /// Writes the result footer: the overall status, process exit code, and total duration.
    /// </summary>
    /// <param name="status">The terminal status of the run.</param>
    /// <param name="exitCode">The process exit code returned to the caller / Task Scheduler.</param>
    /// <param name="duration">The total run duration.</param>
    public static void WriteResultFooter(JobStatus status, int exitCode, TimeSpan duration)
    {
        AnsiConsole.WriteLine(LightSeparator);
        var color = StatusColor(status);
        AnsiConsole.MarkupLineInterpolated($" Result    : [{color}]{status.ToString().ToUpperInvariant()}[/]");
        AnsiConsole.MarkupLineInterpolated($" Exit code : {exitCode}");
        AnsiConsole.MarkupLineInterpolated($" Duration  : {duration.ToString(DurationFormat)}");
        AnsiConsole.WriteLine(HeavySeparator);
    }

    #endregion

    #region Body lines

    /// <summary>
    /// Writes a timestamped status line (e.g., STARTING / SUCCESS / FAILED) for a job, optionally with a duration.
    /// </summary>
    /// <param name="status">The status to display.</param>
    /// <param name="jobName">The job name.</param>
    /// <param name="duration">Optional duration to append (shown for terminal statuses).</param>
    public static void WriteStatus(JobStatus status, string jobName, TimeSpan? duration = null)
    {
        AnsiConsole.WriteLine();
        var label = status.ToString().ToUpperInvariant().PadRight(StatusLabelWidth);
        var color = StatusColor(status);
        var durationText = duration is null ? string.Empty : $"  ({duration.Value.ToString(DurationFormat)})";
        AnsiConsole.MarkupLineInterpolated($"{LinePrefix()}[{color}]{label}[/] {jobName}{durationText}");
    }

    /// <summary>Writes a timestamped informational line reported by a job.</summary>
    /// <param name="message">The message to display.</param>
    public static void WriteInfo(string message)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLineInterpolated($"{LinePrefix()}  INFO     {message}");
    }

    /// <summary>Writes a timestamped warning line reported by a job.</summary>
    /// <param name="message">The warning message to display.</param>
    public static void WriteWarning(string message)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLineInterpolated($"{LinePrefix()}  [yellow]WARN[/]     {message}");
    }

    /// <summary>Writes a timestamped line marking the start of a named job phase.</summary>
    /// <param name="phase">The phase name.</param>
    public static void WritePhaseStart(string phase)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLineInterpolated($"{LinePrefix()}  PHASE    {phase}...");
    }

    /// <summary>Writes a timestamped line marking the end of a named job phase with its duration.</summary>
    /// <param name="phase">The phase name.</param>
    /// <param name="duration">The measured phase duration.</param>
    public static void WritePhaseEnd(string phase, TimeSpan duration)
        => AnsiConsole.MarkupLineInterpolated($"{LinePrefix()}  PHASE    {phase} — done ({duration.ToString(DurationFormat)})");

    /// <summary>
    /// Writes the human-readable error detail for a failed run: the message and exception type, plus a
    /// pointer to the structured logs. The full stack trace is intentionally NOT written to the console
    /// (it is logged via <c>ILogger</c>); this keeps the console readable while preserving diagnosability.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="exceptionType">The exception's fully-qualified type name.</param>
    /// <param name="runId">The run id, so the console error can be tied back to the logged stack trace.</param>
    public static void WriteError(string message, string exceptionType, Guid runId)
    {
        AnsiConsole.MarkupLineInterpolated($"             [red]Error[/] : {message}");
        AnsiConsole.MarkupLineInterpolated($"             Type  : {exceptionType}");
        AnsiConsole.MarkupLineInterpolated($"             (full stack trace in logs — Run Id {runId})");
    }

    #endregion

    #region Usage

    /// <summary>
    /// Prints the available jobs. Uses a Spectre table for interactive runs; the output still reads
    /// fine when redirected by Task Scheduler.
    /// </summary>
    /// <param name="jobs">The registered jobs to list.</param>
    public static void PrintJobTable(IReadOnlyList<IScheduledJob> jobs)
    {
        if (jobs.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No jobs are registered.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Job");
        table.AddColumn("Description");
        foreach (var j in jobs.OrderBy(j => j.Name))
        {
            table.AddRow(Markup.Escape(j.Name), Markup.Escape(j.Description));
        }

        AnsiConsole.MarkupLine("[grey]Available jobs:[/]");
        AnsiConsole.Write(table);
    }

    #endregion

    #region Helpers

    /// <summary>Builds the per-line timestamp prefix (e.g., "[02:00:01] ") in UTC.</summary>
    private static string LinePrefix()
        => $"[{DateTime.UtcNow.ToString(LineTimestampFormat)}] ";

    /// <summary>Maps a status to its Spectre color markup.</summary>
    private static string StatusColor(JobStatus status) => status switch
    {
        JobStatus.Success => "green",
        JobStatus.Failed => "red",
        JobStatus.Cancelled => "yellow",
        _ => "blue"
    };

    #endregion
}