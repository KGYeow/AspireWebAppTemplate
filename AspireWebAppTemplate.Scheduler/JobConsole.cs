using AspireWebAppTemplate.Scheduler.Jobs;
using Spectre.Console;

namespace AspireWebAppTemplate.Scheduler;

/// <summary>
/// Console-output helpers for the Scheduler's interactive/diagnostic messages.
/// </summary>
public static class JobConsole
{
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
}
