// Bugfix: scheduler-dependency-cleanup, Property 1: Scheduler registers exactly what its jobs consume
using Amazon.BedrockRuntime;
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.AuditLog;
using AspireWebAppTemplate.Infrastructure.Clients;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Extensions;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Scheduler.Jobs;
using AspireWebAppTemplate.Scheduler.Jobs.Implementations;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Ganss.Xss;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AspireWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Property 1 (Fix Checking) composition tests verifying the POST-FIX Scheduler service graph.
/// </summary>
/// <remarks>
/// Bugfix: scheduler-dependency-cleanup, Property 1: Scheduler registers exactly what its jobs consume.
///
/// These tests assert the fixed behavior against the graph produced by
/// <see cref="SchedulerInfrastructureServiceExtensions.AddSchedulerInfrastructure"/> plus the Scheduler's
/// job registration (<c>AddScoped&lt;IScheduledJob, AuditLogRetentionJob&gt;</c>):
/// <list type="bullet">
///   <item><see cref="IAuditLogRetentionService"/> resolves and <c>PurgeOldEntriesAsync()</c> runs to success.</item>
///   <item>The graph does NOT register <see cref="IAuditLogService"/>, ASP.NET Core Identity
///     (<see cref="UserManager{ApplicationUser}"/>), Data Protection, an <see cref="AmazonBedrockRuntimeClient"/>,
///     a <see cref="WebCallbackClient"/>, an <see cref="HtmlSanitizer"/>, or an <see cref="ICurrentUserAccessor"/>.</item>
/// </list>
///
/// The companion exploration tests in <c>SchedulerCompositionPropertyTests</c> encode the same intent
/// against the PRE-FIX graph (and are left as-is); this file exercises the focused seam that the fix
/// introduced.
/// </remarks>
public class SchedulerFixCompositionTests
{
    #region Composition builders

