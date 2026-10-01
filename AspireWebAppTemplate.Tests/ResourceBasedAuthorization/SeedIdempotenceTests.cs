// Feature: resource-based-authorization, Property 6: Seed process is idempotent and non-destructive
using System.Reflection;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Data.SeedData;
using AspireWebAppTemplate.Infrastructure.Identity;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Gen = FsCheck.Fluent.Gen;
using Property = FsCheck.Property;

namespace AspireWebAppTemplate.Tests.ResourceBasedAuthorization;

/// <summary>
/// Property-based tests verifying that the permission seed routine
/// <c>SeedData.SeedPermissionsAsync</c> (which internally calls
/// <c>AssignPermissionsToAdminAsync</c>) is idempotent and non-destructive: running it any number
/// of times (N ≥ 1) converges on exactly the 16 canonical permission definitions, grants all 16 to
/// the Admin role, and never alters pre-existing non-Admin role-permission assignments.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Property 6 — Seed process is idempotent and non-destructive.</strong> For any initial
/// database state containing existing non-Admin role-permission assignments, running the permission
/// seed N ≥ 1 times results in: exactly 16 <see cref="Permission"/> rows (no duplicates), the Admin
/// role assigned to all 16, and all pre-existing non-Admin role-permission assignments unchanged.
/// </para>
/// <para>
/// **Validates: Requirements 2.2, 2.4, 2.5**
/// </para>
/// <para>
/// Tests use a real <see cref="ApplicationDbContext"/> over a SQLite in-memory database (foreign-key
/// enforcement ON, matching <c>PermissionEntityConfigurationTests</c>). The seed method is
/// <c>private static</c> on the partial <c>SeedData</c> class, so it is invoked through reflection.
/// It only calls <c>roleManager.FindByNameAsync("Admin")</c>, so a <see cref="RoleManager{T}"/> test
/// double is sufficient — it is configured to return the seeded Admin role.
/// </para>
/// </remarks>
public class SeedIdempotenceTests
{
    #region Test Infrastructure

    /// <summary>
    /// The expected number of canonical permission definitions produced by the seed, as defined by
    /// the <c>SeedData.SeedPermissions</c> array.
    /// </summary>
    private const int ExpectedPermissionCount = 16;

