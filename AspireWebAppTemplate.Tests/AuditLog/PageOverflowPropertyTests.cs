// Feature: audit-log, Property 7: Page overflow returns empty with correct total
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
/// Property-based tests verifying that for any non-empty set of audit log entries and a page
/// number exceeding the total available pages, QueryableDataGridUtils&lt;T&gt;.ServerReloadAsync
/// returns an empty items list while preserving the correct total count of matching entries.
/// </summary>
/// <remarks>
/// **Validates: Requirements 4.5**
/// </remarks>
public class PageOverflowPropertyTests
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
    /// Seeds the specified number of audit log entries into the database.
    /// </summary>
    private static void SeedEntries(ApplicationDbContext dbContext, int count)
    {
        var baseDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < count; i++)
        {
            dbContext.AuditLogEntries.Add(new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                UserId = string.Empty,
                UserDisplayName = $"User{i}",
                ActionType = AuditActionType.LoginSuccess,
                EntityType = AuditEntityType.System,
                EntityId = $"entity-{i}",
                EntityName = $"Entity{i}",
                Description = $"Entry {i}",
                Timestamp = baseDate.AddHours(i)
            });
        }
        dbContext.SaveChanges();
    }

    /// <summary>
    /// **Validates: Requirements 4.5**
    /// For any non-empty set of audit log entries and a page number exceeding the total
    /// available pages, ServerReloadAsync SHALL return an empty items list while preserving
    /// the correct total count of matching entries.
    /// </summary>
    [Property(MaxTest = 1)]
    public Property PageBeyondAvailableData_ReturnsEmptyItemsWithCorrectTotalCount()
    {
        // Generate entry count (1 to 20) and page size (1 to 10)
        var entryCountGen = Gen.Choose(1, 20);
        var pageSizeGen = Gen.Choose(1, 10);

        return Prop.ForAll(
            Arb.From(entryCountGen),
            Arb.From(pageSizeGen),
            (int entryCount, int pageSize) =>
        {
            var (dbContext, connection) = CreateDbContext();
            try
            {
                // Seed entries
                SeedEntries(dbContext, entryCount);

                // Calculate a page index that exceeds available pages
                // Total pages = ceil(entryCount / pageSize), so use that value as page index (0-based)
                var totalPages = (int)Math.Ceiling((double)entryCount / pageSize);
                var overflowPageIndex = totalPages; // This is beyond the last valid page (0-based)

                // Create GridState requesting a page beyond available data
                var state = new GridState<AuditLogEntry>
                {
                    Page = overflowPageIndex,
                    PageSize = pageSize,
                    SortDefinitions = new List<SortDefinition<AuditLogEntry>>(),
                    FilterDefinitions = new List<IFilterDefinition<AuditLogEntry>>()
                };

                // Act: call ServerReloadAsync with overflow page
                var gridUtils = CreateGridUtils();
                var result = gridUtils.ServerReloadAsync(
                    dbContext.AuditLogEntries.AsQueryable(),
                    state).GetAwaiter().GetResult();

                var items = result.Items.ToList();
                var totalItems = result.TotalItems;

                // Assert: items should be empty and TotalItems should equal the total record count
                var itemsEmpty = items.Count == 0;
                var totalCorrect = totalItems == entryCount;

                return (itemsEmpty && totalCorrect)
                    .Label($"Expected empty items and TotalItems={entryCount}. " +
                           $"Got {items.Count} items, TotalItems={totalItems}. " +
                           $"EntryCount={entryCount}, PageSize={pageSize}, PageIndex={overflowPageIndex}, TotalPages={totalPages}");
            }
            finally
            {
                dbContext.Dispose();
                connection.Dispose();
            }
        });
    }
}
