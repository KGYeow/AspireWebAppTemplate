// Feature: resource-based-authorization, Property 10: Audit entry accurately reflects permission changes
using System.Text.Json;
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.AuditLog;
using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services.Permissions;
using AspireWebAppTemplate.Infrastructure.Utilities;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Gen = FsCheck.Fluent.Gen;
using Property = FsCheck.Property;

namespace AspireWebAppTemplate.Tests.ResourceBasedAuthorization;

/// <summary>
/// Property-based tests verifying that a successful permission update on a non-Admin role via
/// <see cref="PermissionService.UpdateRolePermissionsAsync(string, System.Collections.Generic.List{string})"/>
/// produces an audit entry that accurately reflects the before/after permission sets and the
/// affected role.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Property 10 — Audit entry accurately reflects permission changes.</strong> For any
/// successful full-replacement update on a non-Admin role, the single
/// <see cref="AuditLogRequest"/> handed to <see cref="IAuditLogService.LogAsync(AuditLogRequest)"/>
/// carries:
/// <list type="bullet">
///   <item><description><c>OldValues</c> = camelCase JSON <c>{"permissions":[...old keys]}</c>.</description></item>
///   <item><description><c>NewValues</c> = camelCase JSON <c>{"permissions":[...new keys]}</c>.</description></item>
///   <item><description><c>EntityId</c> = the target role ID.</description></item>
///   <item><description><c>EntityName</c> = the role's <see cref="ApplicationRole.DisplayName"/> (falling back to <see cref="Microsoft.AspNetCore.Identity.IdentityRole{T}.Name"/>).</description></item>
///   <item><description><c>ActionType</c> = <see cref="AuditActionType.SettingsChanged"/>.</description></item>
///   <item><description><c>EntityType</c> = <see cref="AuditEntityType.Role"/>.</description></item>
/// </list>
/// </para>
/// <para>
/// **Validates: Requirements 11.1, 11.2**
/// </para>
/// <para>
/// Tests use a real <see cref="ApplicationDbContext"/> over a SQLite in-memory database (foreign-key
/// enforcement ON, matching the shared resource-based authorization test setup) and a real
/// <see cref="PermissionService"/>. The <see cref="IAuditLogService"/> is a Moq double whose
/// <see cref="IAuditLogService.LogAsync(AuditLogRequest)"/> argument is captured for assertion. The
/// Identity <see cref="RoleManager{ApplicationRole}"/> is mocked so
/// <see cref="RoleManager{ApplicationRole}.FindByIdAsync(string)"/> returns a known non-Admin role.
/// The permissions array is compared by <em>set equality</em> (not positional string equality),
/// since the serialized key list follows the caller's list order which is not semantically
/// significant.
/// </para>
/// </remarks>
public class AuditAccuracyTests
{
    #region Test Infrastructure

    /// <summary>
    /// The pool of candidate permission keys used to build the random "old" and "new" grant sets.
    /// All keys are seeded as <see cref="Permission"/> definitions so any subset forms a valid
    /// full-replacement target.
    /// </summary>
    private static readonly string[] KeyPool =
    [
        "Users.Read",
        "Users.Create",
        "Users.Update",
        "Roles.Read",
        "Roles.Manage",
        "AuditLog.Read",
        "AuditLog.Export",
        "EmailTemplates.Read"
    ];

    /// <summary>
    /// Mirrors the serializer configuration used by
    /// <see cref="AuditChangeHelper.Serialize(object?)"/> so the test can deserialize the captured
    /// JSON with identical camelCase handling.
    /// </summary>
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    /// <summary>
    /// The minimal shape of the audit <c>{ "permissions": [...] }</c> payload, used to deserialize
    /// the captured <c>OldValues</c>/<c>NewValues</c> JSON for set-equality comparison.
    /// </summary>
    private sealed class PermissionsPayload
    {
        /// <summary>
        /// The serialized permission key list carried by the audit old/new values object.
        /// </summary>
        public List<string>? Permissions { get; set; }
    }

