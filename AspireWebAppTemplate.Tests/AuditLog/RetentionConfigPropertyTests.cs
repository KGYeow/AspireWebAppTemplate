// Feature: audit-log, Property 10: Retention configuration validation
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities.Template;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services;
using AspireWebAppTemplate.Infrastructure.Services.Template.AuditLog;
using AspireWebAppTemplate.Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Gen = FsCheck.Fluent.Gen;
using Property = FsCheck.Property;

namespace AspireWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Property-based tests verifying that the AuditLog:RetentionDays configuration value is
/// validated correctly: valid integers within 1–3650 are used as-is; missing, non-numeric,
/// or out-of-range values fall back to 365.
/// </summary>
/// <remarks>
/// **Validates: Requirements 10.1, 10.2**
/// </remarks>
public class RetentionConfigPropertyTests
{
    private const int DefaultRetentionDays = 365;
    private const int MinRetentionDays = 1;
    private const int MaxRetentionDays = 3650;

    /// <summary>
    /// Creates a SQLite in-memory ApplicationDbContext for testing.
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
    /// Creates a mock IConfiguration that returns the specified value for "AuditLog:RetentionDays".
    /// </summary>
    private static IConfiguration CreateConfiguration(string? retentionDaysValue)
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["AuditLog:RetentionDays"]).Returns(retentionDaysValue!);
        return configMock.Object;
    }

    /// <summary>
    /// Creates an AuditLogService with the given configuration and database context.
    /// </summary>
    private static AuditLogService CreateService(ApplicationDbContext dbContext, IConfiguration configuration)
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var loggerMock = new Mock<ILogger<AuditLogService>>();

        return new AuditLogService(dbContext, userManagerMock.Object, loggerMock.Object, configuration);
    }

    /// <summary>
    /// Seeds an audit log entry with a specific timestamp into the database.
    /// </summary>
    private static void SeedEntry(ApplicationDbContext dbContext, DateTime timestamp)
    {
        dbContext.AuditLogEntries.Add(new AuditLogEntry
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
        });
        dbContext.SaveChanges();
    }

    /// <summary>
    /// **Validates: Requirements 10.1, 10.2**
    /// For any valid integer in range 1–3650 configured as AuditLog:RetentionDays,
    /// the system uses that value as the retention period (verified via purge behavior).
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ValidRetentionDays_UsedAsIs()
    {
        var validDaysGen = Gen.Choose(MinRetentionDays, MaxRetentionDays);

        return Prop.ForAll(Arb.From(validDaysGen), (int retentionDays) =>
        {
            var (dbContext, connection) = CreateDbContext();
            try
            {
                var configuration = CreateConfiguration(retentionDays.ToString());
                var service = CreateService(dbContext, configuration);

                // Seed an entry that is older than the configured retention period
                var oldTimestamp = DateTime.UtcNow - TimeSpan.FromDays(retentionDays + 1);
                SeedEntry(dbContext, oldTimestamp);

                // Seed an entry that is within the retention period
                var recentTimestamp = DateTime.UtcNow - TimeSpan.FromDays(retentionDays - 1);
                SeedEntry(dbContext, recentTimestamp);

                // Act: purge should use the configured retention days
                var purgedCount = service.PurgeOldEntriesAsync().GetAwaiter().GetResult();

                // The old entry should be purged, the recent one preserved
                var remainingCount = dbContext.AuditLogEntries.Count();

                return (purgedCount == 1 && remainingCount == 1)
                    .Label($"RetentionDays={retentionDays}, Purged={purgedCount}, Remaining={remainingCount}");
            }
            finally
            {
                dbContext.Dispose();
                connection.Dispose();
            }
        });
    }

    /// <summary>
    /// **Validates: Requirements 10.1, 10.2**
    /// For missing configuration (null/empty/whitespace), the system falls back to 365 days.
    /// </summary>
    [Property(MaxTest = 1)]
    public Property MissingConfig_FallsBackTo365()
    {
        var missingValueGen = Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Constant<string?>(""),
            Gen.Constant<string?>("   ")
        );

        return Prop.ForAll(Arb.From(missingValueGen), (string? configValue) =>
        {
            var (dbContext, connection) = CreateDbContext();
            try
            {
                var configuration = CreateConfiguration(configValue);
                var service = CreateService(dbContext, configuration);

                // Seed an entry older than 365 days (should be purged with default)
                var oldTimestamp = DateTime.UtcNow - TimeSpan.FromDays(DefaultRetentionDays + 1);
                SeedEntry(dbContext, oldTimestamp);

                // Seed an entry within 365 days (should be preserved)
                var recentTimestamp = DateTime.UtcNow - TimeSpan.FromDays(DefaultRetentionDays - 1);
                SeedEntry(dbContext, recentTimestamp);

                // Act
                var purgedCount = service.PurgeOldEntriesAsync().GetAwaiter().GetResult();
                var remainingCount = dbContext.AuditLogEntries.Count();

                return (purgedCount == 1 && remainingCount == 1)
                    .Label($"ConfigValue='{configValue ?? "null"}', Purged={purgedCount}, Remaining={remainingCount}");
            }
            finally
            {
                dbContext.Dispose();
                connection.Dispose();
            }
        });
    }

    /// <summary>
    /// **Validates: Requirements 10.1, 10.2**
    /// For non-numeric configuration values, the system falls back to 365 days.
    /// </summary>
    [Property(MaxTest = 1)]
    public Property NonNumericConfig_FallsBackTo365()
    {
        var nonNumericGen = Gen.OneOf(
            Gen.Constant("abc"),
            Gen.Constant("twelve"),
            Gen.Constant("3.14"),
            Gen.Constant("100days"),
            Gen.Constant("--5"),
            Gen.Constant("NaN")
        );

        return Prop.ForAll(Arb.From(nonNumericGen), (string configValue) =>
        {
            var (dbContext, connection) = CreateDbContext();
            try
            {
                var configuration = CreateConfiguration(configValue);
                var service = CreateService(dbContext, configuration);

                // Seed an entry older than 365 days (should be purged with default)
                var oldTimestamp = DateTime.UtcNow - TimeSpan.FromDays(DefaultRetentionDays + 1);
                SeedEntry(dbContext, oldTimestamp);

                // Seed an entry within 365 days (should be preserved)
                var recentTimestamp = DateTime.UtcNow - TimeSpan.FromDays(DefaultRetentionDays - 1);
                SeedEntry(dbContext, recentTimestamp);

                // Act
                var purgedCount = service.PurgeOldEntriesAsync().GetAwaiter().GetResult();
                var remainingCount = dbContext.AuditLogEntries.Count();

                return (purgedCount == 1 && remainingCount == 1)
                    .Label($"ConfigValue='{configValue}', Purged={purgedCount}, Remaining={remainingCount}");
            }
            finally
            {
                dbContext.Dispose();
                connection.Dispose();
            }
        });
    }

    /// <summary>
    /// **Validates: Requirements 10.1, 10.2**
    /// For out-of-range configuration values (below 1 or above 3650), the system falls back to 365 days.
    /// </summary>
    [Property(MaxTest = 1)]
    public Property OutOfRangeConfig_FallsBackTo365()
    {
        var outOfRangeGen = Gen.OneOf(
            Gen.Choose(-1000, 0).Select(i => i.ToString()),
            Gen.Choose(3651, 10000).Select(i => i.ToString()),
            Gen.Constant(int.MinValue.ToString()),
            Gen.Constant(int.MaxValue.ToString())
        );

        return Prop.ForAll(Arb.From(outOfRangeGen), (string configValue) =>
        {
            var (dbContext, connection) = CreateDbContext();
            try
            {
                var configuration = CreateConfiguration(configValue);
                var service = CreateService(dbContext, configuration);

                // Seed an entry older than 365 days (should be purged with default)
                var oldTimestamp = DateTime.UtcNow - TimeSpan.FromDays(DefaultRetentionDays + 1);
                SeedEntry(dbContext, oldTimestamp);

                // Seed an entry within 365 days (should be preserved)
                var recentTimestamp = DateTime.UtcNow - TimeSpan.FromDays(DefaultRetentionDays - 1);
                SeedEntry(dbContext, recentTimestamp);

                // Act
                var purgedCount = service.PurgeOldEntriesAsync().GetAwaiter().GetResult();
                var remainingCount = dbContext.AuditLogEntries.Count();

                return (purgedCount == 1 && remainingCount == 1)
                    .Label($"ConfigValue='{configValue}', Purged={purgedCount}, Remaining={remainingCount}");
            }
            finally
            {
                dbContext.Dispose();
                connection.Dispose();
            }
        });
    }
}
