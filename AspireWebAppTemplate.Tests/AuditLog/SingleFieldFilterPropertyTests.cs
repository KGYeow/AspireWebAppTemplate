// Feature: audit-log, Property 4: Single-field filter correctness
using AspireWebAppTemplate.ApiService.Data;
using AspireWebAppTemplate.ApiService.Data.Entities;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.UI.Utilities;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using Gen = FsCheck.Fluent.Gen;
using Property = FsCheck.Property;

namespace AspireWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Property-based tests verifying that applying ActionType or EntityType pre-filters
/// to an IQueryable before passing to <see cref="QueryableDataGridUtils{T}"/> returns
/// only matching entries with no exclusions.
/// </summary>
/// <remarks>
/// **Validates: Requirements 5.6, 5.7, 5.8**
/// </remarks>
public class SingleFieldFilterPropertyTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;

    public SingleFieldFilterPropertyTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.OpenConnection();
        _dbContext.Database.EnsureCreated();

        // Disable foreign key enforcement so generated entries don't need matching users
        _dbContext.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
    }

    public void Dispose()
    {
        _dbContext.Database.CloseConnection();
        _dbContext.Dispose();
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
    /// Generator for AuditActionType enum values covering all defined members.
    /// </summary>
    private static Gen<AuditActionType> ActionTypeGen()
    {
        var values = Enum.GetValues<AuditActionType>();
        return Gen.Elements(values);
    }

    /// <summary>
    /// Generator for AuditEntityType enum values covering all defined members.
    /// </summary>
    private static Gen<AuditEntityType> EntityTypeGen()
    {
        var values = Enum.GetValues<AuditEntityType>();
        return Gen.Elements(values);
    }

    /// <summary>
    /// Generator for a valid AuditLogEntry with randomly generated field values.
    /// </summary>
    private static Gen<AuditLogEntry> AuditLogEntryGen()
    {
        return ActionTypeGen().SelectMany(actionType =>
            EntityTypeGen().SelectMany(entityType =>
            NonNullStringGen(50).SelectMany(userId =>
            NonNullStringGen(50).SelectMany(userDisplayName =>
            NonNullStringGen(50).SelectMany(entityId =>
            NonNullStringGen(50).SelectMany(entityName =>
            NonNullStringGen(100).Select(description =>
                new AuditLogEntry
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    UserDisplayName = userDisplayName,
                    ActionType = actionType,
                    EntityType = entityType,
                    EntityId = entityId,
                    EntityName = entityName,
                    Description = description,
                    Timestamp = DateTime.UtcNow
                })))))));
    }

    /// <summary>
    /// Generator for a non-empty list of audit log entries (2–10 entries to ensure filter variety).
    /// </summary>
    private static Gen<List<AuditLogEntry>> EntryListGen()
    {
        return Gen.Choose(2, 10)
            .SelectMany(count => Gen.ListOf(AuditLogEntryGen(), count));
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
    /// Property: For any set of audit log entries and any ActionType filter value,
    /// applying a Where filter for that ActionType before passing to QueryableDataGridUtils
    /// returns only entries matching the specified ActionType, and no matching entries are excluded.
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ActionTypeFilter_ReturnsOnlyMatchingEntries_WithNoExclusions()
    {
        var arb = EntryListGen().SelectMany(entries =>
            ActionTypeGen().Select(filterValue => (entries, filterValue)))
            .ToArbitrary();

        return Prop.ForAll(arb, tuple =>
        {
            var (entries, filterValue) = tuple;
            var gridUtils = BuildGridUtils();

            // Seed the database with entries
            _dbContext.AuditLogEntries.AddRange(entries);
            _dbContext.SaveChanges();
            _dbContext.ChangeTracker.Clear();

            // Pre-filter the queryable as the audit log page does (Requirement 5.6)
            var query = _dbContext.AuditLogEntries.AsQueryable()
                .Where(x => x.ActionType == filterValue);

            // Pass to QueryableDataGridUtils
            var state = CreateGridState();
            var result = gridUtils.ServerReloadAsync(query, state).GetAwaiter().GetResult();

            // Calculate expected matches
            var expectedIds = entries
                .Where(e => e.ActionType == filterValue)
                .Select(e => e.Id)
                .OrderBy(id => id)
                .ToList();

            var actualIds = result.Items
                .Select(e => e.Id)
                .OrderBy(id => id)
                .ToList();

            // Verify: results contain only matching entries (no false positives)
            var onlyMatching = result.Items.All(e => e.ActionType == filterValue);

            // Verify: no matching entries are excluded (no false negatives)
            var noneExcluded = expectedIds.SequenceEqual(actualIds);

            // Verify: total count matches
            var totalCorrect = result.TotalItems == expectedIds.Count;

            // Clean up for next iteration
            _dbContext.AuditLogEntries.RemoveRange(_dbContext.AuditLogEntries);
            _dbContext.SaveChanges();
            _dbContext.ChangeTracker.Clear();

            return (onlyMatching && noneExcluded && totalCorrect).Label(
                $"ActionType filter={filterValue}. " +
                $"OnlyMatching={onlyMatching}, NoneExcluded={noneExcluded}, TotalCorrect={totalCorrect}. " +
                $"Expected {expectedIds.Count} entries, got {actualIds.Count}.");
        });
    }

    /// <summary>
    /// Property: For any set of audit log entries and any EntityType filter value,
    /// applying a Where filter for that EntityType before passing to QueryableDataGridUtils
    /// returns only entries matching the specified EntityType, and no matching entries are excluded.
    /// </summary>
    [Property(MaxTest = 1)]
    public Property EntityTypeFilter_ReturnsOnlyMatchingEntries_WithNoExclusions()
    {
        var arb = EntryListGen().SelectMany(entries =>
            EntityTypeGen().Select(filterValue => (entries, filterValue)))
            .ToArbitrary();

        return Prop.ForAll(arb, tuple =>
        {
            var (entries, filterValue) = tuple;
            var gridUtils = BuildGridUtils();

            // Seed the database with entries
            _dbContext.AuditLogEntries.AddRange(entries);
            _dbContext.SaveChanges();
            _dbContext.ChangeTracker.Clear();

            // Pre-filter the queryable as the audit log page does (Requirement 5.7)
            var query = _dbContext.AuditLogEntries.AsQueryable()
                .Where(x => x.EntityType == filterValue);

            // Pass to QueryableDataGridUtils
            var state = CreateGridState();
            var result = gridUtils.ServerReloadAsync(query, state).GetAwaiter().GetResult();

            // Calculate expected matches
            var expectedIds = entries
                .Where(e => e.EntityType == filterValue)
                .Select(e => e.Id)
                .OrderBy(id => id)
                .ToList();

            var actualIds = result.Items
                .Select(e => e.Id)
                .OrderBy(id => id)
                .ToList();

            // Verify: results contain only matching entries (no false positives)
            var onlyMatching = result.Items.All(e => e.EntityType == filterValue);

            // Verify: no matching entries are excluded (no false negatives)
            var noneExcluded = expectedIds.SequenceEqual(actualIds);

            // Verify: total count matches
            var totalCorrect = result.TotalItems == expectedIds.Count;

            // Clean up for next iteration
            _dbContext.AuditLogEntries.RemoveRange(_dbContext.AuditLogEntries);
            _dbContext.SaveChanges();
            _dbContext.ChangeTracker.Clear();

            return (onlyMatching && noneExcluded && totalCorrect).Label(
                $"EntityType filter={filterValue}. " +
                $"OnlyMatching={onlyMatching}, NoneExcluded={noneExcluded}, TotalCorrect={totalCorrect}. " +
                $"Expected {expectedIds.Count} entries, got {actualIds.Count}.");
        });
    }
}
