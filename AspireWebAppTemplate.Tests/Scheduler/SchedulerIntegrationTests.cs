// Bugfix: scheduler-dependency-cleanup, Integration: purge / usage / cancellation end-to-end through JobRunner
using System.Reflection;
using AspireWebAppTemplate.Application.Features.AuditLog;
using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Services.AuditLog;
using AspireWebAppTemplate.Scheduler.Constants;
using AspireWebAppTemplate.Scheduler.Hosting;
using AspireWebAppTemplate.Scheduler.Jobs;
using AspireWebAppTemplate.Scheduler.Jobs.Implementations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AspireWebAppTemplate.Tests.Scheduler;

/// <summary>
/// Integration tests exercising the Scheduler end-to-end through
/// <see cref="JobRunner.RunAsync(IHost, string[])"/> against a composed generic host backed by a
/// SQLite in-memory <see cref="ApplicationDbContext"/>.
/// </summary>
/// <remarks>
/// Bugfix: scheduler-dependency-cleanup. These tests cover the three runtime paths Windows Task
/// Scheduler keys on (regressions 3.3, 3.4, 3.5):
/// <list type="bullet">
///   <item>a full <c>purge-audit-logs</c> run deletes expired entries, retains recent ones, and returns
///     <see cref="ExitCodes.Success"/>;</item>
///   <item>no job name / an unknown job name prints the available-jobs listing and returns
///     <see cref="ExitCodes.InvalidUsage"/>;</item>
///   <item>a cancelled run returns <see cref="ExitCodes.Cancelled"/>.</item>
/// </list>
/// The host is composed with the real <see cref="AuditLogRetentionJob"/> and its sole feature dependency
/// (<see cref="IAuditLogRetentionService"/> â†’ <see cref="AuditLogRetentionService"/>) over a SQLite
/// in-memory context, so the purge path is driven exactly as production wiring does â€” without importing
/// the full API/Web graph.
/// </remarks>
public class SchedulerIntegrationTests
{
    #region Test doubles

    /// <summary>
    /// A test <see cref="IScheduledJob"/> that first triggers the process-wide
    /// <see cref="Console.CancelKeyPress"/> event (the exact mechanism <see cref="JobRunner"/> wires its
    /// internal <see cref="CancellationTokenSource"/> to) and then observes the supplied token, throwing
    /// <see cref="OperationCanceledException"/> once cancellation has been requested.
    /// </summary>
    /// <remarks>
    /// This drives the real cancellation path through the public <see cref="JobRunner.RunAsync"/> surface:
    /// no production code is modified. The job raises <c>Console.CancelKeyPress</c> synchronously, which
    /// runs <see cref="JobRunner"/>'s handler (setting <c>e.Cancel = true</c> and cancelling the internal
    /// source), so by the time the token is observed <c>cts.IsCancellationRequested</c> is <c>true</c> â€”
    /// satisfying the <c>when (cts.IsCancellationRequested)</c> guard that maps to
    /// <see cref="ExitCodes.Cancelled"/>.
    /// </remarks>
    private sealed class CancellingJob : IScheduledJob
    {
        /// <inheritdoc />
        public string Name => "cancelling-job";

        /// <inheritdoc />
        public string Description => "Raises Console.CancelKeyPress then observes the cancellation token.";

        /// <inheritdoc />
        public Task<int> RunAsync(CancellationToken cancellationToken)
        {
            // Trigger the same event JobRunner subscribes to; its handler cancels the internal source.
            RaiseConsoleCancelKeyPress();

            // The handler ran synchronously above, so the token is now cancelled â€” this throws
            // OperationCanceledException, which JobRunner maps to Cancelled because IsCancellationRequested.
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(ExitCodes.Success);
        }
    }

    #endregion

    #region Purge (regression 3.3)