    /// <summary>
    /// Builds a configuration mirroring what the Scheduler host reads: a SQL Server connection string
    /// (consumed by <see cref="SchedulerInfrastructureServiceExtensions.AddSchedulerInfrastructure"/>) plus
    /// the audit-log retention setting.
    /// </summary>
    /// <returns>An in-memory <see cref="IConfiguration"/> for composing the Scheduler graph.</returns>
    private static IConfiguration BuildConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(local);Database=Test;Trusted_Connection=True;",
                ["AuditLog:RetentionDays"] = "365"
            })
            .Build();

    /// <summary>
    /// Composes the post-fix Scheduler service collection exactly as
    /// <c>SchedulerHostBuilder</c> does: <see cref="SchedulerInfrastructureServiceExtensions.AddSchedulerInfrastructure"/>
    /// plus the job registration. This is the graph whose exclusions Property 1 asserts.
    /// </summary>
    /// <param name="configuration">The configuration supplying the connection string and retention days.</param>
    /// <returns>The composed Scheduler <see cref="IServiceCollection"/>.</returns>
    private static IServiceCollection BuildSchedulerServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();

        services.AddSchedulerInfrastructure(configuration);
        services.AddScoped<IScheduledJob, AuditLogRetentionJob>();

        return services;
    }

    /// <summary>
    /// Replaces the <see cref="ApplicationDbContext"/> registration produced by
    /// <see cref="SchedulerInfrastructureServiceExtensions.AddSchedulerInfrastructure"/> (SQL Server) with a
    /// SQLite in-memory context so the purge can actually run in tests. This is a documented test seam only —
    /// it does not change which OTHER service types the Scheduler graph registers, so the Property-1
    /// exclusion assertions still hold against the seam-produced graph.
    /// </summary>
    /// <param name="services">The composed Scheduler service collection.</param>
    /// <param name="connection">The open SQLite connection kept alive for the provider's lifetime.</param>
    private static void SwapDbContextToSqlite(IServiceCollection services, SqliteConnection connection)
    {
        // Disable foreign-key enforcement so audit entries can be seeded without a matching
        // ApplicationUser record (mirrors PurgeCorrectnessPropertyTests).
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = OFF;";
            command.ExecuteNonQuery();
        }

        // Remove every EF Core registration added by AddSchedulerInfrastructure's AddDbContext call
        // (the SQL Server provider registers the context, its options, and internal options-configuration
        // descriptors). EF Core forbids two providers in one provider, so all of these must go before the
        // SQLite provider is added.
        var toRemove = services
            .Where(d =>
                d.ServiceType == typeof(ApplicationDbContext) ||
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                (d.ServiceType.FullName?.Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ?? false))
            .ToList();
        foreach (var descriptor in toRemove)
        {
            services.Remove(descriptor);
        }

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
    }

    /// <summary>
    /// Determines whether a service of the given type is registered in the collection.
    /// </summary>
    /// <typeparam name="T">The service type to check for.</typeparam>
    /// <param name="services">The service collection to inspect.</param>
    /// <returns><c>true</c> if a registration for <typeparamref name="T"/> exists; otherwise <c>false</c>.</returns>
    private static bool IsRegistered<T>(IServiceCollection services)
        => services.Any(d => d.ServiceType == typeof(T));

    /// <summary>
    /// Determines whether any registered service or implementation type name contains the given fragment
    /// (used to detect Data Protection, whose concrete types are internal to the framework).
    /// </summary>
    /// <param name="services">The service collection to inspect.</param>
    /// <param name="fragment">The case-insensitive type-name fragment to search for.</param>
    /// <returns><c>true</c> if any registration's type name contains the fragment; otherwise <c>false</c>.</returns>
    private static bool IsRegisteredByNameFragment(IServiceCollection services, string fragment)
        => services.Any(d =>
            (d.ServiceType.FullName?.Contains(fragment, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (d.ImplementationType?.FullName?.Contains(fragment, StringComparison.OrdinalIgnoreCase) ?? false));

    #endregion

    #region Fix Checking — resolution + purge

    /// <summary>
    /// The retention service resolves from the focused Scheduler graph and
    /// <c>PurgeOldEntriesAsync()</c> runs to success against a SQLite in-memory
    /// <see cref="ApplicationDbContext"/>, deleting entries older than the retention cutoff.
    /// </summary>
    [Fact]
    public async Task Scheduler_ResolvesRetentionService_AndPurgeRunsToSuccess()
    {
        var configuration = BuildConfiguration();
        var services = BuildSchedulerServices(configuration);

        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        try
        {
            SwapDbContextToSqlite(services, connection);

            await using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            // Ensure the schema exists and seed one expired + one recent entry.
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.EnsureCreated();

            var now = DateTime.UtcNow;
            SeedEntry(dbContext, now - TimeSpan.FromDays(400)); // expired (older than 365)
            var recentId = SeedEntry(dbContext, now - TimeSpan.FromDays(10)); // within retention

            // The purge job's sole feature dependency must resolve from the Scheduler graph.
            var retentionService = scope.ServiceProvider.GetService<IAuditLogRetentionService>();
            Assert.NotNull(retentionService);

            var purged = await retentionService!.PurgeOldEntriesAsync();

            dbContext.ChangeTracker.Clear();
            var remainingIds = dbContext.AuditLogEntries.Select(e => e.Id).ToHashSet();

            Assert.Equal(1, purged);
            Assert.Contains(recentId, remainingIds);
        }
        finally
        {
            connection.Dispose();
        }
    }

    #endregion

    #region Fix Checking — exclusions

    /// <summary>
    /// The focused Scheduler graph does NOT register any of the excluded types: the Identity-coupled
    /// <see cref="IAuditLogService"/>, ASP.NET Core Identity (<see cref="UserManager{ApplicationUser}"/>),
    /// Data Protection, an <see cref="AmazonBedrockRuntimeClient"/>, a <see cref="WebCallbackClient"/>,
    /// an <see cref="HtmlSanitizer"/>, or an <see cref="ICurrentUserAccessor"/>.
    /// </summary>
    [Fact]
    public void SchedulerGraph_DoesNotRegisterExcludedTypes()
    {
        var configuration = BuildConfiguration();
        var services = BuildSchedulerServices(configuration);

        Assert.False(IsRegistered<IAuditLogService>(services),
            "The Scheduler graph must not register the Identity-coupled IAuditLogService.");
        Assert.False(IsRegistered<UserManager<ApplicationUser>>(services),
            "The Scheduler graph must not register ASP.NET Core Identity (UserManager<ApplicationUser>).");
        Assert.False(IsRegisteredByNameFragment(services, "DataProtection"),
            "The Scheduler graph must not register Data Protection.");
        Assert.False(IsRegistered<AmazonBedrockRuntimeClient>(services),
            "The Scheduler graph must not register an AmazonBedrockRuntimeClient (AWS Bedrock).");
        Assert.False(IsRegistered<WebCallbackClient>(services),
            "The Scheduler graph must not register a WebCallbackClient (Web host coupling).");
        Assert.False(IsRegistered<HtmlSanitizer>(services),
            "The Scheduler graph must not register an HtmlSanitizer.");
        Assert.False(IsRegistered<ICurrentUserAccessor>(services),
            "The Scheduler graph must not register an ICurrentUserAccessor.");
    }

    /// <summary>
    /// Property: for any generated Scheduler composition, the graph never contains the excluded types
    /// while <see cref="IAuditLogRetentionService"/> always resolves. The generator varies the retention
    /// configuration to exercise multiple compositions of the same focused seam.
    /// </summary>
    /// <returns>An FsCheck <see cref="Property"/> asserting exclusion + resolvability across compositions.</returns>
    [Property(MaxTest = 2)]
    public Property SchedulerCompositions_NeverContainExcludedTypes_AndAlwaysResolveRetentionService()
    {
        // Vary the retention configuration across valid values; the seam's registered TYPE set is
        // invariant to this, which is exactly the property under test.
        var retentionDaysGen = Gen.Choose(1, 3650);

        return Prop.ForAll(Arb.From(retentionDaysGen), retentionDays =>
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=(local);Database=Test;Trusted_Connection=True;",
                    ["AuditLog:RetentionDays"] = retentionDays.ToString()
                })
                .Build();

            var services = BuildSchedulerServices(configuration);

            var excludesAll =
                !IsRegistered<IAuditLogService>(services) &&
                !IsRegistered<UserManager<ApplicationUser>>(services) &&
                !IsRegisteredByNameFragment(services, "DataProtection") &&
                !IsRegistered<AmazonBedrockRuntimeClient>(services) &&
                !IsRegistered<WebCallbackClient>(services) &&
                !IsRegistered<HtmlSanitizer>(services) &&
                !IsRegistered<ICurrentUserAccessor>(services);

            // Swap to SQLite so the provider is buildable and the retention service is resolvable.
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            try
            {
                SwapDbContextToSqlite(services, connection);
                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                var resolves = scope.ServiceProvider.GetService<IAuditLogRetentionService>() is not null;

                return (excludesAll && resolves).Label(
                    $"RetentionDays={retentionDays}, excludesAll={excludesAll}, resolvesRetentionService={resolves}");
            }
            finally
            {
                connection.Dispose();
            }
        });
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Seeds a single audit-log entry with the specified timestamp and returns its id.
    /// Foreign-key enforcement is off for the SQLite test context, so no matching user is required.
    /// </summary>
    /// <param name="dbContext">The database context to seed into.</param>
    /// <param name="timestamp">The timestamp to assign the entry.</param>
    /// <returns>The generated id of the seeded entry.</returns>
    private static Guid SeedEntry(ApplicationDbContext dbContext, DateTime timestamp)
    {
        var entry = new Infrastructure.Data.Entities.AuditLogEntry
        {
            Id = Guid.NewGuid(),
            UserId = string.Empty,
            UserDisplayName = string.Empty,
            ActionType = Domain.Enums.AuditActionType.LoginSuccess,
            EntityType = Domain.Enums.AuditEntityType.System,
            EntityId = "test",
            EntityName = "test",
            Description = "test entry",
            Timestamp = timestamp
        };
        dbContext.AuditLogEntries.Add(entry);
        dbContext.SaveChanges();
        return entry.Id;
    }

    #endregion
}
