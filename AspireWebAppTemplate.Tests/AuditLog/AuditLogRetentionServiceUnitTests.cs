// Bugfix: scheduler-dependency-cleanup, Unit tests: AuditLogRetentionService
using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Services.AuditLog;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AspireWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Example-based unit tests for <see cref="AuditLogRetentionService.PurgeOldEntriesAsync"/>.
/// </summary>
/// <remarks>
/// Bugfix: scheduler-dependency-cleanup. These complement the property-based
/// <c>PurgeCorrectnessPropertyTests</c>/<c>RetentionConfigPropertyTests</c> with concrete cases:
/// <list type="bullet">
///   <item>cutoff behavior — only entries older than the cutoff are deleted;</item>
///   <item>exception propagation — unlike <c>LogAsync</c> (which swallows), purge lets DB-layer
///     exceptions escape so a background caller can retry;</item>
///   <item>focused constructability — the service is built from only
///     <see cref="ApplicationDbContext"/> + <see cref="IConfiguration"/> (+ logger), with no Identity,
///     <c>UserManager</c>, or display-name resolver dependency.</item>
/// </list>
/// Uses a SQLite in-memory <see cref="ApplicationDbContext"/>.
/// </remarks>
public class AuditLogRetentionServiceUnitTests
{
    #region Test setup helpers

    /// <summary>
    /// Creates a SQLite in-memory <see cref="ApplicationDbContext"/>. The connection must stay open for the
    /// lifetime of the test. Foreign-key enforcement is disabled so audit entries can be seeded without a
    /// matching <c>ApplicationUser</c>.
    /// </summary>
    /// <returns>The created context and the open connection backing it.</returns>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = OFF;";
        command.ExecuteNonQuery();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();
        return (dbContext, connection);
    }

    /// <summary>
    /// Builds an <see cref="IConfiguration"/> carrying the given <c>AuditLog:RetentionDays</c> value.
    /// </summary>
    /// <param name="retentionDays">The retention-days value to expose, or null to omit the key entirely.</param>
    /// <returns>An in-memory configuration for the retention service.</returns>
    private static IConfiguration BuildConfiguration(int? retentionDays)
    {
        var values = new Dictionary<string, string?>();
        if (retentionDays is not null)
        {
            values["AuditLog:RetentionDays"] = retentionDays.Value.ToString();
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
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

    #endregion

    #region Cutoff behavior

    /// <summary>
    /// Purge deletes only entries older than the retention cutoff, preserving entries within the window and
    /// returning the count of deleted rows.
    /// </summary>
    [Fact]
    public async Task PurgeOldEntriesAsync_DeletesOnlyEntriesOlderThanCutoff()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            const int retentionDays = 30;
            var now = DateTime.UtcNow;

            // Two expired (older than 30 days) and two within retention.
            SeedEntry(dbContext, now - TimeSpan.FromDays(60));
            SeedEntry(dbContext, now - TimeSpan.FromDays(31));
            var recentA = SeedEntry(dbContext, now - TimeSpan.FromDays(29));
            var recentB = SeedEntry(dbContext, now - TimeSpan.FromDays(1));

            var service = new AuditLogRetentionService(
                dbContext,
                BuildConfiguration(retentionDays),
                NullLogger<AuditLogRetentionService>.Instance);

            var purged = await service.PurgeOldEntriesAsync();

            dbContext.ChangeTracker.Clear();
            var remaining = dbContext.AuditLogEntries.Select(e => e.Id).ToHashSet();

            Assert.Equal(2, purged);
            Assert.Equal(2, remaining.Count);
            Assert.Contains(recentA, remaining);
            Assert.Contains(recentB, remaining);
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    /// <summary>
    /// When no entries are older than the cutoff, purge deletes nothing and returns zero.
    /// </summary>
    [Fact]
    public async Task PurgeOldEntriesAsync_WhenNothingExpired_ReturnsZeroAndKeepsAll()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            var now = DateTime.UtcNow;
            SeedEntry(dbContext, now - TimeSpan.FromDays(1));
            SeedEntry(dbContext, now - TimeSpan.FromDays(10));

            var service = new AuditLogRetentionService(
                dbContext,
                BuildConfiguration(365),
                NullLogger<AuditLogRetentionService>.Instance);

            var purged = await service.PurgeOldEntriesAsync();

            dbContext.ChangeTracker.Clear();
            Assert.Equal(0, purged);
            Assert.Equal(2, dbContext.AuditLogEntries.Count());
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    #endregion

    #region Exception propagation

    /// <summary>
    /// Unlike <c>LogAsync</c> (which swallows audit-write failures), <see cref="AuditLogRetentionService.PurgeOldEntriesAsync"/>
    /// propagates exceptions from the database layer so a background caller can retry. Disposing the context's
    /// underlying connection before the purge forces the DB call to fault, which must surface out of the method.
    /// </summary>
    [Fact]
    public async Task PurgeOldEntriesAsync_PropagatesDatabaseExceptions()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            var service = new AuditLogRetentionService(
                dbContext,
                BuildConfiguration(365),
                NullLogger<AuditLogRetentionService>.Instance);

            // Close the underlying SQLite connection so ExecuteDeleteAsync cannot reach the database.
            connection.Close();

            await Assert.ThrowsAnyAsync<Exception>(() => service.PurgeOldEntriesAsync());
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    #endregion

    #region Focused constructability

    /// <summary>
    /// The retention service is constructable from only <see cref="ApplicationDbContext"/> +
    /// <see cref="IConfiguration"/> (+ logger) and runs its purge — proving it carries no Identity,
    /// <c>UserManager</c>, or display-name-resolver dependency.
    /// </summary>
    [Fact]
    public async Task Service_IsConstructableWithOnlyDbContextAndConfiguration()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            // No UserManager, resolver, or Identity dependency is passed or required.
            var service = new AuditLogRetentionService(
                dbContext,
                BuildConfiguration(365),
                NullLogger<AuditLogRetentionService>.Instance);

            Assert.NotNull(service);

            // It also runs end-to-end against an empty table with no Identity services present.
            var purged = await service.PurgeOldEntriesAsync();
            Assert.Equal(0, purged);
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    #endregion
}
