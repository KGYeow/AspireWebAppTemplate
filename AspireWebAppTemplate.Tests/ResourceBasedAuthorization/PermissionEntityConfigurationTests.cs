// Feature: resource-based-authorization, EF configuration integration tests
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AspireWebAppTemplate.Tests.ResourceBasedAuthorization;

/// <summary>
/// Integration tests verifying the EF Core configuration of the resource-based authorization
/// entities <see cref="Permission"/> and <see cref="RolePermission"/> against a real SQLite
/// in-memory database created from the model (via <c>EnsureCreated</c>).
/// </summary>
/// <remarks>
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
/// These tests exercise the configuration defined in <c>PermissionConfiguration</c> and
/// <c>RolePermissionConfiguration</c>:
/// <list type="bullet">
///   <item><description>the unique index on <see cref="Permission.Key"/>;</description></item>
///   <item><description>the composite primary key on
///     (<see cref="RolePermission.RoleId"/>, <see cref="RolePermission.PermissionId"/>) which
///     prevents duplicate grants;</description></item>
///   <item><description>cascade delete from both <see cref="ApplicationRole"/> and
///     <see cref="Permission"/> into <c>RolePermissions</c>;</description></item>
///   <item><description>the mapped tables and columns.</description></item>
/// </list>
/// Unlike the announcement tests, foreign-key enforcement is explicitly turned ON so the SQLite
/// provider actually applies the configured cascade-delete behavior.
/// </remarks>
public class PermissionEntityConfigurationTests
{
    #region Test Infrastructure

