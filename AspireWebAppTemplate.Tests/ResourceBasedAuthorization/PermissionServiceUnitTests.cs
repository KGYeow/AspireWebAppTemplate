// Feature: resource-based-authorization, Task 3.7: Unit tests for PermissionService
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.AuditLog;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services.Permissions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AspireWebAppTemplate.Tests.ResourceBasedAuthorization;

/// <summary>
/// Unit tests covering the validation and guard paths of
/// <see cref="PermissionService.UpdateRolePermissionsAsync(string, System.Collections.Generic.List{string})"/>.
/// </summary>
/// <remarks>
/// <para>
/// These example-based tests exercise the three distinct failure guards that run before any grant
/// mutation occurs, plus the de-duplication behavior of a successful full replacement:
/// </para>
/// <list type="bullet">
/// <item><description>Unknown role → <see cref="KeyNotFoundException"/> (Requirement 8.7).</description></item>
/// <item><description>Admin role → <see cref="InvalidOperationException"/> (Requirement 3.4).</description></item>
/// <item><description>Invalid permission key → <see cref="ArgumentException"/> naming the undefined keys (Requirement 8.5).</description></item>
/// <item><description>Duplicate keys within the request are de-duplicated without a composite-PK violation (Requirement 1.7).</description></item>
/// </list>
/// <para>
/// **Validates: Requirements 1.7, 3.4, 8.5.**
/// </para>
/// <para>
/// Tests use a real <see cref="ApplicationDbContext"/> over a SQLite in-memory database (foreign-key
/// enforcement ON, matching the shared resource-based authorization test setup) and a real
/// <see cref="PermissionService"/>. The <see cref="RoleManager{ApplicationRole}"/> is a Moq double
/// configured per test to control the <c>FindByIdAsync</c> result; the
/// <see cref="UserManager{ApplicationUser}"/>, <see cref="IAuditLogService"/>,
/// <see cref="ICurrentUserAccessor"/>, and <see cref="ILogger{PermissionService}"/> dependencies are
/// Moq doubles that are not consulted by the validation paths under test.
/// </para>
/// </remarks>
public class PermissionServiceUnitTests
{
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
    /// Creates a Moq <see cref="RoleManager{ApplicationRole}"/> whose <c>FindByIdAsync</c> returns
    /// the supplied role (or <c>null</c> to simulate an unknown role).
    /// </summary>
    /// <param name="findByIdResult">The role to return from <c>FindByIdAsync</c>, or <c>null</c>.</param>
    private static Mock<RoleManager<ApplicationRole>> CreateRoleManager(ApplicationRole? findByIdResult)
    {
        var roleStore = new Mock<IRoleStore<ApplicationRole>>();
        var roleManager = new Mock<RoleManager<ApplicationRole>>(
            roleStore.Object, null!, null!, null!, null!);

        roleManager
            .Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(findByIdResult);

        return roleManager;
    }

