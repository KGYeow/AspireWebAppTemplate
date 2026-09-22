// Bugfix: scheduler-dependency-cleanup, Property 1: Scheduler registers exactly what its jobs consume
using System.Reflection;
using Amazon.BedrockRuntime;
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.AuditLog;
using AspireWebAppTemplate.Infrastructure.Clients;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Extensions;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services.AuditLog;
using AspireWebAppTemplate.Scheduler.Jobs;
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
/// Property 1 (Expected Behavior) tests for the Scheduler composition after the over-inheritance fix.
/// </summary>
/// <remarks>
/// Bugfix: scheduler-dependency-cleanup, Property 1: Scheduler registers exactly what its jobs consume.
///
/// These tests validate the fixed behavior: the Scheduler's registered service set equals exactly what
/// its job(s) consume — <see cref="ApplicationDbContext"/>, <see cref="IConfiguration"/>, and the focused
/// <see cref="IAuditLogRetentionService"/> — and does NOT pull in ASP.NET Core Identity / Data Protection,
/// an <see cref="AmazonBedrockRuntimeClient"/> (AWS Bedrock), a <see cref="WebCallbackClient"/> (Web host),
/// an <see cref="HtmlSanitizer"/>, an <see cref="ICurrentUserAccessor"/>, or the Identity-coupled
/// <see cref="IAuditLogService"/>.
///
/// The graph is composed exactly as the Scheduler host composes it: via
/// <see cref="SchedulerInfrastructureServiceExtensions.AddSchedulerInfrastructure"/> plus the job
/// registration (<c>AddScoped&lt;IScheduledJob, AuditLogRetentionJob&gt;</c>). The purge/retention path
/// is constructable with only <see cref="ApplicationDbContext"/> + <see cref="IConfiguration"/>
/// (plus a logger), so the Scheduler never needs a <see cref="UserManager{ApplicationUser}"/>.
/// </remarks>
public class SchedulerCompositionPropertyTests
{
    #region Scheduler graph builders

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
    /// Composes the Scheduler service collection exactly as <c>SchedulerHostBuilder</c> does:
    /// <see cref="SchedulerInfrastructureServiceExtensions.AddSchedulerInfrastructure"/> plus the job
    /// registration. This is the focused graph whose composition Property 1 asserts.
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
    /// Determines whether any registered service or implementation type name contains the given
    /// fragment (used to detect Data Protection, whose concrete types are internal to the framework).
    /// </summary>
    /// <param name="services">The service collection to inspect.</param>
    /// <param name="fragment">The case-insensitive type-name fragment to search for.</param>
    /// <returns><c>true</c> if any registration's type name contains the fragment; otherwise <c>false</c>.</returns>
    private static bool IsRegisteredByNameFragment(IServiceCollection services, string fragment)
        => services.Any(d =>
            (d.ServiceType.FullName?.Contains(fragment, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (d.ImplementationType?.FullName?.Contains(fragment, StringComparison.OrdinalIgnoreCase) ?? false));

    #endregion

    #region Expected Behavior 1 — purge path is constructable without Identity

    /// <summary>
    /// The purge/retention path the Scheduler depends on is <see cref="AuditLogRetentionService"/>, whose
    /// public constructor requires only <see cref="ApplicationDbContext"/>, <see cref="IConfiguration"/>,
    /// and a logger — never a <see cref="UserManager{ApplicationUser}"/>. This confirms the Identity
    /// coupling that previously forced the Scheduler to register the full Identity stack has been removed
    /// from the purge path.
    ///
    /// The Identity-coupled <see cref="AuditLogService"/> intentionally KEEPS its
    /// <see cref="UserManager{ApplicationUser}"/> dependency (it still resolves display names on the
    /// audit-write path); the fix decouples the Scheduler by giving purge its own focused service rather
    /// than by removing UserManager from AuditLogService.
    /// </summary>
    [Fact]
    public void AuditLogRetentionService_ShouldBeConstructableWithoutUserManager()
    {
        var ctor = typeof(AuditLogRetentionService).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
        var parameterTypes = ctor.GetParameters().Select(p => p.ParameterType).ToArray();

        var requiresUserManager = parameterTypes.Contains(typeof(UserManager<ApplicationUser>));

        Assert.False(
            requiresUserManager,
            "AuditLogRetentionService (the service the Scheduler depends on for purging) must NOT require " +
            "UserManager<ApplicationUser> — the purge path carries no Identity coupling. " +
            "Constructor parameters = [" +
            string.Join(", ", parameterTypes.Select(t => t.Name)) + "].");
    }

    #endregion

    #region Expected Behavior 2-4 — focused graph excludes over-inherited types

    /// <summary>
    /// Expected Behavior 2: the focused Scheduler graph does NOT register an
    /// <see cref="AmazonBedrockRuntimeClient"/> (AWS Bedrock) — no job consumes it.
    /// </summary>
    [Fact]
    public void SchedulerGraph_ShouldNotRegisterBedrockClient()
    {
        var services = BuildSchedulerServices(BuildConfiguration());

        Assert.False(
            IsRegistered<AmazonBedrockRuntimeClient>(services),
            "The Scheduler graph must not register AmazonBedrockRuntimeClient (AWS Bedrock) — " +
            "purge-audit-logs never invokes AI.");
    }

    /// <summary>
    /// Expected Behavior 3: the focused Scheduler graph does NOT register a <see cref="WebCallbackClient"/>
    /// typed HttpClient targeting the Web host — no job consumes it.
    /// </summary>
    [Fact]
    public void SchedulerGraph_ShouldNotRegisterWebCallbackClient()
    {
        var services = BuildSchedulerServices(BuildConfiguration());

        Assert.False(
            IsRegistered<WebCallbackClient>(services),
            "The Scheduler graph must not register WebCallbackClient (targeting https+http://webfrontend) — " +
            "an optional batch host must not couple to the Web project's callback target.");
    }

    /// <summary>
    /// Expected Behavior 4: the focused Scheduler graph does NOT register the <see cref="HtmlSanitizer"/>,
    /// ASP.NET Core Identity (<see cref="UserManager{ApplicationUser}"/>), or Data Protection — none of
    /// which any job consumes.
    /// </summary>
    [Fact]
    public void SchedulerGraph_ShouldNotRegisterSanitizerIdentityOrDataProtection()
    {
        var services = BuildSchedulerServices(BuildConfiguration());

        Assert.False(
            IsRegistered<HtmlSanitizer>(services),
            "The Scheduler graph must not register HtmlSanitizer (Ganss.Xss) — no job uses it.");

        Assert.False(
            IsRegistered<UserManager<ApplicationUser>>(services),
            "The Scheduler graph must not register ASP.NET Core Identity (UserManager<ApplicationUser>) — " +
            "the purge path is no longer coupled to Identity.");

        Assert.False(
            IsRegisteredByNameFragment(services, "DataProtection"),
            "The Scheduler graph must not register Data Protection — no job requires it.");
    }

    /// <summary>
    /// The focused Scheduler graph depends on <see cref="IAuditLogRetentionService"/> and never registers
    /// the Identity-coupled <see cref="IAuditLogService"/> nor the HTTP-backed
    /// <see cref="ICurrentUserAccessor"/> (which previously had to be shadowed defensively). The generator
    /// varies the retention configuration to exercise multiple compositions of the same focused seam;
    /// the registered TYPE set is invariant to that value, which is exactly the property under test.
    /// </summary>
    /// <returns>An FsCheck <see cref="Property"/> asserting the focused graph across compositions.</returns>
    [Property(MaxTest = 2)]
    public Property SchedulerGraph_RegistersOnlyWhatJobsConsume()
    {
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

            var registersAuditLogService = IsRegistered<IAuditLogService>(services);
            var registersCurrentUserAccessor = IsRegistered<ICurrentUserAccessor>(services);
            var registersRetentionService = IsRegistered<IAuditLogRetentionService>(services);

            // Post-fix expectation: the focused retention service is registered, while neither the
            // Identity-coupled IAuditLogService nor the HTTP-backed ICurrentUserAccessor belongs in the
            // Scheduler graph.
            return (registersRetentionService && !registersAuditLogService && !registersCurrentUserAccessor).Label(
                $"RetentionDays={retentionDays}, registersRetentionService={registersRetentionService}, " +
                $"registersAuditLogService={registersAuditLogService}, " +
                $"registersCurrentUserAccessor={registersCurrentUserAccessor} " +
                "(expected: retention=true, auditLogService=false, currentUserAccessor=false).");
        });
    }

    #endregion
}
