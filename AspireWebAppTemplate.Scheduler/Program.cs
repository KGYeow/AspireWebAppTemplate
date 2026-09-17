using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Extensions;
using AspireWebAppTemplate.Scheduler;
using AspireWebAppTemplate.Scheduler.Infrastructure;
using AspireWebAppTemplate.Scheduler.Jobs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

// -----------------------------------------------------------------------------------------------
// AspireWebAppTemplate.Scheduler
//
// A short-lived, Windows-Task-Scheduler-triggered batch runner. It builds the same DI graph as
// the API (Application + Infrastructure services + DbContext), selects ONE job by command-line
// name, runs it once, and exits with a process code that Task Scheduler records.
//
//   Windows Task Scheduler -> Scheduler.exe <job-name> -> run one job -> log -> exit code
//
// This is intentionally NOT a Worker Service: there is no continuous loop and no in-process
// scheduling — Task Scheduler owns timing, retries, and run history.
// -----------------------------------------------------------------------------------------------

// Resolve config/content relative to the executable directory, not the current working directory.
// Windows Task Scheduler may launch the exe with an arbitrary working directory (or none), so we
// pin ContentRootPath to AppContext.BaseDirectory to reliably locate appsettings.json.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// Aspire service defaults (telemetry, logging enrichment). Health-check endpoints are not mapped
// because this is a short-lived console process, not a long-running service.
builder.AddServiceDefaults();

// Explicitly load appsettings from the executable directory. Task Scheduler may launch the exe
// with an unrelated working directory, so we anchor config to AppContext.BaseDirectory rather than
// relying on the current directory.
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

// Database — same connection string the API uses. The Scheduler never runs migrations; it assumes
// the schema already exists (the API/deploy step owns migrations).
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, b => b.MigrationsAssembly("AspireWebAppTemplate.Infrastructure")));

// Identity core services (UserManager / RoleManager / stores). Several Infrastructure services
// (audit log, users, roles, authentication) depend on these managers, so the Scheduler registers
// them just like the API. No sign-in/cookie schemes are added — batch jobs never sign in a user.
// Data protection — required by Identity''s default token providers. Web hosts register this
// automatically; a console generic host must add it explicitly. Keys persist under the executable`s
// data directory (sufficient for token generation from batch jobs).
builder.Services.AddDataProtection();

builder.Services.AddIdentityCore<ApplicationUser>()
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Reuse the exact same Application/Infrastructure service graph as the API.
builder.Services.AddInfrastructureServices();

// Batch jobs run without an HTTP request, so replace the HTTP-backed current-user accessor with a
// fixed system principal (used by any service method that audit-logs).
builder.Services.AddScoped<ICurrentUserAccessor, SystemCurrentUserAccessor>();

// Register all scheduled jobs. Business apps add their jobs here (or delete the template job).
builder.Services.AddScoped<IScheduledJob, AuditLogRetentionJob>();

using var host = builder.Build();

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
    PrintJobTable(registeredJobs);
    logger.LogWarning("Scheduler invoked with no job name.");
    return ExitCodes.InvalidUsage;
}

var job = registeredJobs.FirstOrDefault(j => string.Equals(j.Name, jobName, StringComparison.OrdinalIgnoreCase));
if (job is null)
{
    AnsiConsole.MarkupLineInterpolated($"[red]Unknown job:[/] '{jobName}'.");
    PrintJobTable(registeredJobs);
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

// Prints the available jobs. Uses a Spectre table for interactive runs; the output still reads
// fine when redirected by Task Scheduler.
static void PrintJobTable(IReadOnlyList<IScheduledJob> jobs)
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