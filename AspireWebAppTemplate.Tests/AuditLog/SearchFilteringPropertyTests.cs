// Feature: audit-log, Property 3: Search text filtering correctness
using BlazorWebAppTemplate.Core.Domain.Enums;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using BlazorWebAppTemplate.UI.Utilities;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using Gen = FsCheck.Fluent.Gen;
using Property = FsCheck.Property;

namespace BlazorWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Property-based tests verifying that global search results from QueryableDataGridUtils
/// contain only entries where at least one of UserDisplayName, EntityName, or Description
/// contains the search text (case-insensitive), and no matching entries are excluded.
/// </summary>
/// <remarks>
/// **Validates: Requirements 4.9**
/// </remarks>
public class SearchFilteringPropertyTests
{
    /// <summary>
    /// Creates a SQLite in-memory ApplicationDbContext for testing.
    /// The connection must be kept open for the lifetime of the test.
    /// </summary>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();
        return (dbContext, connection);
    }

    /// <summary>
    /// Creates a QueryableDataGridUtils configured for AuditLogEntry with the three
    /// searchable string fields (UserDisplayName, EntityName, Description) and Timestamp.
    /// </summary>
    private static QueryableDataGridUtils<AuditLogEntry> CreateGridUtils()
    {
        return new QueryableDataGridUtils<AuditLogEntry>()
            .MapString(nameof(AuditLogEntry.UserDisplayName), x => x.UserDisplayName)
            .MapString(nameof(AuditLogEntry.EntityName), x => x.EntityName)
            .MapString(nameof(AuditLogEntry.Description), x => x.Description)
            .MapDateTime(nameof(AuditLogEntry.Timestamp), x => x.Timestamp);
    }

    /// <summary>
    /// Creates a test user in the database to satisfy FK constraints on AuditLogEntry.UserId.
    /// Returns the user's Id for use in seeded entries.
    /// </summary>
    private static string EnsureTestUser(ApplicationDbContext dbContext)
    {
        var userId = "test-user-id";
        if (!dbContext.Users.Any(u => u.Id == userId))
        {
            dbContext.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = "testuser@test.com",
                NormalizedUserName = "TESTUSER@TEST.COM",
                Email = "testuser@test.com",
                NormalizedEmail = "TESTUSER@TEST.COM",
                DisplayName = "TestUser",
                SecurityStamp = Guid.NewGuid().ToString()
            });
            dbContext.SaveChanges();
        }
        return userId;
    }

    /// <summary>
    /// Seeds multiple audit log entries with varied field values into the database.
    /// Uses the provided seed value to generate deterministic but varied entries.
    /// </summary>
    private static List<AuditLogEntry> SeedEntries(ApplicationDbContext dbContext, int seed)
    {
        // Ensure a user exists to satisfy FK constraint
        var userId = EnsureTestUser(dbContext);

        // Generate 3-8 entries with varied values based on seed
        var random = new Random(seed);
        var count = random.Next(3, 9);
        var entries = new List<AuditLogEntry>();

        var names = new[] { "Alice", "Bob", "Charlie", "Dave", "Eve", "Frank", "Grace", "Heidi" };
        var entities = new[] { "Invoice", "Order", "Report", "Payment", "UserAccount", "Settings", "Dashboard", "Export" };
        var descriptions = new[] { "Created new record", "Updated profile settings", "Deleted old entry",
            "Modified configuration", "Generated report", "Exported data", "Changed password", "Assigned role" };

        for (var i = 0; i < count; i++)
        {
            var entry = new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                UserDisplayName = names[random.Next(names.Length)],
                ActionType = (AuditActionType)random.Next(16),
                EntityType = (AuditEntityType)random.Next(4),
                EntityId = Guid.NewGuid().ToString(),
                EntityName = entities[random.Next(entities.Length)],
                Description = descriptions[random.Next(descriptions.Length)],
                Timestamp = DateTime.UtcNow.AddHours(-random.Next(1000))
            };
            entries.Add(entry);
        }

        dbContext.AuditLogEntries.AddRange(entries);
        dbContext.SaveChanges();
        return entries;
    }

    /// <summary>
    /// Determines whether a given entry matches the search term in any of the three searchable fields
    /// (UserDisplayName, EntityName, Description) using case-insensitive contains.
    /// This serves as the reference implementation for verifying the query utility's behavior.
    /// </summary>
    private static bool EntryMatchesSearch(AuditLogEntry entry, string searchTerm)
    {
        return (entry.UserDisplayName ?? string.Empty).Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            || (entry.EntityName ?? string.Empty).Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            || (entry.Description ?? string.Empty).Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// **Validates: Requirements 4.9**
    /// For any set of audit log entries and any non-empty search string, the results returned
    /// by ServerReloadAsync with that global search term SHALL contain only entries where at
    /// least one of UserDisplayName, EntityName, or Description contains the search text
    /// (case-insensitive), and no matching entries SHALL be excluded from the results.
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Search_Results_Contain_Only_Matching_Entries_And_No_Matches_Are_Excluded()
    {
        // Use a seed to generate entries deterministically, and a search term index
        // to select search terms that may or may not match entries
        var seedGen = Gen.Choose(1, 100000);
        var searchTermIndexGen = Gen.Choose(0, 7);

        return Prop.ForAll(
            Arb.From(seedGen),
            Arb.From(searchTermIndexGen),
            (int seed, int searchTermIndex) =>
        {
            // Use search terms that cover various scenarios:
            // - Substrings of known field values (should match)
            // - Random strings (may or may not match)
            var searchTerms = new[] { "ali", "ord", "upd", "set", "rep", "inv", "exp", "rol" };
            var searchTerm = searchTerms[searchTermIndex];

            var (dbContext, connection) = CreateDbContext();
            try
            {
                // Seed entries deterministically based on the random seed
                var entries = SeedEntries(dbContext, seed);

                // Set up QueryableDataGridUtils with the three searchable string fields
                var gridUtils = CreateGridUtils();

                // Call ServerReloadAsync with the global search term across the three fields
                var state = new GridState<AuditLogEntry>
                {
                    Page = 0,
                    PageSize = 100, // Large enough to get all results in one page
                    SortDefinitions = new List<SortDefinition<AuditLogEntry>>(),
                    FilterDefinitions = new List<IFilterDefinition<AuditLogEntry>>()
                };

                var result = gridUtils.ServerReloadAsync(
                    dbContext.AuditLogEntries.AsQueryable(),
                    state,
                    globalSearchTerm: searchTerm,
                    globalSearchFields: new[]
                    {
                        nameof(AuditLogEntry.UserDisplayName),
                        nameof(AuditLogEntry.EntityName),
                        nameof(AuditLogEntry.Description)
                    }).GetAwaiter().GetResult();

                var resultItems = result.Items.ToList();

                // Property A: Every returned entry must match the search term in at least one field
                var allReturnedMatch = resultItems.All(item => EntryMatchesSearch(item, searchTerm));

                // Property B: No matching entries should be excluded (completeness)
                var expectedMatchIds = entries
                    .Where(e => EntryMatchesSearch(e, searchTerm))
                    .Select(e => e.Id)
                    .ToHashSet();

                var actualIds = resultItems.Select(item => item.Id).ToHashSet();
                var noExclusions = expectedMatchIds.All(id => actualIds.Contains(id));

                // Both properties must hold
                return (allReturnedMatch && noExclusions)
                    .Label($"Search term: '{searchTerm}'. " +
                           $"Entries: {entries.Count}, Expected matches: {expectedMatchIds.Count}, " +
                           $"Actual results: {resultItems.Count}. " +
                           $"AllReturnedMatch={allReturnedMatch}, NoExclusions={noExclusions}");
            }
            finally
            {
                dbContext.Dispose();
                connection.Dispose();
            }
        });
    }
}