    /// <summary>
    /// Constructs a real <see cref="PermissionService"/> over the supplied context and role manager.
    /// The user manager, audit log service, current-user accessor, and logger are Moq doubles that
    /// the validation paths under test do not consult.
    /// </summary>
    private static PermissionService CreateService(
        ApplicationDbContext dbContext,
        Mock<RoleManager<ApplicationRole>> roleManager)
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
            roleManager.Object,
            auditLogService.Object,
            currentUserAccessor.Object,
            logger.Object);
    }

    /// <summary>
    /// Builds an <see cref="ApplicationRole"/> with the supplied name for use as the
    /// <c>FindByIdAsync</c> result.
    /// </summary>
    private static ApplicationRole BuildRole(string name) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        DisplayName = name
    };

    /// <summary>
    /// Seeds the supplied permission keys as <see cref="Permission"/> rows so they resolve as valid
    /// grant targets during <c>UpdateRolePermissionsAsync</c> key validation.
    /// </summary>
    private static void SeedPermissions(ApplicationDbContext dbContext, params string[] keys)
    {
        foreach (var key in keys)
        {
            dbContext.Set<Permission>().Add(new Permission
            {
                Key = key,
                DisplayName = key,
                Module = key.Split('.')[0],
                Description = null
            });
        }

        dbContext.SaveChanges();
    }

    #endregion

    #region UpdateRolePermissionsAsync — Validation Guards

    /// <summary>
    /// Verifies that updating permissions for a role ID that does not resolve to any role throws a
    /// <see cref="KeyNotFoundException"/> (the 404 path) before any grant mutation.
    /// **Validates: Requirement 8.7.**
    /// </summary>
    [Fact]
    public async Task UpdateRolePermissionsAsync_UnknownRole_ThrowsKeyNotFoundException()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            // RoleManager.FindByIdAsync returns null → unknown role.
            var roleManager = CreateRoleManager(findByIdResult: null);
            var service = CreateService(dbContext, roleManager);

            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => service.UpdateRolePermissionsAsync("missing-role-id", ["Users.Read"]));
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    /// <summary>
    /// Verifies that attempting to modify the Admin role's permissions throws an
    /// <see cref="InvalidOperationException"/> — the Admin role's grants are immutable.
    /// **Validates: Requirement 3.4.**
    /// </summary>
    [Fact]
    public async Task UpdateRolePermissionsAsync_AdminRole_ThrowsInvalidOperationException()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            // FindByIdAsync returns a role named "Admin" → modification forbidden.
            var roleManager = CreateRoleManager(BuildRole("Admin"));
            var service = CreateService(dbContext, roleManager);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateRolePermissionsAsync("admin-role-id", ["Users.Read"]));
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    /// <summary>
    /// Verifies that supplying a permission key not present in the Permissions table throws an
    /// <see cref="ArgumentException"/> whose message names the undefined key, while valid keys in
    /// the same request are not reported.
    /// **Validates: Requirement 8.5.**
    /// </summary>
    [Fact]
    public async Task UpdateRolePermissionsAsync_InvalidKey_ThrowsArgumentExceptionNamingInvalidKey()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            // Seed only the valid key; "Users.Nonexistent" is deliberately undefined.
            SeedPermissions(dbContext, "Users.Read");

            var roleManager = CreateRoleManager(BuildRole("Editor"));
            var service = CreateService(dbContext, roleManager);

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => service.UpdateRolePermissionsAsync(
                    "editor-role-id",
                    ["Users.Read", "Users.Nonexistent"]));

            // The message must name the undefined key and must not name the valid one.
            Assert.Contains("Users.Nonexistent", exception.Message);
            Assert.DoesNotContain("Users.Read", exception.Message);
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    #endregion

    #region UpdateRolePermissionsAsync — Successful Replacement

    /// <summary>
    /// Verifies that duplicate permission keys within a single request are de-duplicated so the
    /// full replacement completes without a composite-PK violation, and the role ends up with a
    /// single grant for the repeated key.
    /// **Validates: Requirement 1.7.**
    /// </summary>
    [Fact]
    public async Task UpdateRolePermissionsAsync_DuplicateKeys_DedupedWithoutPrimaryKeyViolation()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            SeedPermissions(dbContext, "Users.Read");

            var role = BuildRole("Editor");
            dbContext.Roles.Add(role);
            dbContext.SaveChanges();

            var roleManager = CreateRoleManager(role);
            var service = CreateService(dbContext, roleManager);

            // Same key supplied twice — must be deduped rather than inserting two identical grants
            // (which would violate the (RoleId, PermissionId) composite primary key).
            await service.UpdateRolePermissionsAsync(role.Id, ["Users.Read", "Users.Read"]);

            var grantCount = await dbContext.RolePermissions
                .AsNoTracking()
                .CountAsync(rp => rp.RoleId == role.Id);

            Assert.Equal(1, grantCount);
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    #endregion
}
