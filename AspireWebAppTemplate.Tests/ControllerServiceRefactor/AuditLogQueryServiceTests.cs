// Feature: controller-service-refactor, Property 2: Audit log pagination invariants
// Feature: controller-service-refactor, Property 3: Audit log search filter correctness
// Feature: controller-service-refactor, Property 4: Audit log lookup round-trip
// Feature: controller-service-refactor, Property 5: Audit log export row cap
using AspireWebAppTemplate.ApiService.Data;
using AspireWebAppTemplate.ApiService.Data.Entities;
using AspireWebAppTemplate.ApiService.Services;
using AspireWebAppTemplate.Core.Common.Defaults;
using AspireWebAppTemplate.Core.Contracts.AuditLog;
using AspireWebAppTemplate.Core.Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace AspireWebAppTemplate.Tests.ControllerServiceRefactor;

/// <summary>
/// Property-based tests verifying the correctness of <see cref="AuditLogService"/>
/// query methods for pagination, search filtering, lookup round-trip, and export row cap invariants.
/// </summary>
/// <remarks>
/// Uses SQLite in-memory database for real EF Core queries (no mocking).
/// **Validates: Requirements 2.1, 2.2, 2.3, 2.5**
/// </remarks>
public class AuditLogQueryServiceTests
{
    #region Helpers

    /// <summary>
    /// Creates a fresh SQLite in-memory ApplicationDbContext for test isolation.
    /// Each call returns a new database instance with schema created.
    /// Foreign keys are disabled to allow seeding AuditLogEntry without requiring
    /// a matching ApplicationUser record.
    /// </summary>
    private static ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        var context = new ApplicationDbContext(options);
        context.Database.OpenConnection();
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Creates an AuditLogService instance with the given context and mocked dependencies.
    /// The UserManager, Logger, and Configuration are mocked since query methods don't use them.
    /// </summary>
    private static AuditLogService CreateService(ApplicationDbContext context)
    {
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        var mockLogger = new Mock<ILogger<AuditLogService>>();
        var mockConfig = new Mock<IConfiguration>();

        return new AuditLogService(context, mockUserManager.Object, mockLogger.Object, mockConfig.Object);
    }

    /// <summary>
    /// Seeds the given context with a specified number of audit log entries using varied data.
    /// </summary>
    private static void SeedEntries(ApplicationDbContext context, int count)
    {
        var actionTypes = Enum.GetValues<AuditActionType>();
        var entityTypes = Enum.GetValues<AuditEntityType>();

        for (int i = 0; i < count; i++)
        {
            context.AuditLogEntries.Add(new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                UserId = $"user-{i}",
                UserDisplayName = $"User {i}",
                ActionType = actionTypes[i % actionTypes.Length],
                EntityType = entityTypes[i % entityTypes.Length],
                EntityId = $"entity-{i}",
                EntityName = $"Entity {i}",
                Description = $"Action {i} was performed",
                OldValues = null,
                NewValues = null,
                IpAddress = $"192.168.1.{i % 256}",
                Timestamp = DateTime.UtcNow.AddMinutes(-i)
            });
        }

