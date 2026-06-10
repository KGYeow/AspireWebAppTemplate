// Feature: audit-log, Property 11: Purge correctness
using BlazorWebAppTemplate.Core.Domain.Enums;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using BlazorWebAppTemplate.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Gen = FsCheck.Fluent.Gen;
using Property = FsCheck.Property;

namespace BlazorWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Property-based tests verifying purge correctness: after invoking PurgeOldEntriesAsync,
/// no entries with a Timestamp older than (UtcNow minus retention days) remain,
/// and all entries within the retention window are preserved.
/// </summary>
/// <remarks>
/// **Validates: Requirements 10.4**
/// </remarks>
public class PurgeCorrectnessPropertyTests
{
    /// <summary>
    /// Creates a SQLite in-memory ApplicationDbContext for testing.
    /// The connection must be kept open for the lifetime of the test.
    /// Foreign key enforcement is disabled to allow seeding AuditLogEntry
    /// without requiring a corresponding ApplicationUser record.
    /// </summary>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Disable foreign key enforcement so audit entries can be seeded
        // without requiring matching ApplicationUser records in the database.
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
    /// Creates an AuditLogService with the given configuration and database context.
    /// </summary>
    private static AuditLogService CreateService(ApplicationDbContext dbContext, int retentionDays)
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuditLog:RetentionDays"] = retentionDays.ToString()
            })
            .Build();

        return new AuditLogService(
            dbContext,
            userManagerMock.Object,
            NullLogger<AuditLogService>.Instance,
            configuration);
    }

    /// <summary>
    /// Seeds an audit log entry with the specified timestamp into the database.
    /// </summary>
    private static AuditLogEntry SeedEntry(ApplicationDbContext dbContext, DateTime timestamp)
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
        return entry;
    }

    /// <summary>
    /// Property: For any set of audit log entries with varying Timestamps and any valid retention period,
    /// after invoking the purge method, no entries with a Timestamp older than (UtcNow minus retention days)
    /// SHALL remain, and all entries within the retention window SHALL be preserved.
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property AfterPurge_NoExpiredEntriesRemain_And_AllValidEntriesPreserved()
    {
        // Generator for a valid retention period within the allowed range (1–3650 days)
        var retentionDaysGen = Gen.Choose(1, 3650);

        // Generator for day offsets representing how many days ago each entry was created.
        // Offsets in range 0–7300 (twice the max retention) ensure a mix of entries
        // inside and outside the retention window.
        var dayOffsetsGen = Gen.Choose(1, 20)
            .SelectMany(count => Gen.ArrayOf(Gen.Choose(0, 7300), count));

        var gen = retentionDaysGen.SelectMany(retentionDays =>
            dayOffsetsGen.Select(offsets => (retentionDays, offsets)));

        return Prop.ForAll(Arb.From(gen), ((int retentionDays, int[] offsets) input) =>
        {
            var (dbContext, connection) = CreateDbContext();
            try
            {
                var now = DateTime.UtcNow;
                var cutoff = now - TimeSpan.FromDays(input.retentionDays);

                // Seed entries at various timestamps based on generated day offsets
                var seededEntries = new List<(Guid Id, DateTime Timestamp, bool ShouldSurvive)>();
                foreach (var dayOffset in input.offsets)
                {
                    var timestamp = now - TimeSpan.FromDays(dayOffset);
                    var entry = SeedEntry(dbContext, timestamp);
                    // Entry survives if its timestamp is on or after the cutoff (not older than retention)
                    var shouldSurvive = timestamp >= cutoff;
                    seededEntries.Add((entry.Id, timestamp, shouldSurvive));
                }

                // Configure and invoke purge
                var service = CreateService(dbContext, input.retentionDays);
                service.PurgeOldEntriesAsync().GetAwaiter().GetResult();

                // Clear change tracker to force a fresh read from the database
                dbContext.ChangeTracker.Clear();

                // Verify: get remaining entries
                var remainingIds = dbContext.AuditLogEntries
                    .Select(e => e.Id)
                    .ToHashSet();

                // Check 1: No entries older than retention remain
                var expiredStillPresent = seededEntries
                    .Where(e => !e.ShouldSurvive && remainingIds.Contains(e.Id))
                    .ToList();

                // Check 2: All entries within retention window are preserved
                var validMissing = seededEntries
                    .Where(e => e.ShouldSurvive && !remainingIds.Contains(e.Id))
                    .ToList();

                var noExpiredRemain = expiredStillPresent.Count == 0;
                var allValidPreserved = validMissing.Count == 0;

                return (noExpiredRemain && allValidPreserved).Label(
                    $"RetentionDays={input.retentionDays}, TotalSeeded={input.offsets.Length}, " +
                    $"ExpiredStillPresent={expiredStillPresent.Count}, ValidMissing={validMissing.Count}, " +
                    $"Remaining={remainingIds.Count}");
            }
            finally
            {
                dbContext.Dispose();
                connection.Dispose();
            }
        });
    }
}
