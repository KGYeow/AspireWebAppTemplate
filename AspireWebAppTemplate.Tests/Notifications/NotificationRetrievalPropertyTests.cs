// Feature: notification-system, Property 3: Filtering returns only notifications matching all specified criteria
// Feature: notification-system, Property 4: Pagination returns at most pageSize items
// Feature: notification-system, Property 5: Unread count matches actual count of unread notifications
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities.Template;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services;
using AspireWebAppTemplate.Infrastructure.Services.Template.Notifications;
using AspireWebAppTemplate.Application.Features.Template.Notifications;
using AspireWebAppTemplate.Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Property-based tests verifying notification retrieval behaviors including filtering,
/// pagination constraints, and result set correctness.
/// </summary>
/// <remarks>
/// **Validates: Requirements 2.2, 2.3, 2.4, 3.1**
/// </remarks>
public class NotificationRetrievalPropertyTests
{
    /// <summary>
    /// Creates a SQLite in-memory ApplicationDbContext for testing.
    /// Foreign key enforcement is disabled to allow seeding notifications
    /// without requiring a full ApplicationUser entity graph.
    /// </summary>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Disable FK enforcement so we can seed notifications without a full user record.
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = OFF;";
        cmd.ExecuteNonQuery();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();
        return (dbContext, connection);
    }

    /// <summary>
    /// Property: For any set of notifications with mixed categories and IsRead states,
    /// when a category filter and/or read status filter is applied, all returned notifications
    /// SHALL match every specified filter criterion, and no notification matching the criteria
    /// SHALL be excluded.
    /// **Validates: Requirements 2.2, 2.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property Filtering_ReturnsOnlyNotificationsMatchingAllSpecifiedCriteria()
    {
        var categoryGen = Gen.Elements(Enum.GetValues<NotificationCategory>());
        var isReadGen = Gen.Elements(true, false);

        return Prop.ForAll(
            Arb.From(categoryGen),
            Arb.From(isReadGen),
            (NotificationCategory filterCategory, bool filterIsRead) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    var userId = "test-user-filter";

                    // Seed notifications with all combinations of categories and IsRead states
                    var allCategories = Enum.GetValues<NotificationCategory>();
                    var seededNotifications = new List<Notification>();
                    var baseTime = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
                    var counter = 0;

                    foreach (var category in allCategories)
                    {
                        foreach (var isRead in new[] { true, false })
                        {
                            var notification = new Notification
                            {
                                Id = Guid.NewGuid(),
                                UserId = userId,
                                Category = category,
                                Title = $"Test Notification {counter}",
                                Message = $"Message for category {category}, isRead {isRead}",
                                IsRead = isRead,
                                CreatedAtUtc = baseTime.AddMinutes(counter),
                                ReadAtUtc = isRead ? baseTime.AddMinutes(counter + 1) : null
                            };
                            seededNotifications.Add(notification);
                            counter++;
                        }
                    }

                    dbContext.Notifications.AddRange(seededNotifications);
                    dbContext.SaveChanges();

                    // Create service and apply both filters
                    var service = new NotificationService(dbContext, NullLogger<NotificationService>.Instance, null!);
                    var queryParams = new NotificationQueryParams
                    {
                        Page = 1,
                        PageSize = 100,
                        Category = filterCategory,
                        IsRead = filterIsRead
                    };

                    var result = service.GetNotificationsAsync(userId, queryParams).GetAwaiter().GetResult();

                    // Verify: all returned items match both filter criteria
                    var allMatchCategory = result.Items.All(n => n.Category == filterCategory);
                    var allMatchIsRead = result.Items.All(n => n.IsRead == filterIsRead);

                    // Verify: no items matching the criteria are excluded (compare counts)
                    var expectedCount = seededNotifications.Count(n =>
                        n.Category == filterCategory && n.IsRead == filterIsRead);
                    var actualCount = result.Items.Count;
                    var noneExcluded = actualCount == expectedCount;

                    var allPass = allMatchCategory && allMatchIsRead && noneExcluded;

                    return allPass.Label(
                        $"Filter(Category={filterCategory}, IsRead={filterIsRead}): " +
                        $"AllMatchCategory={allMatchCategory}, AllMatchIsRead={allMatchIsRead}, " +
                        $"NoneExcluded={noneExcluded} (expected={expectedCount}, actual={actualCount})");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }

    /// <summary>
    /// Property: For any positive pageSize value (1–100) and any total notification count,
    /// the returned page SHALL contain at most pageSize items, and the total count SHALL
    /// reflect the full filtered result set size.
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property Pagination_ReturnsAtMostPageSizeItems()
    {
        // Generator for pageSize (1-100)
        var pageSizeGen = Gen.Choose(1, 100);

        // Generator for total notification count to seed (5-30)
        var notificationCountGen = Gen.Choose(5, 30);

        // Generator for page number (1-5)
        var pageGen = Gen.Choose(1, 5);

        return Prop.ForAll(
            Arb.From(pageSizeGen),
            Arb.From(notificationCountGen),
            Arb.From(pageGen),
            (int pageSize, int totalNotifications, int page) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    var userId = "test-user-pagination";

                    // Seed a valid ApplicationUser record
                    dbContext.Users.Add(new ApplicationUser
                    {
                        Id = userId,
                        UserName = "pagination-test-user",
                        NormalizedUserName = "PAGINATION-TEST-USER",
                        Email = "pagination@test.com",
                        NormalizedEmail = "PAGINATION@TEST.COM",
                        SecurityStamp = Guid.NewGuid().ToString()
                    });
                    dbContext.SaveChanges();

                    // Seed a random number of notifications directly into DB
                    var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    var random = new Random(pageSize + totalNotifications + page);

                    for (int i = 0; i < totalNotifications; i++)
                    {
                        var notification = new Notification
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId,
                            Category = Enum.GetValues<NotificationCategory>()[random.Next(3)],
                            Title = $"Pagination Test {i}",
                            Message = $"Message {i}",
                            IsRead = random.Next(2) == 0,
                            CreatedAtUtc = baseTime.AddMinutes(i)
                        };
                        dbContext.Notifications.Add(notification);
                    }
                    dbContext.SaveChanges();

                    // Act: call GetNotificationsAsync with the generated page/pageSize
                    var service = new NotificationService(dbContext, NullLogger<NotificationService>.Instance, null!);
                    var queryParams = new NotificationQueryParams
                    {
                        Page = page,
                        PageSize = pageSize
                    };

                    var result = service.GetNotificationsAsync(userId, queryParams).GetAwaiter().GetResult();

                    // Assert: Items.Count ≤ pageSize
                    var countWithinPageSize = result.Items.Count <= pageSize;

                    // Assert: TotalCount equals the total number of seeded notifications
                    var totalCountCorrect = result.TotalCount == totalNotifications;

                    // Assert: PagedResult.Page and PageSize match the query
                    var pageMatches = result.Page == page;
                    var pageSizeMatches = result.PageSize == pageSize;

                    var allPass = countWithinPageSize && totalCountCorrect && pageMatches && pageSizeMatches;

                    return allPass.Label(
                        $"PageSize={pageSize}, Page={page}, TotalSeeded={totalNotifications}: " +
                        $"CountWithinPageSize={countWithinPageSize} (Items.Count={result.Items.Count}), " +
                        $"TotalCountCorrect={totalCountCorrect} (TotalCount={result.TotalCount}), " +
                        $"PageMatches={pageMatches} (Result.Page={result.Page}), " +
                        $"PageSizeMatches={pageSizeMatches} (Result.PageSize={result.PageSize})");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }

    /// <summary>
    /// Property: For any set of notifications belonging to a user with mixed IsRead states,
    /// the unread count query SHALL return a value equal to the count of notifications where
    /// IsRead is false.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property UnreadCount_MatchesActualCountOfUnreadNotifications()
    {
        // Generator for total notification count (1-30)
        var totalCountGen = Gen.Choose(1, 30);

        // Generator for the number of unread notifications (0 to totalCount)
        // We generate a pair and ensure unreadCount <= totalCount
        return Prop.ForAll(
            Arb.From(totalCountGen),
            (int totalCount) =>
            {
                // Generate a random unread count between 0 and totalCount
                var unreadCountGen = Gen.Choose(0, totalCount);

                return Prop.ForAll(
                    Arb.From(unreadCountGen),
                    (int unreadCount) =>
                    {
                        var (dbContext, connection) = CreateDbContext();
                        try
                        {
                            var userId = "test-user-unread-count";
                            var baseTime = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);

                            // Seed notifications with the specified number of unread (IsRead=false)
                            // and the remainder as read (IsRead=true)
                            for (int i = 0; i < totalCount; i++)
                            {
                                var isRead = i >= unreadCount; // first 'unreadCount' are unread
                                var notification = new Notification
                                {
                                    Id = Guid.NewGuid(),
                                    UserId = userId,
                                    Category = NotificationCategory.System,
                                    Title = $"Notification {i}",
                                    Message = $"Message {i}",
                                    IsRead = isRead,
                                    CreatedAtUtc = baseTime.AddMinutes(i),
                                    ReadAtUtc = isRead ? baseTime.AddMinutes(i + 1) : null
                                };
                                dbContext.Notifications.Add(notification);
                            }
                            dbContext.SaveChanges();

                            // Act: call GetUnreadCountAsync
                            var service = new NotificationService(dbContext, NullLogger<NotificationService>.Instance, null!);
                            var result = service.GetUnreadCountAsync(userId).GetAwaiter().GetResult();

                            // Assert: returned count equals the actual number of unread notifications
                            var countMatches = result == unreadCount;

                            return countMatches.Label(
                                $"Total={totalCount}, ExpectedUnread={unreadCount}, ActualUnread={result}");
                        }
                        finally
                        {
                            dbContext.Dispose();
                            connection.Dispose();
                        }
                    });
            });
    }
}
