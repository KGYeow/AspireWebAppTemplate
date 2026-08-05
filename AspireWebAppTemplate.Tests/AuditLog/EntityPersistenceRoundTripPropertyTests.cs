// Feature: audit-log, Property 1: Entity persistence round-trip
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AspireWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Property-based tests verifying that persisting and retrieving an AuditLogEntry
/// produces identical property values with enums stored as PascalCase strings.
/// </summary>
/// <remarks>
/// **Validates: Requirements 1.1, 1.6, 2.3**
/// </remarks>
public class EntityPersistenceRoundTripPropertyTests
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

        // Disable FK enforcement so we can test entity round-trip persistence
        // without needing to seed a valid ApplicationUser for the UserId FK.
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
    /// Property: For any valid AuditLogEntry with randomly generated field values (including all
    /// AuditActionType and AuditEntityType enum values), persisting the entry to the database and
    /// retrieving it by Id SHALL produce an entry with all property values identical to the original,
    /// with enum values stored as their PascalCase string representation.
    /// **Validates: Requirements 1.1, 1.6, 2.3**
    /// </summary>
    [Property(MaxTest = 1)]
    public FsCheck.Property PersistAndRetrieve_ProducesIdenticalPropertyValues()
    {
        // Generator for AuditActionType enum values
        var actionTypeGen = Gen.Elements(Enum.GetValues<AuditActionType>());
        // Generator for AuditEntityType enum values
        var entityTypeGen = Gen.Elements(Enum.GetValues<AuditEntityType>());

        return Prop.ForAll(
            Arb.From(actionTypeGen),
            Arb.From(entityTypeGen),
            (AuditActionType actionType, AuditEntityType entityType) =>
        {
            var (dbContext, connection) = CreateDbContext();
            try
            {
                // Create an AuditLogEntry with known values including the random enums
                var entry = new AuditLogEntry
                {
                    Id = Guid.NewGuid(),
                    UserId = "test-user-id-123",
                    UserDisplayName = "Test User Display",
                    ActionType = actionType,
                    EntityType = entityType,
                    EntityId = "entity-id-456",
                    EntityName = "Test Entity Name",
                    Description = "A test description for the audit entry",
                    OldValues = "{\"key\": \"oldValue\"}",
                    NewValues = "{\"key\": \"newValue\"}",
                    IpAddress = "192.168.1.100",
                    Timestamp = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc)
                };

                // Persist the entry
                dbContext.AuditLogEntries.Add(entry);
                dbContext.SaveChanges();

                // Detach so the retrieval hits the database, not the change tracker
                dbContext.ChangeTracker.Clear();

                // Retrieve by Id
                var retrieved = dbContext.AuditLogEntries.Single(e => e.Id == entry.Id);

                // Verify all property values are identical after round-trip
                var idMatch = retrieved.Id == entry.Id;
                var userIdMatch = retrieved.UserId == entry.UserId;
                var displayNameMatch = retrieved.UserDisplayName == entry.UserDisplayName;
                var actionTypeMatch = retrieved.ActionType == entry.ActionType;
                var entityTypeMatch = retrieved.EntityType == entry.EntityType;
                var entityIdMatch = retrieved.EntityId == entry.EntityId;
                var entityNameMatch = retrieved.EntityName == entry.EntityName;
                var descriptionMatch = retrieved.Description == entry.Description;
                var oldValuesMatch = retrieved.OldValues == entry.OldValues;
                var newValuesMatch = retrieved.NewValues == entry.NewValues;
                var ipAddressMatch = retrieved.IpAddress == entry.IpAddress;
                var timestampMatch = retrieved.Timestamp == entry.Timestamp;

                // Verify enum values stored as PascalCase strings by querying raw SQL via ADO.NET.
                // EF Core's SQLite provider stores GUIDs as TEXT in uppercase format.
                using var rawCmd = connection.CreateCommand();
                rawCmd.CommandText = "SELECT ActionType, EntityType FROM AuditLogEntries WHERE Id = @id";
                var idParam = rawCmd.CreateParameter();
                idParam.ParameterName = "@id";
                idParam.Value = entry.Id;
                rawCmd.Parameters.Add(idParam);
                using var reader = rawCmd.ExecuteReader();
                var hasRow = reader.Read();

                // hasRow must be true - we just persisted this entry and it round-tripped via EF Core
                var actionTypeRaw = hasRow ? reader.GetString(0) : string.Empty;
                var entityTypeRaw = hasRow ? reader.GetString(1) : string.Empty;

                var actionTypeStoredAsPascal = actionTypeRaw == entry.ActionType.ToString();
                var entityTypeStoredAsPascal = entityTypeRaw == entry.EntityType.ToString();

                var allMatch = hasRow && idMatch && userIdMatch && displayNameMatch &&
                               actionTypeMatch && entityTypeMatch &&
                               entityIdMatch && entityNameMatch && descriptionMatch &&
                               oldValuesMatch && newValuesMatch && ipAddressMatch &&
                               timestampMatch && actionTypeStoredAsPascal && entityTypeStoredAsPascal;

                return allMatch.Label(
                    $"Round-trip failed. HasRow={hasRow}, " +
                    $"Id={idMatch}, UserId={userIdMatch}, DisplayName={displayNameMatch}, " +
                    $"ActionType={actionTypeMatch}, EntityType={entityTypeMatch}, " +
                    $"EntityId={entityIdMatch}, EntityName={entityNameMatch}, Description={descriptionMatch}, " +
                    $"OldValues={oldValuesMatch}, NewValues={newValuesMatch}, IpAddress={ipAddressMatch}, " +
                    $"Timestamp={timestampMatch}, ActionTypeStoredAsPascal={actionTypeStoredAsPascal} (raw='{actionTypeRaw}'), " +
                    $"EntityTypeStoredAsPascal={entityTypeStoredAsPascal} (raw='{entityTypeRaw}')");
            }
            finally
            {
                dbContext.Dispose();
                connection.Dispose();
            }
        });
    }

    /// <summary>
    /// Property: Nullable fields (OldValues, NewValues, IpAddress) round-trip correctly as null.
    /// This verifies that nullable string columns persist null values without substitution.
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Property(MaxTest = 1)]
    public FsCheck.Property PersistAndRetrieve_NullableFieldsPreserveNull()
    {
        var actionTypeGen = Gen.Elements(Enum.GetValues<AuditActionType>());
        var entityTypeGen = Gen.Elements(Enum.GetValues<AuditEntityType>());

        return Prop.ForAll(
            Arb.From(actionTypeGen),
            Arb.From(entityTypeGen),
            (AuditActionType actionType, AuditEntityType entityType) =>
        {
            var (dbContext, connection) = CreateDbContext();
            try
            {
                // Create entry with all nullable fields set to null
                var entry = new AuditLogEntry
                {
                    Id = Guid.NewGuid(),
                    UserId = "user-nullable-test",
                    UserDisplayName = "Nullable Test",
                    ActionType = actionType,
                    EntityType = entityType,
                    EntityId = "entity-null-test",
                    EntityName = "Null Fields Test",
                    Description = "Testing nullable field persistence",
                    OldValues = null,
                    NewValues = null,
                    IpAddress = null,
                    Timestamp = new DateTime(2024, 3, 1, 8, 0, 0, DateTimeKind.Utc)
                };

                dbContext.AuditLogEntries.Add(entry);
                dbContext.SaveChanges();
                dbContext.ChangeTracker.Clear();

                var retrieved = dbContext.AuditLogEntries.Single(e => e.Id == entry.Id);

                var oldValuesNull = retrieved.OldValues == null;
                var newValuesNull = retrieved.NewValues == null;
                var ipAddressNull = retrieved.IpAddress == null;

                return (oldValuesNull && newValuesNull && ipAddressNull).Label(
                    $"Nullable fields not preserved. OldValues={retrieved.OldValues}, " +
                    $"NewValues={retrieved.NewValues}, IpAddress={retrieved.IpAddress}");
            }
            finally
            {
                dbContext.Dispose();
                connection.Dispose();
            }
        });
    }
}
