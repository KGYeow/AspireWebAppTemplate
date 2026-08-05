// Feature: audit-log-old-new-values, Property 1: LogAsync Field Mapping Correctness
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services;
using AspireWebAppTemplate.Application.Contracts.AuditLog;
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

namespace AspireWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Property-based tests verifying that LogAsync(AuditLogRequest) correctly maps
/// all request properties to the persisted AuditLogEntry entity fields.
/// </summary>
/// <remarks>
/// **Validates: Requirements 1.4, 2.5, 8.1**
/// </remarks>
public class AuditLogRequestFieldMappingTests
{
    /// <summary>
    /// Creates a SQLite in-memory ApplicationDbContext for testing.
    /// Foreign key enforcement is disabled to allow persisting AuditLogEntry
    /// without requiring a corresponding ApplicationUser record.
    /// </summary>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Disable FK enforcement so we can test without seeding ApplicationUser records.
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
    /// Generates an AuditLogRequest with random field values for property testing.
    /// </summary>
    private static Gen<AuditLogRequest> AuditLogRequestGen()
    {
        var actionTypeGen = Gen.Elements(Enum.GetValues<AuditActionType>());
        var entityTypeGen = Gen.Elements(Enum.GetValues<AuditEntityType>());
        var stringGen = Gen.Elements("alpha", "beta", "gamma", "delta", "epsilon",
            "user-123", "entity-456", "A test description", "Some Name", "192.168.1.1");
        var nullableStringGen = Gen.Frequency(
            (1, Gen.Constant((string?)null)),
            (3, stringGen.Select(s => (string?)s)));

        return actionTypeGen.SelectMany(actionType =>
            entityTypeGen.SelectMany(entityType =>
            nullableStringGen.SelectMany(userId =>
            stringGen.SelectMany(entityId =>
            stringGen.SelectMany(entityName =>
            stringGen.SelectMany(description =>
            nullableStringGen.SelectMany(oldValues =>
            nullableStringGen.SelectMany(newValues =>
            nullableStringGen.Select(ipAddress => new AuditLogRequest
            {
                UserId = userId,
                ActionType = actionType,
                EntityType = entityType,
                EntityId = entityId,
                EntityName = entityName,
                Description = description,
                OldValues = oldValues,
                NewValues = newValues,
                IpAddress = ipAddress
            })))))))));
    }

    /// <summary>
    /// Property: For any valid combination of audit parameters (userId, actionType, entityType,
    /// entityId, entityName, description, oldValues, newValues, ipAddress), calling
    /// LogAsync(AuditLogRequest) with those values packed into an AuditLogRequest SHALL produce
    /// an AuditLogEntry where each field on the entity matches the corresponding property on the request.
    /// **Validates: Requirements 1.4, 2.5, 8.1**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property LogAsync_MapsAllRequestFieldsToEntityCorrectly()
    {
        return Prop.ForAll(
            Arb.From(AuditLogRequestGen()),
            (AuditLogRequest request) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    // Set up mocked UserManager that returns a known user with DisplayName
                    var knownDisplayName = "Test Display Name";
                    var store = new Mock<IUserStore<ApplicationUser>>();
                    var userManager = new Mock<UserManager<ApplicationUser>>(
                        store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

                    if (request.UserId is not null)
                    {
                        userManager.Setup(um => um.FindByIdAsync(request.UserId))
                            .ReturnsAsync(new ApplicationUser
                            {
                                Id = request.UserId,
                                DisplayName = knownDisplayName
                            });
                    }

                    var logger = new Mock<ILogger<AuditLogService>>();
                    var configuration = new Mock<IConfiguration>();

                    var service = new AuditLogService(
                        dbContext, userManager.Object, logger.Object, configuration.Object);

                    // Act
                    service.LogAsync(request).GetAwaiter().GetResult();

                    // Assert - query the persisted entry
                    dbContext.ChangeTracker.Clear();
                    var entry = dbContext.AuditLogEntries.Single();

                    // Verify field mappings
                    var userIdMatch = entry.UserId == (request.UserId ?? string.Empty);
                    var actionTypeMatch = entry.ActionType == request.ActionType;
                    var entityTypeMatch = entry.EntityType == request.EntityType;
                    var entityIdMatch = entry.EntityId == request.EntityId;
                    var entityNameMatch = entry.EntityName == request.EntityName;
                    var descriptionMatch = entry.Description == request.Description;
                    var oldValuesMatch = entry.OldValues == request.OldValues;
                    var newValuesMatch = entry.NewValues == request.NewValues;
                    var ipAddressMatch = entry.IpAddress == request.IpAddress;

                    // UserDisplayName should be resolved:
                    // - If UserId is not null and user found → knownDisplayName
                    // - If UserId is null → empty string
                    var expectedDisplayName = request.UserId is not null ? knownDisplayName : string.Empty;
                    var displayNameMatch = entry.UserDisplayName == expectedDisplayName;

                    var allMatch = userIdMatch && actionTypeMatch && entityTypeMatch &&
                                   entityIdMatch && entityNameMatch && descriptionMatch &&
                                   oldValuesMatch && newValuesMatch && ipAddressMatch &&
                                   displayNameMatch;

                    return allMatch.Label(
                        $"Field mapping failed. UserId={userIdMatch}, ActionType={actionTypeMatch}, " +
                        $"EntityType={entityTypeMatch}, EntityId={entityIdMatch}, EntityName={entityNameMatch}, " +
                        $"Description={descriptionMatch}, OldValues={oldValuesMatch}, NewValues={newValuesMatch}, " +
                        $"IpAddress={ipAddressMatch}, DisplayName={displayNameMatch} " +
                        $"(expected='{expectedDisplayName}', actual='{entry.UserDisplayName}')");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