        context.SaveChanges();
    }

    /// <summary>
    /// Seeds entries with specific searchable text in various fields for search filter tests.
    /// </summary>
    private static void SeedEntriesWithSearchableText(
        ApplicationDbContext context,
        string searchableText,
        int matchingCount,
        int nonMatchingCount)
    {
        // Add entries that contain the search term in different fields
        for (int i = 0; i < matchingCount; i++)
        {
            var field = i % 4;
            context.AuditLogEntries.Add(new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                UserId = $"user-match-{i}",
                UserDisplayName = field == 0 ? $"Name {searchableText} Here" : "OtherUser",
                ActionType = AuditActionType.UserCreated,
                EntityType = AuditEntityType.User,
                EntityId = field == 3 ? $"id-{searchableText}-ref" : $"entity-{i}",
                EntityName = field == 1 ? $"Ent {searchableText} Name" : "OtherEntity",
                Description = field == 2 ? $"Did {searchableText} action" : "Other description",
                OldValues = null,
                NewValues = null,
                IpAddress = "10.0.0.1",
                Timestamp = DateTime.UtcNow.AddMinutes(-i)
            });
        }

        // Add entries that do NOT contain the search term in any searchable field
        for (int i = 0; i < nonMatchingCount; i++)
        {
            context.AuditLogEntries.Add(new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                UserId = $"user-nomatch-{i}",
                UserDisplayName = "Completely Different",
                ActionType = AuditActionType.RoleCreated,
                EntityType = AuditEntityType.Role,
                EntityId = $"no-match-{i}",
                EntityName = "Unrelated Entity",
                Description = "Unrelated action happened",
                OldValues = null,
                NewValues = null,
                IpAddress = "10.0.0.2",
                Timestamp = DateTime.UtcNow.AddMinutes(-(matchingCount + i))
            });
        }

        context.SaveChanges();
    }

    #endregion

    #region Property 2: Audit log pagination invariants

    /// <summary>
    /// Property: For any AuditLogQueryParams with page >= 0 and pageSize > 0, the returned
    /// PagedResult satisfies Items.Count &lt;= PageSize, Page == queryParams.Page, and
    /// TotalCount >= Items.Count.
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property Pagination_Invariants_HoldForAnyValidParams()
    {
        var pageGen = Gen.Choose(0, 5);
        var pageSizeGen = Gen.Choose(1, 10);

        var paramsGen = from page in pageGen
                        from pageSize in pageSizeGen
                        select new AuditLogQueryParams { Page = page, PageSize = pageSize };

        return Prop.ForAll(Arb.From(paramsGen), (AuditLogQueryParams queryParams) =>
        {
            using var context = CreateInMemoryContext();
            SeedEntries(context, 15);

            var service = CreateService(context);
            var result = service.SearchAsync(queryParams).GetAwaiter().GetResult();

            var itemsCountValid = result.Items.Count <= queryParams.PageSize;
            var pageValid = result.Page == queryParams.Page;
            var totalCountValid = result.TotalCount >= result.Items.Count;

            return (itemsCountValid && pageValid && totalCountValid)
                .Label($"Items.Count={result.Items.Count} <= PageSize={queryParams.PageSize}: {itemsCountValid}, " +
                       $"Page={result.Page} == Requested={queryParams.Page}: {pageValid}, " +
                       $"TotalCount={result.TotalCount} >= Items.Count={result.Items.Count}: {totalCountValid}");
        });
    }

    #endregion

    #region Property 3: Audit log search filter correctness

    /// <summary>
    /// Property: For any non-empty search term, every returned entry contains the term
    /// (case-insensitive) in at least one of: UserDisplayName, EntityName, Description, or EntityId.
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property SearchFilter_AllReturnedEntries_ContainSearchTerm()
    {
        var searchTermGen = Gen.Elements("Alpha", "Beta", "Gamma", "Delta");

        return Prop.ForAll(Arb.From(searchTermGen), (string searchTerm) =>
        {
            using var context = CreateInMemoryContext();
            SeedEntriesWithSearchableText(context, searchTerm, matchingCount: 4, nonMatchingCount: 5);

            var service = CreateService(context);
            var queryParams = new AuditLogQueryParams
            {
                Page = 0,
                PageSize = 50,
                SearchTerm = searchTerm
            };

            var result = service.SearchAsync(queryParams).GetAwaiter().GetResult();

            var allMatch = result.Items.All(entry =>
                entry.UserDisplayName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                entry.EntityName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                entry.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                entry.EntityId.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));

            var hasResults = result.Items.Count > 0;

            return (allMatch && hasResults)
                .Label($"SearchTerm='{searchTerm}', ResultCount={result.Items.Count}, AllMatch={allMatch}");
        });
    }

    #endregion

    #region Property 4: Audit log lookup round-trip

    /// <summary>
    /// Property: For any existing audit log entry, GetByIdAsync returns a DTO with all fields
    /// matching the persisted entity.
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property LookupRoundTrip_ReturnsMatchingDto()
    {
        var actionTypeGen = Gen.Elements(Enum.GetValues<AuditActionType>());
        var entityTypeGen = Gen.Elements(Enum.GetValues<AuditEntityType>());
        var displayNameGen = Gen.Elements("Alice", "Bob", "Charlie", "Diana");
        var entityNameGen = Gen.Elements("UserEntity", "RoleEntity", "SettingsEntity");
        var descriptionGen = Gen.Elements("Created item", "Updated item", "Deleted item");

        var entryGen = from actionType in actionTypeGen
                       from entityType in entityTypeGen
                       from displayName in displayNameGen
                       from entityName in entityNameGen
                       from description in descriptionGen
                       select new AuditLogEntry
                       {
                           Id = Guid.NewGuid(),
                           UserId = "user-test-1",
                           UserDisplayName = displayName,
                           ActionType = actionType,
                           EntityType = entityType,
                           EntityId = "entity-test-1",
                           EntityName = entityName,
                           Description = description,
                           OldValues = "{\"name\":\"old\"}",
                           NewValues = "{\"name\":\"new\"}",
                           IpAddress = "10.20.30.40",
                           Timestamp = DateTime.UtcNow
                       };

        return Prop.ForAll(Arb.From(entryGen), (AuditLogEntry entry) =>
        {
            using var context = CreateInMemoryContext();
            context.AuditLogEntries.Add(entry);
            context.SaveChanges();

            var service = CreateService(context);
            var dto = service.GetByIdAsync(entry.Id).GetAwaiter().GetResult();

            var idMatch = dto.Id == entry.Id;
            var userIdMatch = dto.UserId == entry.UserId;
            var displayNameMatch = dto.UserDisplayName == entry.UserDisplayName;
            var actionTypeMatch = dto.ActionType == entry.ActionType;
            var entityTypeMatch = dto.EntityType == entry.EntityType;
            var entityIdMatch = dto.EntityId == entry.EntityId;
            var entityNameMatch = dto.EntityName == entry.EntityName;
            var descriptionMatch = dto.Description == entry.Description;
            var oldValuesMatch = dto.OldValues == entry.OldValues;
            var newValuesMatch = dto.NewValues == entry.NewValues;
            var ipAddressMatch = dto.IpAddress == entry.IpAddress;
            var timestampMatch = dto.Timestamp == entry.Timestamp;

            var allMatch = idMatch && userIdMatch && displayNameMatch &&
                           actionTypeMatch && entityTypeMatch && entityIdMatch &&
                           entityNameMatch && descriptionMatch && oldValuesMatch &&
                           newValuesMatch && ipAddressMatch && timestampMatch;

            return allMatch
                .Label($"AllFieldsMatch={allMatch}, Id={idMatch}, UserId={userIdMatch}, " +
                       $"DisplayName={displayNameMatch}, ActionType={actionTypeMatch}, " +
                       $"EntityType={entityTypeMatch}, EntityId={entityIdMatch}, " +
                       $"EntityName={entityNameMatch}, Description={descriptionMatch}, " +
                       $"OldValues={oldValuesMatch}, NewValues={newValuesMatch}, " +
                       $"IpAddress={ipAddressMatch}, Timestamp={timestampMatch}");
        });
    }

    #endregion

    #region Property 5: Audit log export row cap

    /// <summary>
    /// Property: For any AuditLogQueryParams, GetForExportAsync returns at most
    /// ExportDefaults.MaxExportRows entries.
    /// **Validates: Requirements 2.5**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ExportRowCap_NeverExceedsMaxExportRows()
    {
        var seedCountGen = Gen.Choose(1, 20);

        return Prop.ForAll(Arb.From(seedCountGen), (int seedCount) =>
        {
            using var context = CreateInMemoryContext();
            SeedEntries(context, seedCount);

            var service = CreateService(context);
            var queryParams = new AuditLogQueryParams
            {
                Page = 0,
                PageSize = 50
            };

            var result = service.GetForExportAsync(queryParams).GetAwaiter().GetResult();

            var withinCap = result.Count <= ExportDefaults.MaxExportRows;
            var matchesSeedCount = result.Count == seedCount;

            return (withinCap && matchesSeedCount)
                .Label($"Count={result.Count} <= MaxExportRows={ExportDefaults.MaxExportRows}: {withinCap}, " +
                       $"Count={result.Count} == SeededCount={seedCount}: {matchesSeedCount}");
        });
    }

    #endregion
}