    /// <summary>
    /// Creates a SQLite in-memory <see cref="ApplicationDbContext"/> for testing with foreign-key
    /// enforcement enabled, matching the shared in-memory setup pattern used across the test suite
    /// (open connection, <c>EnsureCreated</c>, connection kept alive for the database's lifetime).
    /// </summary>
    /// <remarks>
    /// Foreign keys are enabled via <c>PRAGMA foreign_keys = ON</c> so that the cascade-delete
    /// relationships configured on <see cref="RolePermission"/> are actually honored by SQLite,
    /// which disables FK enforcement by default.
    /// </remarks>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Enable FK enforcement so cascade-delete behavior configured on RolePermission is applied.
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
    /// Seeds a minimal <see cref="ApplicationRole"/> directly onto the context and returns its id.
    /// </summary>
    private static string SeedRole(ApplicationDbContext dbContext, string name = "TestRole")
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
        return role.Id;
    }

    /// <summary>
    /// Seeds a minimal valid <see cref="Permission"/> and returns the tracked entity (with its
    /// database-generated id populated).
    /// </summary>
    private static Permission SeedPermission(ApplicationDbContext dbContext, string key)
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
        return permission;
    }

    #endregion

    #region Permission.Key Unique Constraint

    /// <summary>
    /// Verifies that <see cref="Permission.Key"/> carries a unique constraint: inserting a second
    /// permission with the same key throws <see cref="DbUpdateException"/> on save.
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Fact]
    public void PermissionKey_IsUnique_DuplicateInsertThrows()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            SeedPermission(dbContext, "Users.Read");

            dbContext.Set<Permission>().Add(new Permission
            {
                Key = "Users.Read",
                DisplayName = "View Users (duplicate)",
                Module = "Users"
            });

            Assert.Throws<DbUpdateException>(() => dbContext.SaveChanges());
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    /// <summary>
    /// Verifies that two permissions with distinct keys can coexist, confirming the unique
    /// constraint applies to the key value rather than rejecting all inserts.
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Fact]
    public void PermissionKey_DistinctKeys_AreAllowed()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            SeedPermission(dbContext, "Users.Read");
            SeedPermission(dbContext, "Users.Create");

            Assert.Equal(2, dbContext.Set<Permission>().Count());
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    #endregion

    #region RolePermission Composite Primary Key

    /// <summary>
    /// Verifies the composite primary key on (<see cref="RolePermission.RoleId"/>,
    /// <see cref="RolePermission.PermissionId"/>): inserting a duplicate pair throws
    /// <see cref="DbUpdateException"/> on save.
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Fact]
    public void RolePermission_CompositeKey_DuplicatePairThrows()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            var roleId = SeedRole(dbContext);
            var permission = SeedPermission(dbContext, "Users.Read");

            dbContext.Set<RolePermission>().Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permission.Id
            });
            dbContext.SaveChanges();

            // Insert the same (RoleId, PermissionId) pair again via a fresh context so change
            // tracking does not short-circuit the duplicate before it reaches the database.
            using var secondContext = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            secondContext.Set<RolePermission>().Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permission.Id
            });

            Assert.Throws<DbUpdateException>(() => secondContext.SaveChanges());
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    /// <summary>
    /// Verifies that the same permission can be granted to two different roles — the composite key
    /// only forbids duplicate (RoleId, PermissionId) pairs, not reuse of either component.
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Fact]
    public void RolePermission_SamePermissionDifferentRoles_IsAllowed()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            var roleA = SeedRole(dbContext, "RoleA");
            var roleB = SeedRole(dbContext, "RoleB");
            var permission = SeedPermission(dbContext, "Users.Read");

            dbContext.Set<RolePermission>().AddRange(
                new RolePermission { RoleId = roleA, PermissionId = permission.Id },
                new RolePermission { RoleId = roleB, PermissionId = permission.Id });
            dbContext.SaveChanges();

            Assert.Equal(2, dbContext.Set<RolePermission>().Count());
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    #endregion

    #region Cascade Delete

    /// <summary>
    /// Verifies cascade delete on the permission foreign key: deleting a <see cref="Permission"/>
    /// removes the <see cref="RolePermission"/> rows that reference it.
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Fact]
    public void DeletingPermission_CascadeDeletesRolePermissions()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            var roleId = SeedRole(dbContext);
            var permission = SeedPermission(dbContext, "Users.Read");

            dbContext.Set<RolePermission>().Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permission.Id
            });
            dbContext.SaveChanges();

            // Delete the permission through a fresh context to force a real DELETE round-trip,
            // then confirm the dependent join row was cascaded away.
            using (var deleteContext = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options))
            {
                var tracked = deleteContext.Set<Permission>().Single(p => p.Id == permission.Id);
                deleteContext.Set<Permission>().Remove(tracked);
                deleteContext.SaveChanges();
            }

            using var verifyContext = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            Assert.Empty(verifyContext.Set<RolePermission>());
            Assert.Empty(verifyContext.Set<Permission>());
            // The role itself must survive deletion of the permission.
            Assert.Single(verifyContext.Roles);
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    /// <summary>
    /// Verifies cascade delete on the role foreign key: deleting an <see cref="ApplicationRole"/>
    /// removes the <see cref="RolePermission"/> rows that reference it.
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Fact]
    public void DeletingRole_CascadeDeletesRolePermissions()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            var roleId = SeedRole(dbContext);
            var permission = SeedPermission(dbContext, "Users.Read");

            dbContext.Set<RolePermission>().Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permission.Id
            });
            dbContext.SaveChanges();

            using (var deleteContext = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options))
            {
                var tracked = deleteContext.Roles.Single(r => r.Id == roleId);
                deleteContext.Roles.Remove(tracked);
                deleteContext.SaveChanges();
            }

            using var verifyContext = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            Assert.Empty(verifyContext.Set<RolePermission>());
            // The permission itself must survive deletion of the role.
            Assert.Single(verifyContext.Set<Permission>());
            Assert.Empty(verifyContext.Roles);
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    #endregion

    #region Table & Column Mapping

    /// <summary>
    /// Verifies that the <c>Permissions</c> table exists with the expected columns, confirming the
    /// configured table name and property-to-column mapping.
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Fact]
    public void PermissionsTable_HasExpectedColumns()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            var columns = GetTableColumns(connection, "Permissions");

            Assert.Contains("Id", columns);
            Assert.Contains("Key", columns);
            Assert.Contains("DisplayName", columns);
            Assert.Contains("Module", columns);
            Assert.Contains("Description", columns);
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    /// <summary>
    /// Verifies that the <c>RolePermissions</c> table exists with the expected columns, confirming
    /// the configured table name and property-to-column mapping.
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Fact]
    public void RolePermissionsTable_HasExpectedColumns()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            var columns = GetTableColumns(connection, "RolePermissions");

            Assert.Contains("RoleId", columns);
            Assert.Contains("PermissionId", columns);
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    /// <summary>
    /// Reads the column names of a table from the SQLite schema via <c>PRAGMA table_info</c>.
    /// </summary>
    private static HashSet<string> GetTableColumns(SqliteConnection connection, string tableName)
    {
        var columns = new HashSet<string>(StringComparer.Ordinal);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info('{tableName}');";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            // Column 1 of PRAGMA table_info is the column name.
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    #endregion
}
