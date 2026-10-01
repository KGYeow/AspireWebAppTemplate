// Feature: resource-based-authorization, Property 8: Full replacement strategy for role permissions
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.AuditLog;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services.Permissions;
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
/// Property-based tests verifying the full-replacement contract of
/// <see cref="PermissionService.UpdateRolePermissionsAsync(string, System.Collections.Generic.List{string})"/>
/// as observed through
/// <see cref="PermissionService.GetRolePermissionKeysAsync(string)"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Property 8 — Full replacement strategy for role permissions.</strong> For any valid set
/// of permission keys (all referencing seeded <see cref="Permission"/> definitions) and any
/// NON-Admin role, invoking <c>UpdateRolePermissionsAsync(roleId, keys)</c> makes a subsequent
/// <c>GetRolePermissionKeysAsync(roleId)</c> return EXACTLY that set (case-insensitive) — no more,
/// no less. In particular, supplying an empty set clears all of the role's existing grants. The
/// update fully replaces any prior configuration rather than merging with it.
/// </para>
/// <para>
/// **Validates: Requirements 8.4**
/// </para>
/// <para>
/// Tests use a real <see cref="ApplicationDbContext"/> over a SQLite in-memory database
/// (foreign-key enforcement ON) and a real <see cref="PermissionService"/>. The Identity
/// <see cref="UserManager{ApplicationUser}"/> is a bare Moq double (unused by the methods under
/// test); the <see cref="RoleManager{ApplicationRole}"/> double is configured so
/// <c>FindByIdAsync</c> returns the seeded non-Admin role (both methods under test resolve the
/// role through it). <see cref="IAuditLogService"/> is a no-op mock because audit failures must
/// never disrupt the primary operation.
/// </para>
/// </remarks>
public class FullReplacementTests
{
    #region Test Infrastructure

    /// <summary>
    /// The pool of candidate permission keys seeded as the valid universe for each test case. The
    /// pool mixes modules so generated subsets exercise cross-module replacement.
    /// </summary>
    private static readonly string[] KeyPool =
    [
        "Users.Read",
        "Users.Create",
        "Users.Update",
        "Users.Delete",
        "Roles.Read",
        "Roles.Manage",
        "AuditLog.Read",
        "AuditLog.Export",
        "EmailTemplates.Read",
        "EmailTemplates.Update"
    ];

    /// <summary>
    /// Creates a SQLite in-memory <see cref="ApplicationDbContext"/> with foreign-key enforcement
    /// enabled (open connection, <c>PRAGMA foreign_keys = ON</c>, <c>EnsureCreated</c>, connection
    /// kept alive for the database's lifetime), matching the shared in-memory setup pattern used
    /// across the resource-based authorization test suite.
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
    /// Constructs a real <see cref="PermissionService"/> over the supplied context. The
    /// <see cref="RoleManager{ApplicationRole}"/> double is configured so <c>FindByIdAsync</c>
    /// returns the supplied non-Admin <paramref name="role"/> for its ID (and null otherwise),
    /// because both methods under test resolve the role through it. The user manager and audit log
    /// service are inert doubles.
    /// </summary>
    /// <param name="dbContext">The context backing the service.</param>
    /// <param name="role">The non-Admin role that <c>FindByIdAsync</c> should return for its ID.</param>
    private static PermissionService CreateService(ApplicationDbContext dbContext, ApplicationRole role)
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var roleStore = new Mock<IRoleStore<ApplicationRole>>();
        var roleManager = new Mock<RoleManager<ApplicationRole>>(
            roleStore.Object, null!, null!, null!, null!);

        // Both UpdateRolePermissionsAsync and GetRolePermissionKeysAsync resolve the role via
        // RoleManager.FindByIdAsync; return the seeded non-Admin role for its ID.
        roleManager
            .Setup(m => m.FindByIdAsync(role.Id))
            .ReturnsAsync(role);

