using AspireWebAppTemplate.Infrastructure.Extensions;
using AspireWebAppTemplate.Scheduler.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AspireWebAppTemplate.Scheduler.Hosting;

/// <summary>
/// Builds and configures the Scheduler's generic host: Aspire service defaults, executable-anchored
/// configuration, the focused infrastructure seam, and the registered batch jobs.
/// </summary>
/// <remarks>
/// The Scheduler is a short-lived console process, not a Worker Service, so this builder maps only
/// what its jobs consume via <see cref="SchedulerInfrastructureServiceExtensions.AddSchedulerInfrastructure"/>
/// â€” it does NOT import the full API/Web feature graph, Identity, or Data Protection. Job
/// registrations live here (not in Infrastructure) so business apps add or remove jobs in one place.
/// </remarks>
public static class SchedulerHostBuilder
{
    /// <summary>
    /// Builds the configured <see cref="IHost"/> for the Scheduler process.
    /// </summary>
    /// <param name="args">The command-line arguments passed through to the host builder.</param>
    /// <returns>A fully configured, not-yet-started <see cref="IHost"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the <c>DefaultConnection</c> connection string is not configured.
    /// </exception>
    public static IHost Build(string[] args)
    {
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

        // Focused infrastructure seam: registers ONLY what the Scheduler's jobs consume
        // (ApplicationDbContext + IAuditLogRetentionService). No Identity, Data Protection, AI/Bedrock,
        // Web callback client, sanitizer, or full feature graph.
        builder.Services.AddSchedulerInfrastructure(builder.Configuration);

        // Register all scheduled jobs. Business apps add their jobs here (or delete the template job).
        builder.Services.AddScoped<IScheduledJob, AuditLogRetentionJob>();

        return builder.Build();
    }
}
