// Feature: audit-log, Property 5: Date range filter correctness
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
/// Property-based tests verifying that date range filtering (start only, end only, or both)
/// applied to an IQueryable before passing to <see cref="QueryableDataGridUtils{T}"/>
/// returns only entries with Timestamp within the specified range (inclusive), and no entries
/// within the range are excluded.
/// </summary>
/// <remarks>
/// **Validates: Requirements 5.8**
/// </remarks>
public class DateRangeFilterPropertyTests
{
    /// <summary>
    /// Creates a SQLite in-memory ApplicationDbContext for testing.
    /// Disables FK enforcement so entries can be seeded without ApplicationUser records.
    /// </summary>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Disable FK enforcement so we can seed AuditLogEntry without creating ApplicationUser records.
        // This test focuses on date range filtering correctness, not referential integrity.
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
    /// Generator for non-null strings constrained to a maximum length.
    /// Produces printable ASCII strings to avoid encoding issues in SQLite.
    /// </summary>
    private static Gen<string> NonNullStringGen(int maxLength)
    {
        return Gen.Choose(1, Math.Max(1, maxLength))
            .SelectMany(len =>
                Gen.ArrayOf(Gen.Choose(32, 126).Select(c => (char)c), len)
                   .Select(chars => new string(chars)));
    }

    /// <summary>
    /// Generator for a UTC DateTime within a reasonable range (2020–2026).
    /// Uses second-level precision to avoid sub-second rounding issues with SQLite.
    /// </summary>
    private static Gen<DateTime> TimestampGen()
    {
        // Generate timestamps between 2020-01-01 and 2026-12-31 using day offsets
        var baseDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var totalDays = (int)(new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc) - baseDate).TotalDays;