    /// <summary>
    /// A full <c>purge-audit-logs</c> run through <see cref="JobRunner.RunAsync"/> deletes entries older
    /// than the retention cutoff, retains recent entries, and returns <see cref="ExitCodes.Success"/>.
    /// </summary>
    [Fact]
    public async Task RunAsync_PurgeAuditLogs_DeletesExpired_RetainsRecent_ReturnsSuccess()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        DisableForeignKeys(connection);
        try
        {
            using var host = BuildHost(connection, retentionDays: 365);

            Guid expiredId;
            Guid recentId;
            using (var seedScope = host.Services.CreateScope())
            {
                var dbContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                dbContext.Database.EnsureCreated();

                var now = DateTime.UtcNow;
                expiredId = SeedEntry(dbContext, now - TimeSpan.FromDays(400)); // older than 365 â†’ purged
                recentId = SeedEntry(dbContext, now - TimeSpan.FromDays(10));   // within retention â†’ kept
            }

            var exitCode = await JobRunner.RunAsync(host, ["purge-audit-logs"]);

            Assert.Equal(ExitCodes.Success, exitCode);

            using var verifyScope = host.Services.CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var remainingIds = verifyContext.AuditLogEntries.Select(e => e.Id).ToHashSet();

            Assert.DoesNotContain(expiredId, remainingIds);
            Assert.Contains(recentId, remainingIds);
        }
        finally
        {
            connection.Dispose();
        }
    }

    #endregion

    #region Invalid usage (regression 3.4)

    /// <summary>
    /// Invoking the Scheduler with no job name prints the available-jobs listing and returns
    /// <see cref="ExitCodes.InvalidUsage"/>.
    /// </summary>
    [Fact]
    public async Task RunAsync_WithNoJobName_ReturnsInvalidUsage()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        DisableForeignKeys(connection);
        try
        {
            using var host = BuildHost(connection, retentionDays: 365);

            var exitCode = await JobRunner.RunAsync(host, []);

            Assert.Equal(ExitCodes.InvalidUsage, exitCode);
        }
        finally
        {
            connection.Dispose();
        }
    }

    /// <summary>
    /// Invoking the Scheduler with an unknown job name prints the available-jobs listing and returns
    /// <see cref="ExitCodes.InvalidUsage"/>.
    /// </summary>
    [Fact]
    public async Task RunAsync_WithUnknownJobName_ReturnsInvalidUsage()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        DisableForeignKeys(connection);
        try
        {
            using var host = BuildHost(connection, retentionDays: 365);

            var exitCode = await JobRunner.RunAsync(host, ["no-such-job"]);

            Assert.Equal(ExitCodes.InvalidUsage, exitCode);
        }
        finally
        {
            connection.Dispose();
        }
    }

    #endregion

    #region Cancellation (regression 3.5)

    /// <summary>
    /// When the running job observes the cancellation token after <see cref="Console.CancelKeyPress"/>
    /// fires, <see cref="JobRunner.RunAsync"/> returns <see cref="ExitCodes.Cancelled"/>.
    /// </summary>
    /// <remarks>
    /// This exercises the same cancellation wiring the production process uses (Ctrl+C / SIGTERM â†’
    /// <c>Console.CancelKeyPress</c> â†’ internal <c>cts.Cancel()</c>) through the public
    /// <see cref="JobRunner.RunAsync"/> surface, with no production seam added.
    /// </remarks>
    [Fact]
    public async Task RunAsync_WhenCancelKeyPressed_ReturnsCancelled()
    {
        using var host = BuildJobsOnlyHost(new CancellingJob());

        var exitCode = await JobRunner.RunAsync(host, ["cancelling-job"]);

        Assert.Equal(ExitCodes.Cancelled, exitCode);
    }

    #endregion

    #region Host builders

    /// <summary>
    /// Builds a generic host that registers the real <see cref="AuditLogRetentionJob"/> and its feature
    /// dependency (<see cref="IAuditLogRetentionService"/> â†’ <see cref="AuditLogRetentionService"/>) over a
    /// SQLite in-memory <see cref="ApplicationDbContext"/>. Mirrors the Scheduler's focused composition
    /// (job + retention service + DbContext) without importing the full API/Web graph.
    /// </summary>
    /// <param name="connection">The open SQLite connection kept alive for the provider's lifetime.</param>
    /// <param name="retentionDays">The <c>AuditLog:RetentionDays</c> value to configure.</param>
    /// <returns>A built host ready to pass to <see cref="JobRunner.RunAsync"/>.</returns>
    private static IHost BuildHost(SqliteConnection connection, int retentionDays)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AuditLog:RetentionDays"] = retentionDays.ToString()
        });

        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        builder.Services.AddScoped<IAuditLogRetentionService, AuditLogRetentionService>();
        builder.Services.AddScoped<IScheduledJob, AuditLogRetentionJob>();

        return builder.Build();
    }

    /// <summary>
    /// Builds a minimal host that registers only the supplied jobs plus logging â€” used for paths that do
    /// not touch the database (e.g., the cancellation path).
    /// </summary>
    /// <param name="jobs">The jobs to register.</param>
    /// <returns>A built host ready to pass to <see cref="JobRunner.RunAsync"/>.</returns>
    private static IHost BuildJobsOnlyHost(params IScheduledJob[] jobs)
    {
        var builder = Host.CreateApplicationBuilder();

        foreach (var job in jobs)
        {
            builder.Services.AddScoped<IScheduledJob>(_ => job);
        }

        return builder.Build();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Disables SQLite foreign-key enforcement so audit entries can be seeded without a matching
    /// <c>ApplicationUser</c> record (mirrors the purge property/composition tests).
    /// </summary>
    /// <param name="connection">The open SQLite connection to configure.</param>
    private static void DisableForeignKeys(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = OFF;";
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Seeds a single audit-log entry with the specified timestamp and returns its id.
    /// </summary>
    /// <param name="dbContext">The database context to seed into.</param>
    /// <param name="timestamp">The timestamp to assign the entry.</param>
    /// <returns>The generated id of the seeded entry.</returns>
    private static Guid SeedEntry(ApplicationDbContext dbContext, DateTime timestamp)
    {
        var entry = new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            UserId = string.Empty,
            UserDisplayName = string.Empty,
            ActionType = AuditActionType.LoginSuccess,
            EntityType = AuditEntityType.System,
            EntityId = "test",
            EntityName = "test",
            Description = "test entry",
            Timestamp = timestamp
        };
        dbContext.AuditLogEntries.Add(entry);
        dbContext.SaveChanges();
        return entry.Id;
    }

    /// <summary>
    /// Raises the process-wide <see cref="Console.CancelKeyPress"/> event via reflection, simulating a
    /// Ctrl+C press. <see cref="ConsoleCancelEventArgs"/> has no public constructor and the event has no
    /// public raise method, so both are accessed through non-public members. This touches only framework
    /// types â€” no production Scheduler code is modified or given a test seam.
    /// </summary>
    private static void RaiseConsoleCancelKeyPress()
    {
        // Construct ConsoleCancelEventArgs(ConsoleSpecialKey) via its non-public constructor.
        var argsType = typeof(ConsoleCancelEventArgs);
        var ctor = argsType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(ConsoleSpecialKey)],
            modifiers: null)
            ?? throw new InvalidOperationException("Could not locate the ConsoleCancelEventArgs constructor.");

        var eventArgs = (ConsoleCancelEventArgs)ctor.Invoke([ConsoleSpecialKey.ControlC]);

        // Fetch the backing multicast delegate for the static Console.CancelKeyPress event and invoke it.
        var field = typeof(Console).GetField("s_cancelCallbacks", BindingFlags.Static | BindingFlags.NonPublic)
            ?? typeof(Console).GetField("_cancelCallbacks", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not locate the Console.CancelKeyPress backing field.");

        if (field.GetValue(null) is not ConsoleCancelEventHandler handler)
        {
            throw new InvalidOperationException("No Console.CancelKeyPress handler is registered to raise.");
        }

        // Console.CancelKeyPress is a process-global event and JobRunner subscribes a fresh handler on each
        // RunAsync call without unsubscribing, so prior test runs leave stale handlers whose captured
        // CancellationTokenSource has since been disposed. Invoke each handler in the multicast list
        // individually and swallow ObjectDisposedException from those stale handlers â€” only the live
        // handler (whose source is the current run's cts) needs to observe the event.
        foreach (var singleHandler in handler.GetInvocationList().Cast<ConsoleCancelEventHandler>())
        {
            try
            {
                singleHandler(null, eventArgs);
            }
            catch (ObjectDisposedException)
            {
                // Stale handler from a previous run whose CancellationTokenSource is disposed; ignore.
            }
        }
    }

    #endregion
}