    /// <summary>
    /// Cached reflection handle to the <c>private static</c> <c>SeedPermissionsAsync</c> method on
    /// the partial <see cref="SeedData"/> class.
    /// </summary>
    private static readonly MethodInfo SeedPermissionsMethod =
        typeof(SeedData).GetMethod(
            "SeedPermissionsAsync",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "Could not locate the private static SeedData.SeedPermissionsAsync method via reflection.");

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
    /// Seeds a minimal <see cref="ApplicationRole"/> directly onto the context and returns it.
    /// </summary>
    /// <param name="dbContext">The context to seed into.</param>
    /// <param name="name">The role name (also used as its normalized name and display name).</param>
    private static ApplicationRole SeedRole(ApplicationDbContext dbContext, string name)
    {
        var role = new ApplicationRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            DisplayName = name
        };
        dbContext.Roles.Add(role);
        dbContext.SaveChanges();
        return role;
    }

    /// <summary>
    /// Builds a <see cref="RoleManager{ApplicationRole}"/> test double whose
    /// <c>FindByNameAsync("Admin")</c> returns the supplied Admin role — the only role-manager call
    /// the seed makes.
    /// </summary>
    /// <param name="adminRole">The Admin role to resolve, or <c>null</c> to simulate its absence.</param>
    private static RoleManager<ApplicationRole> CreateRoleManager(ApplicationRole? adminRole)
    {
        var roleStore = new Mock<IRoleStore<ApplicationRole>>();
        var roleManager = new Mock<RoleManager<ApplicationRole>>(
            roleStore.Object, null!, null!, null!, null!);

        roleManager
            .Setup(m => m.FindByNameAsync("Admin"))
            .ReturnsAsync(adminRole);

        return roleManager.Object;
    }

    /// <summary>
    /// Invokes the reflected <c>SeedPermissionsAsync</c> method and awaits the returned
    /// <see cref="Task"/>.
    /// </summary>
    /// <param name="dbContext">The context passed to the seed.</param>
    /// <param name="roleManager">The role manager passed to the seed.</param>
    private static async Task InvokeSeedAsync(
        ApplicationDbContext dbContext,
        RoleManager<ApplicationRole> roleManager)
    {
        var task = (Task)SeedPermissionsMethod.Invoke(
            null,
            [dbContext, roleManager, (ILogger)NullLogger.Instance])!;
        await task;
    }

    #endregion

    #region Property 6 — Seed Idempotence & Non-Destructiveness

    /// <summary>
    /// Verifies Property 6 against an initial database that already contains a non-Admin role with a
    /// pre-existing permission grant. After running the seed N ≥ 1 times:
    /// <list type="bullet">
    ///   <item><description>exactly 16 permission definitions exist (no duplicates);</description></item>
    ///   <item><description>the Admin role is granted all 16 permissions;</description></item>
    ///   <item><description>the pre-existing non-Admin grant is preserved and no further non-Admin
    ///     grants are added or removed by seeding.</description></item>
    /// </list>
    /// The pre-existing non-Admin grant is established by seeding one canonical permission and one
    /// non-Admin grant BEFORE any seed run, so the seed must leave it untouched.
    /// **Validates: Requirements 2.2, 2.4, 2.5**
    /// </summary>
    [Property(MaxTest = 20, Arbitrary = [typeof(SeedIdempotenceArbitraries)])]
    public Property Seed_IsIdempotent_AndPreservesExistingNonAdminGrants(int runCount)
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            // Admin role (resolved by the seed) plus a distinct non-Admin role whose pre-existing
            // grant must survive every seed run.
            var adminRole = SeedRole(dbContext, "Admin");
            var editorRole = SeedRole(dbContext, "Editor");

            // Pre-insert ONE canonical permission and grant it to the non-Admin role before seeding.
            // The seed defines this same key, so it exercises the "existing row left unchanged" path
            // while the non-Admin grant exercises the non-destructive guarantee.
            var preExisting = new Permission
            {
                Key = "Users.Read",
                DisplayName = "View Users",
                Module = "Users"
            };
            dbContext.Permissions.Add(preExisting);
            dbContext.SaveChanges();

            dbContext.RolePermissions.Add(new RolePermission
            {
                RoleId = editorRole.Id,
                PermissionId = preExisting.Id
            });
            dbContext.SaveChanges();

            var roleManager = CreateRoleManager(adminRole);

            // Run the seed N times.
            for (var i = 0; i < runCount; i++)
            {
                InvokeSeedAsync(dbContext, roleManager).GetAwaiter().GetResult();
            }

            // Re-read through a fresh context so assertions reflect persisted state, not tracked
            // entities.
            using var verifyContext = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);

            // (1) Exactly 16 permissions, no duplicate keys.
            var permissionCount = verifyContext.Permissions.Count();
            var distinctKeyCount = verifyContext.Permissions
                .Select(p => p.Key)
                .Distinct()
                .Count();

            // (2) Admin granted all 16.
            var adminGrantCount = verifyContext.RolePermissions
                .Count(rp => rp.RoleId == adminRole.Id);

            // (3) The pre-existing non-Admin grant is preserved, and no OTHER non-Admin grants exist
            //     (seeding only grants to Admin; it must not touch the Editor role).
            var editorGrants = verifyContext.RolePermissions
                .Where(rp => rp.RoleId == editorRole.Id)
                .Select(rp => rp.PermissionId)
                .ToList();

            var preExistingPreserved =
                editorGrants.Count == 1 && editorGrants[0] == preExisting.Id;

            return (permissionCount == ExpectedPermissionCount)
                .Label($"permission count {permissionCount} == {ExpectedPermissionCount}")
                .And((distinctKeyCount == ExpectedPermissionCount)
                    .Label($"distinct keys {distinctKeyCount} == {ExpectedPermissionCount}"))
                .And((adminGrantCount == ExpectedPermissionCount)
                    .Label($"admin grants {adminGrantCount} == {ExpectedPermissionCount}"))
                .And(preExistingPreserved
                    .Label("pre-existing non-Admin grant preserved and unchanged"));
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    #endregion
}

/// <summary>
/// FsCheck arbitrary providers for <see cref="SeedIdempotenceTests"/>.
/// </summary>
internal static class SeedIdempotenceArbitraries
{
    /// <summary>
    /// Provides the N ≥ 1 run-count generator, bounded to 1–4 so repeated seed runs stay fast while
    /// still proving convergence across multiple runs.
    /// </summary>
    public static Arbitrary<int> RunCount() => Gen.Choose(1, 4).ToArbitrary();
}