        return Gen.Choose(0, totalDays).SelectMany(dayOffset =>
            Gen.Choose(0, 86399).Select(secondOffset =>
                baseDate.AddDays(dayOffset).AddSeconds(secondOffset)));
    }

    /// <summary>
    /// Generator for a valid AuditLogEntry with a randomly generated timestamp.
    /// </summary>
    private static Gen<AuditLogEntry> AuditLogEntryGen()
    {
        var actionTypeGen = Gen.Elements(Enum.GetValues<AuditActionType>());
        var entityTypeGen = Gen.Elements(Enum.GetValues<AuditEntityType>());

        return TimestampGen().SelectMany(timestamp =>
            actionTypeGen.SelectMany(actionType =>
            entityTypeGen.SelectMany(entityType =>
            NonNullStringGen(50).SelectMany(userDisplayName =>
            NonNullStringGen(50).SelectMany(entityId =>
            NonNullStringGen(50).SelectMany(entityName =>
            NonNullStringGen(100).Select(description =>
                new AuditLogEntry
                {
                    Id = Guid.NewGuid(),
                    UserId = string.Empty,
                    UserDisplayName = userDisplayName,
                    ActionType = actionType,
                    EntityType = entityType,
                    EntityId = entityId,
                    EntityName = entityName,
                    Description = description,
                    Timestamp = timestamp
                })))))));
    }

    /// <summary>
    /// Generator for a non-empty list of audit log entries (3–10 entries to ensure variety in timestamps).
    /// </summary>
    private static Gen<List<AuditLogEntry>> EntryListGen()
    {
        return Gen.Choose(3, 10)
            .SelectMany(count => Gen.ListOf(AuditLogEntryGen(), count));
    }

    /// <summary>
    /// Generator for a pair of DateTimes where the first is less than or equal to the second,
    /// suitable for use as a date range filter.
    /// </summary>
    private static Gen<(DateTime start, DateTime end)> DateRangePairGen()
    {
        return TimestampGen().SelectMany(d1 =>
            TimestampGen().Select(d2 =>
                d1 <= d2 ? (d1, d2) : (d2, d1)));
    }

    /// <summary>
    /// Builds a QueryableDataGridUtils configured for AuditLogEntry with Timestamp mapped
    /// (needed for default sort fallback).
    /// </summary>
    private static QueryableDataGridUtils<AuditLogEntry> BuildGridUtils()
    {
        return new QueryableDataGridUtils<AuditLogEntry>()
            .MapString(nameof(AuditLogEntry.UserDisplayName), x => x.UserDisplayName)
            .MapString(nameof(AuditLogEntry.EntityName), x => x.EntityName)
            .MapString(nameof(AuditLogEntry.Description), x => x.Description)
            .MapDateTime(nameof(AuditLogEntry.Timestamp), x => x.Timestamp);
    }

    /// <summary>
    /// Creates a GridState with a large enough page size to return all entries (no pagination effect).
    /// </summary>
    private static GridState<AuditLogEntry> CreateGridState()
    {
        return new GridState<AuditLogEntry>
        {
            Page = 0,
            PageSize = 1000,
            SortDefinitions = new List<SortDefinition<AuditLogEntry>>(),
            FilterDefinitions = new List<IFilterDefinition<AuditLogEntry>>()
        };
    }

    /// <summary>
    /// Property: For any set of audit log entries and any start date, applying a >= filter
    /// on Timestamp returns only entries on or after that date (inclusive), and no entries
    /// within the range are excluded.
    /// </summary>
    [Property(MaxTest = 1)]
    public Property StartOnly_Filter_Returns_Only_Entries_On_Or_After_Start()
    {
        var arb = EntryListGen().SelectMany(entries =>
            TimestampGen().Select(startDate => (entries, startDate)))
            .ToArbitrary();

        return Prop.ForAll(arb, tuple =>
        {
            var (entries, startDate) = tuple;
            var (dbContext, connection) = CreateDbContext();
            try
            {
                var gridUtils = BuildGridUtils();

                // Seed the database with entries
                dbContext.AuditLogEntries.AddRange(entries);
                dbContext.SaveChanges();
                dbContext.ChangeTracker.Clear();

                // Apply start-only date filter as the audit log page does (Requirement 5.8)
                var query = dbContext.AuditLogEntries.AsQueryable()
                    .Where(x => x.Timestamp >= startDate);

                // Pass to QueryableDataGridUtils
                var state = CreateGridState();
                var result = gridUtils.ServerReloadAsync(query, state).GetAwaiter().GetResult();

                // Calculate expected matches
                var expectedIds = entries
                    .Where(e => e.Timestamp >= startDate)
                    .Select(e => e.Id)
                    .OrderBy(id => id)
                    .ToList();

                var actualIds = result.Items
                    .Select(e => e.Id)
                    .OrderBy(id => id)
                    .ToList();

                // Verify: all results have Timestamp >= startDate (no false positives)
                var allWithinRange = result.Items.All(e => e.Timestamp >= startDate);

                // Verify: no entries within range are excluded (no false negatives)
                var noneExcluded = expectedIds.SequenceEqual(actualIds);

                // Verify: total count matches
                var totalCorrect = result.TotalItems == expectedIds.Count;

                return (allWithinRange && noneExcluded && totalCorrect).Label(
                    $"Start-only filter: startDate={startDate:O}. " +
                    $"AllWithinRange={allWithinRange}, NoneExcluded={noneExcluded}, TotalCorrect={totalCorrect}. " +
                    $"Expected {expectedIds.Count} entries, got {actualIds.Count}.");
            }
            finally
            {
                dbContext.Dispose();
                connection.Dispose();
            }
        });
    }

    /// <summary>
    /// Property: For any set of audit log entries and any end date, applying a &lt;= filter
    /// on Timestamp returns only entries on or before that date (inclusive), and no entries
    /// within the range are excluded.
    /// </summary>
    [Property(MaxTest = 1)]
    public Property EndOnly_Filter_Returns_Only_Entries_On_Or_Before_End()
    {
        var arb = EntryListGen().SelectMany(entries =>
            TimestampGen().Select(endDate => (entries, endDate)))
            .ToArbitrary();

        return Prop.ForAll(arb, tuple =>
        {
            var (entries, endDate) = tuple;
            var (dbContext, connection) = CreateDbContext();
            try
            {
                var gridUtils = BuildGridUtils();

                // Seed the database with entries
                dbContext.AuditLogEntries.AddRange(entries);
                dbContext.SaveChanges();
                dbContext.ChangeTracker.Clear();

                // Apply end-only date filter as the audit log page does (Requirement 5.8)
                var query = dbContext.AuditLogEntries.AsQueryable()
                    .Where(x => x.Timestamp <= endDate);

                // Pass to QueryableDataGridUtils
                var state = CreateGridState();
                var result = gridUtils.ServerReloadAsync(query, state).GetAwaiter().GetResult();

                // Calculate expected matches
                var expectedIds = entries
                    .Where(e => e.Timestamp <= endDate)
                    .Select(e => e.Id)
                    .OrderBy(id => id)
                    .ToList();

                var actualIds = result.Items
                    .Select(e => e.Id)
                    .OrderBy(id => id)
                    .ToList();

                // Verify: all results have Timestamp <= endDate (no false positives)
                var allWithinRange = result.Items.All(e => e.Timestamp <= endDate);

                // Verify: no entries within range are excluded (no false negatives)
                var noneExcluded = expectedIds.SequenceEqual(actualIds);

                // Verify: total count matches
                var totalCorrect = result.TotalItems == expectedIds.Count;

                return (allWithinRange && noneExcluded && totalCorrect).Label(
                    $"End-only filter: endDate={endDate:O}. " +
                    $"AllWithinRange={allWithinRange}, NoneExcluded={noneExcluded}, TotalCorrect={totalCorrect}. " +
                    $"Expected {expectedIds.Count} entries, got {actualIds.Count}.");
            }
            finally
            {
                dbContext.Dispose();
                connection.Dispose();
            }
        });
    }

    /// <summary>
    /// Property: For any set of audit log entries and any date range (start &lt;= end),
    /// applying both >= start and &lt;= end filters on Timestamp returns only entries within
    /// that range (inclusive on both sides), and no entries within the range are excluded.
    /// </summary>
    [Property(MaxTest = 1)]
    public Property BothStartAndEnd_Filter_Returns_Only_Entries_Within_Range()
    {
        var arb = EntryListGen().SelectMany(entries =>
            DateRangePairGen().Select(range => (entries, range.start, range.end)))
            .ToArbitrary();

        return Prop.ForAll(arb, tuple =>
        {
            var (entries, startDate, endDate) = tuple;
            var (dbContext, connection) = CreateDbContext();
            try
            {
                var gridUtils = BuildGridUtils();

                // Seed the database with entries
                dbContext.AuditLogEntries.AddRange(entries);
                dbContext.SaveChanges();
                dbContext.ChangeTracker.Clear();

                // Apply both start and end date filters as the audit log page does (Requirement 5.8)
                var query = dbContext.AuditLogEntries.AsQueryable()
                    .Where(x => x.Timestamp >= startDate)
                    .Where(x => x.Timestamp <= endDate);

                // Pass to QueryableDataGridUtils
                var state = CreateGridState();
                var result = gridUtils.ServerReloadAsync(query, state).GetAwaiter().GetResult();

                // Calculate expected matches
                var expectedIds = entries
                    .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                    .Select(e => e.Id)
                    .OrderBy(id => id)
                    .ToList();

                var actualIds = result.Items
                    .Select(e => e.Id)
                    .OrderBy(id => id)
                    .ToList();

                // Verify: all results have Timestamp within range (no false positives)
                var allWithinRange = result.Items.All(e => e.Timestamp >= startDate && e.Timestamp <= endDate);

                // Verify: no entries within range are excluded (no false negatives)
                var noneExcluded = expectedIds.SequenceEqual(actualIds);

                // Verify: total count matches
                var totalCorrect = result.TotalItems == expectedIds.Count;

                return (allWithinRange && noneExcluded && totalCorrect).Label(
                    $"Both filter: range=[{startDate:O}, {endDate:O}]. " +
                    $"AllWithinRange={allWithinRange}, NoneExcluded={noneExcluded}, TotalCorrect={totalCorrect}. " +
                    $"Expected {expectedIds.Count} entries, got {actualIds.Count}.");
            }
            finally
            {
                dbContext.Dispose();
                connection.Dispose();
            }
        });
    }
}
