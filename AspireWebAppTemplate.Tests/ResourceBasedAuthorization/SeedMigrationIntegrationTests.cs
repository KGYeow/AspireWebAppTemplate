// Feature: resource-based-authorization, Task 8.7: End-to-end seed integration test
using System.Reflection;
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.AuditLog;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.SeedData;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services.Permissions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AspireWebAppTemplate.Tests.ResourceBasedAuthorization;

/// <summary>
/// End-to-end integration tests covering the resource-based authorization seed pipeline against a
/// real <see cref="ApplicationDbContext"/> over a SQLite in-memory database. The tests invoke the
/// actual (private static) <c>SeedData.SeedPermissionsAsync</c> routine via reflection, then verify
/// the resulting data through a real <see cref="PermissionService"/>.
/// </summary>
/// <remarks>
/// <para>
/// **Validates: Requirements 2.1, 2.3, 5.5**
/// </para>
/// <list type="bullet">
///   <item><description>Req 2.1 — seeding produces exactly the 16 canonical permission definitions.</description></item>
///   <item><description>Req 2.3 — the Admin role is granted all 16 permissions.</description></item>
///   <item><description>Req 5.5 — the permission-resolution JOIN query
///     (<see cref="PermissionService.GetPermissionsForRolesAsync"/>) returns the correct distinct
///     union of keys for a given set of roles.</description></item>
/// </list>
/// <para>
/// The in-memory setup (open connection, <c>PRAGMA foreign_keys = ON</c>, <c>EnsureCreated</c>,
/// connection kept alive for the database's lifetime) matches the shared pattern used by
/// <c>PermissionEntityConfigurationTests</c> so foreign-key enforcement mirrors production.
/// </para>
/// </remarks>
public class SeedMigrationIntegrationTests
{
    #region Constants

    /// <summary>
    /// The expected number of canonical permission definitions produced by seeding (Req 2.1).
    /// </summary>
    private const int ExpectedPermissionCount = 16;

    #endregion

    #region Test Infrastructure

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
    /// Seeds a minimal <see cref="ApplicationRole"/> directly onto the context and returns the
    /// tracked entity (with its assigned id).
    /// </summary>
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
    /// Invokes the private static <c>SeedData.SeedPermissionsAsync(dbContext, roleManager, logger)</c>
    /// via reflection. The supplied <see cref="RoleManager{ApplicationRole}"/> double only needs to
    /// resolve the Admin role (the single RoleManager call the method makes).
    /// </summary>
    private static async Task InvokeSeedPermissionsAsync(
        ApplicationDbContext dbContext,
        RoleManager<ApplicationRole> roleManager,
        ILogger logger)
    {
        var method = typeof(SeedData).GetMethod(
            "SeedPermissionsAsync",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        await (Task)method.Invoke(null, [dbContext, roleManager, logger])!;
    }

    /// <summary>
    /// Builds a <see cref="RoleManager{ApplicationRole}"/> double whose <c>FindByNameAsync("Admin")</c>
    /// returns the supplied Admin role (the only RoleManager interaction performed by
    /// <c>SeedPermissionsAsync</c>) and whose <c>FindByIdAsync</c> resolves the supplied roles by id
    /// (needed by <c>UpdateRolePermissionsAsync</c>).
    /// </summary>
    private static RoleManager<ApplicationRole> CreateRoleManager(params ApplicationRole[] roles)
    {
        var roleStore = new Mock<IRoleStore<ApplicationRole>>();
        var roleManager = new Mock<RoleManager<ApplicationRole>>(
            roleStore.Object, null!, null!, null!, null!);

        var adminRole = roles.FirstOrDefault(r => r.Name == "Admin");
        if (adminRole is not null)
        {
            roleManager
                .Setup(m => m.FindByNameAsync("Admin"))
                .ReturnsAsync(adminRole);
        }

        foreach (var role in roles)
        {
            roleManager
                .Setup(m => m.FindByIdAsync(role.Id))
                .ReturnsAsync(role);
        }

        return roleManager.Object;
    }

    /// <summary>
    /// Constructs a real <see cref="PermissionService"/> over the supplied context and role manager.
    /// The user manager is an inert Moq double; the audit log service and current-user accessor are
    /// no-op doubles (audit failures must never disrupt the primary operation), and the logger is a
    /// Moq double.
    /// </summary>
    private static PermissionService CreateService(
        ApplicationDbContext dbContext,
        RoleManager<ApplicationRole> roleManager)
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var auditLogService = new Mock<IAuditLogService>();
        var currentUserAccessor = new Mock<ICurrentUserAccessor>();
        var logger = new Mock<ILogger<PermissionService>>();

        return new PermissionService(
            dbContext,
            userManager.Object,
            roleManager,
            auditLogService.Object,
            currentUserAccessor.Object,
            logger.Object);
    }