    /// <summary>
    /// Creates a SQLite in-memory <see cref="ApplicationDbContext"/> with foreign-key enforcement
    /// enabled, matching the shared in-memory setup pattern used across the resource-based
    /// authorization test suite (open connection, <c>PRAGMA foreign_keys = ON</c>,
    /// <c>EnsureCreated</c>, connection kept alive for the database's lifetime).
    /// </summary>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Enable FK enforcement so RolePermission grants require real Role/Permission rows,
        // mirroring production behavior.
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys = ON;";
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
    /// Seeds a <see cref="Permission"/> row for every key in <see cref="KeyPool"/> so any requested
    /// subset is a valid (defined) full-replacement target, then returns the key → generated-ID map.
    /// </summary>
    private static Dictionary<string, int> SeedPermissions(ApplicationDbContext dbContext)
    {
        var permissionIdByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in KeyPool)
        {
            var permission = new Permission
            {
                Key = key,
                DisplayName = key,
                Module = key.Split('.')[0],
                Description = null
            };
            dbContext.Set<Permission>().Add(permission);
            dbContext.SaveChanges();
            permissionIdByKey[key] = permission.Id;
        }

        return permissionIdByKey;
    }

    /// <summary>
    /// Inserts a non-Admin <see cref="ApplicationRole"/> with the supplied identity and seeds its
    /// initial ("old") grant set via <see cref="RolePermission"/> rows.
    /// </summary>
    private static void SeedRoleWithGrants(
        ApplicationDbContext dbContext,
        string roleId,
        string displayName,
        IReadOnlyDictionary<string, int> permissionIdByKey,
        IEnumerable<string> oldKeys)
    {
        var role = new ApplicationRole
        {
            Id = roleId,
            Name = "Editor",
            NormalizedName = "EDITOR",
            DisplayName = displayName
        };
        dbContext.Roles.Add(role);
        dbContext.SaveChanges();

        foreach (var key in oldKeys.Distinct(StringComparer.Ordinal))
        {
            dbContext.Set<RolePermission>().Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionIdByKey[key]
            });
        }

        dbContext.SaveChanges();
    }

    /// <summary>
    /// Constructs a real <see cref="PermissionService"/> whose <see cref="IAuditLogService"/> captures
    /// the <see cref="AuditLogRequest"/> handed to <see cref="IAuditLogService.LogAsync(AuditLogRequest)"/>
    /// into <paramref name="captured"/>, and whose <see cref="RoleManager{ApplicationRole}"/> resolves
    /// <see cref="RoleManager{ApplicationRole}.FindByIdAsync(string)"/> to <paramref name="role"/>.
    /// </summary>
    private static PermissionService CreateService(
        ApplicationDbContext dbContext,
        ApplicationRole role,
        Action<AuditLogRequest> captured)
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var roleStore = new Mock<IRoleStore<ApplicationRole>>();
        var roleManager = new Mock<RoleManager<ApplicationRole>>(
            roleStore.Object, null!, null!, null!, null!);
        roleManager
            .Setup(m => m.FindByIdAsync(role.Id))
            .ReturnsAsync(role);

        var auditLogService = new Mock<IAuditLogService>();
        auditLogService
            .Setup(a => a.LogAsync(It.IsAny<AuditLogRequest>()))
            .Callback<AuditLogRequest>(captured)
            .Returns(Task.CompletedTask);

        var currentUserAccessor = new Mock<ICurrentUserAccessor>();
        currentUserAccessor.SetupGet(c => c.UserId).Returns("acting-user");
        currentUserAccessor.SetupGet(c => c.IpAddress).Returns("127.0.0.1");

        var logger = new Mock<ILogger<PermissionService>>();

        return new PermissionService(
            dbContext,
            userManager.Object,
            roleManager.Object,
            auditLogService.Object,
            currentUserAccessor.Object,
            logger.Object);
    }

    /// <summary>
    /// Deserializes a captured audit <c>{ "permissions": [...] }</c> JSON string into a
    /// case-insensitive set of permission keys for order-independent comparison.
    /// </summary>
    private static HashSet<string> ExtractPermissionSet(string? json)
    {
        var payload = JsonSerializer.Deserialize<PermissionsPayload>(json!, CamelCaseOptions);
        return new HashSet<string>(payload?.Permissions ?? [], StringComparer.OrdinalIgnoreCase);
    }

    #endregion

    #region Property 10: Audit Accuracy

    /// <summary>
    /// Property: for a randomly generated non-Admin role, a random "old" grant set, and a random
    /// "new" grant set (both drawn from <see cref="KeyPool"/>), calling
    /// <see cref="PermissionService.UpdateRolePermissionsAsync(string, System.Collections.Generic.List{string})"/>
    /// captures exactly one <see cref="AuditLogRequest"/> whose permission arrays set-equal the old
    /// and new sets and whose scalar fields match the expected role identity, action, and entity
    /// type.
    /// **Validates: Requirements 11.1, 11.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpdateRolePermissions_AuditEntryReflectsChange()
    {
        var subsetGen = Gen.SubListOf(KeyPool).Select(keys => keys.ToList());
        var displayNameGen = Gen.Elements("Content Editor", "Report Viewer", "Operations", "Support");

        var modelGen = subsetGen
            .SelectMany(oldKeys => subsetGen
                .SelectMany(newKeys => displayNameGen
                    .Select(displayName => (OldKeys: oldKeys, NewKeys: newKeys, DisplayName: displayName))));

        return Prop.ForAll(Arb.From(modelGen), model =>
        {
            var (dbContext, connection) = CreateDbContext();
            try
            {
                var permissionIdByKey = SeedPermissions(dbContext);

                var roleId = Guid.NewGuid().ToString();
                SeedRoleWithGrants(dbContext, roleId, model.DisplayName, permissionIdByKey, model.OldKeys);

                // The role instance the mocked RoleManager returns — carries the DisplayName used
                // as the audit EntityName.
                var role = new ApplicationRole
                {
                    Id = roleId,
                    Name = "Editor",
                    NormalizedName = "EDITOR",
                    DisplayName = model.DisplayName
                };

                AuditLogRequest? captured = null;
                var service = CreateService(dbContext, role, request => captured = request);

                service.UpdateRolePermissionsAsync(roleId, model.NewKeys.ToList())
                    .GetAwaiter().GetResult();

                if (captured is null)
                {
                    return false.Label("No AuditLogRequest was captured — LogAsync was not called.");
                }

                var expectedOld = new HashSet<string>(model.OldKeys, StringComparer.OrdinalIgnoreCase);
                var expectedNew = new HashSet<string>(model.NewKeys, StringComparer.OrdinalIgnoreCase);

                var actualOld = ExtractPermissionSet(captured.OldValues);
                var actualNew = ExtractPermissionSet(captured.NewValues);

                // OldValues set-equality: the "old" keys are sourced from a DB query whose row order
                // is not guaranteed, so we compare the permissions array as a set rather than by
                // positional string equality.
                var oldSetMatches = actualOld.SetEquals(expectedOld);
                var newSetMatches = actualNew.SetEquals(expectedNew);

                // NewValues shape: the "new" keys are the caller-supplied list in list order, so the
                // serialized value must equal exactly the camelCase { "permissions": [...] } object
                // produced by AuditChangeHelper.Serialize for that same list.
                var expectedNewJson = AuditChangeHelper.Serialize(new { permissions = model.NewKeys });
                var newShapeMatches = captured.NewValues == expectedNewJson;

                // OldValues shape: deserialization into the { permissions: [...] } payload must
                // succeed (confirming the camelCase object shape) and round-trip to the expected set.
                var oldShapeMatches = JsonSerializer
                    .Deserialize<PermissionsPayload>(captured.OldValues ?? "null", CamelCaseOptions)?.Permissions is not null;

                var entityIdMatches = captured.EntityId == roleId;
                var entityNameMatches = captured.EntityName == model.DisplayName;
                var actionTypeMatches = captured.ActionType == AuditActionType.SettingsChanged;
                var entityTypeMatches = captured.EntityType == AuditEntityType.Role;

                var allMatch = oldSetMatches
                    && newSetMatches
                    && oldShapeMatches
                    && newShapeMatches
                    && entityIdMatches
                    && entityNameMatches
                    && actionTypeMatches
                    && entityTypeMatches;

                return allMatch.Label(
                    $"OldSet={oldSetMatches} NewSet={newSetMatches} " +
                    $"OldShape={oldShapeMatches} NewShape={newShapeMatches} " +
                    $"(expectedNew '{expectedNewJson}', actualNew '{captured.NewValues}', actualOld '{captured.OldValues}') " +
                    $"EntityId={entityIdMatches} EntityName={entityNameMatches} " +
                    $"ActionType={actionTypeMatches} EntityType={entityTypeMatches}");
            }
            finally
            {
                dbContext.Dispose();
                connection.Dispose();
            }
        });
    }

    #endregion
}
