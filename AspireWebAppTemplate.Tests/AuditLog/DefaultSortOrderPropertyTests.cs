// Feature: audit-log, Property 6: Default sort order
using AspireWebAppTemplate.ApiService.Data;
using AspireWebAppTemplate.ApiService.Data.Entities;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.UI.Utilities;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using Gen = FsCheck.Fluent.Gen;
using Property = FsCheck.Property;

namespace AspireWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Property-based tests verifying that when QueryableDataGridUtils&lt;T&gt;.ServerReloadAsync
/// is called with an empty SortDefinitions in GridState, the returned entries are ordered
/// by Timestamp descending (newest first).
/// </summary>
/// <remarks>
/// **Validates: Requirements 4.10**
/// </remarks>
public class DefaultSortOrderPropertyTests
{
    /// <summary>
    /// Creates a SQLite in-memory ApplicationDbContext for testing.
    /// The connection must be kept open for the lifetime of the test.
    /// </summary>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Disable FK enforcement so we can seed AuditLogEntry without creating ApplicationUser records.
        // This test focuses on sort order correctness, not referential integrity.
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys = OFF;";
            cmd.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();
        return (dbContext, connection);
    }

    /// <summary>
    /// Creates a QueryableDataGridUtils configured for AuditLogEntry with Timestamp mapped
    /// as the DateTime field (which becomes the default sort field).
    /// </summary>
    private static QueryableDataGridUtils<AuditLogEntry> CreateGridUtils()
    {
        return new QueryableDataGridUtils<AuditLogEntry>()
            .MapString(nameof(AuditLogEntry.UserDisplayName), x => x.UserDisplayName)
            .MapString(nameof(AuditLogEntry.Description), x => x.Description)
            .MapDateTime(nameof(AuditLogEntry.Timestamp), x => x.Timestamp);
    }

    /// <summary>
    /// Seeds multiple audit log entries with distinct timestamps into the database.
    /// </summary>
    private static void SeedEntries(ApplicationDbContext dbContext, DateTime[] timestamps)
    {
        foreach (var ts in timestamps)
        {
            dbContext.AuditLogEntries.Add(new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                UserId = string.Empty,
                UserDisplayName = "TestUser",
                ActionType = AuditActionType.LoginSuccess,
                EntityType = AuditEntityType.System,
                EntityId = "test-entity",
                EntityName = "TestEntity",
                Description = $"Entry at {ts:O}",
                Timestamp = ts
            });
        }
        dbContext.SaveChanges();
    }

    /// <summary>
    /// **Validates: Requirements 4.10**
    /// For any set of audit log entries, when ServerReloadAsync is called with empty
    /// SortDefinitions in GridState, the returned entries SHALL be ordered by Timestamp
    /// descending (newest first).
    /// </summary>
    [Property(MaxTest = 1)]
    public Property EmptySortDefinitions_ReturnsEntriesOrderedByTimestampDescending()
    {
        // Generate a count of entries (3 to 10) to ensure meaningful ordering verification
        var entryCountGen = Gen.Choose(3, 10);

        return Prop.ForAll(Arb.From(entryCountGen), (int entryCount) =>
        {
            var (dbContext, connection) = CreateDbContext();
            try
            {
                // Create distinct timestamps from offsets (using a fixed base date to avoid timezone issues)
                var baseDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var timestamps = Enumerable.Range(0, entryCount)
                    .Select(i => baseDate.AddHours(i * 7)) // Use distinct hour offsets
                    .ToArray();

                // Shuffle timestamps to seed entries in arbitrary (non-sorted) order
                var rng = new Random(42);
                var shuffled = timestamps.OrderBy(_ => rng.Next()).ToArray();

                // Seed entries in shuffled order
                SeedEntries(dbContext, shuffled);

                // Create GridState with empty SortDefinitions (triggers default sort)
                var state = new GridState<AuditLogEntry>
                {
                    Page = 0,
                    PageSize = entryCount,
                    SortDefinitions = new List<SortDefinition<AuditLogEntry>>(),
                    FilterDefinitions = new List<IFilterDefinition<AuditLogEntry>>()
                };

                // Act: call ServerReloadAsync with empty sort definitions
                var gridUtils = CreateGridUtils();
                var result = gridUtils.ServerReloadAsync(
                    dbContext.AuditLogEntries.AsQueryable(),
                    state).GetAwaiter().GetResult();

                var items = result.Items.ToList();

                // Assert: items should be ordered by Timestamp descending (newest first)
                var isDescending = true;
                for (var i = 0; i < items.Count - 1; i++)
                {
                    if (items[i].Timestamp < items[i + 1].Timestamp)
                    {
                        isDescending = false;
                        break;
                    }
                }

                return (items.Count == entryCount && isDescending)
                    .Label($"Expected {entryCount} items ordered by Timestamp DESC. " +
                           $"Got {items.Count} items, isDescending={isDescending}. " +
                           $"Timestamps: [{string.Join(", ", items.Select(i => i.Timestamp.ToString("O")))}]");
            }
            finally
            {
                dbContext.Dispose();
                connection.Dispose();
            }
        });
    }
}