    #endregion

    #region End-to-End Seed

    /// <summary>
    /// End-to-end check that running the seed produces exactly the 16 canonical permission
    /// definitions (Req 2.1) and grants all 16 to the Admin role (Req 2.3).
    /// </summary>
    [Fact]
    public async Task Seed_ProducesCanonicalPermissionsAndAdminGrants()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            // Arrange: an Admin role and a non-Admin Editor role.
            var adminRole = SeedRole(dbContext, "Admin");
            var editorRole = SeedRole(dbContext, "Editor");

            var logger = NullLogger.Instance;
            var roleManager = CreateRoleManager(adminRole, editorRole);

            // Act: run the real seed routine.
            await InvokeSeedPermissionsAsync(dbContext, roleManager, logger);

            // Assert (Req 2.1): exactly 16 permission definitions exist.
            Assert.Equal(ExpectedPermissionCount, await dbContext.Permissions.CountAsync());

            // Assert (Req 2.3): the Admin role holds all 16 permissions.
            var adminGrantCount = await dbContext.RolePermissions
                .CountAsync(rp => rp.RoleId == adminRole.Id);
            Assert.Equal(ExpectedPermissionCount, adminGrantCount);
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    /// <summary>
    /// Verifies that the full permission-resolution JOIN query
    /// (<see cref="PermissionService.GetPermissionsForRolesAsync"/>) returns the correct distinct
    /// union of keys after seeding: the Admin resolves to all 16 seeded keys, and a non-Admin role
    /// granted a couple of permissions directly (via the service's full-replacement path) resolves
    /// to exactly those two keys (Req 5.5).
    /// </summary>
    [Fact]
    public async Task GetPermissionsForRoles_AfterSeed_ReturnsCorrectUnionPerRole()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            // Arrange: seed permissions + Admin grants exactly as production does.
            var adminRole = SeedRole(dbContext, "Admin");
            var editorRole = SeedRole(dbContext, "Editor");

            var logger = NullLogger.Instance;
            var roleManager = CreateRoleManager(adminRole, editorRole);
            await InvokeSeedPermissionsAsync(dbContext, roleManager, logger);

            var service = CreateService(dbContext, roleManager);

            // Grant the Editor two permissions directly via the real full-replacement path.
            await service.UpdateRolePermissionsAsync(editorRole.Id, ["Users.Read", "AuditLog.Read"]);

            // Assert (Req 5.5): the Editor's resolved union is exactly its two direct grants.
            var editorPermissions = await service.GetPermissionsForRolesAsync([editorRole.Id]);
            Assert.Equal(
                new HashSet<string>(["Users.Read", "AuditLog.Read"], StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(editorPermissions, StringComparer.OrdinalIgnoreCase));

            // Assert (Req 5.5 / 2.3): the Admin's resolved union is all 16 seeded keys, distinct.
            var adminPermissions = await service.GetPermissionsForRolesAsync([adminRole.Id]);
            Assert.Equal(ExpectedPermissionCount, adminPermissions.Count);
            Assert.Equal(
                ExpectedPermissionCount,
                new HashSet<string>(adminPermissions, StringComparer.OrdinalIgnoreCase).Count);
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    #endregion
}