        // IAuditLogService.LogAsync defaults to a completed no-op task under Moq; the service also
        // swallows any audit failure, so no explicit setup is required.
        var auditLogService = new Mock<IAuditLogService>();
        var currentUserAccessor = new Mock<ICurrentUserAccessor>();
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
    /// Seeds one <see cref="Permission"/> row per key in <see cref="KeyPool"/> (the valid universe)
    /// and a single non-Admin <see cref="ApplicationRole"/>, returning the seeded role.
    /// </summary>
    /// <param name="dbContext">The context to seed.</param>
    /// <returns>The seeded non-Admin role.</returns>
    private static ApplicationRole SeedUniverse(ApplicationDbContext dbContext)
    {
        foreach (var key in KeyPool)
        {
            dbContext.Set<Permission>().Add(new Permission
            {
                Key = key,
                DisplayName = key,
                Module = key.Split('.')[0],
                Description = null
            });
        }

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Editor",
            NormalizedName = "EDITOR",
            DisplayName = "Editor"
        };
        dbContext.Roles.Add(role);
        dbContext.SaveChanges();

        return role;
    }

    #endregion

    #region Property 8: Full Replacement

    /// <summary>
    /// Property: starting from an arbitrary initial grant set and applying an arbitrary target set
    /// (both drawn from the seeded <see cref="KeyPool"/>, either possibly empty), a round-trip of
    /// <c>UpdateRolePermissionsAsync(roleId, target)</c> followed by
    /// <c>GetRolePermissionKeysAsync(roleId)</c> returns EXACTLY the target set (case-insensitive,
    /// no duplicates). The empty target case is included and must clear all grants.
    /// **Validates: Requirements 8.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpdateRolePermissions_ThenGet_ReturnsExactlyTheReplacementSet()
    {
        // Generate an initial grant subset (to seed a prior configuration) and a target subset (the
        // replacement). Both range over empty..full so the empty-clears case is covered.
        var subsetGen = Gen.SubListOf(KeyPool).Select(keys => keys.ToList());
        var pairGen = subsetGen.SelectMany(initial =>
            subsetGen.Select(target => (initial, target)));

        return Prop.ForAll(Arb.From(pairGen), pair =>
        {
            var (initialKeys, targetKeys) = pair;

            var (dbContext, connection) = CreateDbContext();
            try
            {
                var role = SeedUniverse(dbContext);
                var service = CreateService(dbContext, role);

                // Establish a prior configuration via the full-replacement API itself so the test
                // observes replacement over a non-trivial starting state (not just an empty role).
                service.UpdateRolePermissionsAsync(role.Id, initialKeys)
                    .GetAwaiter().GetResult();

                // Apply the replacement target set.
                service.UpdateRolePermissionsAsync(role.Id, targetKeys)
                    .GetAwaiter().GetResult();

                // Read back the role's effective grants.
                var actual = service.GetRolePermissionKeysAsync(role.Id)
                    .GetAwaiter().GetResult();

                var actualSet = new HashSet<string>(actual, StringComparer.OrdinalIgnoreCase);
                var expectedSet = new HashSet<string>(targetKeys, StringComparer.OrdinalIgnoreCase);

                // The stored set must equal exactly the target set (no leftovers from the initial
                // configuration) and contain no case-insensitive duplicates.
                var setMatches = actualSet.SetEquals(expectedSet);
                var noDuplicates = actual.Count == actualSet.Count;

                return (setMatches && noDuplicates)
                    .Label($"Initial [{string.Join(", ", initialKeys.OrderBy(k => k))}] -> " +
                           $"Target [{string.Join(", ", expectedSet.OrderBy(k => k))}] " +
                           $"(count={expectedSet.Count}). " +
                           $"Actual [{string.Join(", ", actual.OrderBy(k => k))}] " +
                           $"(count={actual.Count}, distinct={actualSet.Count}).");
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
