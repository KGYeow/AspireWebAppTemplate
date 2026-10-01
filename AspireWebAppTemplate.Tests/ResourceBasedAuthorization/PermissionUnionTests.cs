// Feature: resource-based-authorization, Property 3: Effective permissions equal the union of role grants
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.AuditLog;
using AspireWebAppTemplate.Application.Features.Permissions;
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
/// Property-based tests verifying that
/// <see cref="PermissionService.GetPermissionsForRolesAsync(System.Collections.Generic.IEnumerable{string})"/>
/// resolves effective permissions as the DISTINCT (case-insensitive) union of all permission keys
/// granted — via <see cref="RolePermission"/> join records — to the supplied set of role IDs.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Property 3 — Effective permissions equal the union of role grants.</strong> For any set
/// of role IDs with associated permission grants, the returned set equals the distinct union of all
/// permission keys from <see cref="RolePermission"/> records matching those role IDs. The handler
/// denial half of Requirement 4.7 is covered by the authorization-handler tests; these tests focus
/// solely on union-resolution correctness.
/// </para>
/// <para>
/// **Validates: Requirements 4.1, 4.7, 5.1**
/// </para>
/// <para>
/// Tests use a real <see cref="ApplicationDbContext"/> over a SQLite in-memory database (foreign-key
/// enforcement ON, matching <c>PermissionEntityConfigurationTests</c>) and a real
/// <see cref="PermissionService"/>. Because <c>GetPermissionsForRolesAsync</c> touches only the
/// <see cref="ApplicationDbContext"/>, the Identity <see cref="UserManager{ApplicationUser}"/> and
/// <see cref="RoleManager{ApplicationRole}"/> dependencies are supplied as Moq test doubles.
/// </para>
/// </remarks>
public class PermissionUnionTests
{
    #region Test Infrastructure

    /// <summary>
    /// The pool of candidate permission keys used to build random role grants. The pool
    /// deliberately mixes modules and includes keys that differ only by casing (e.g.
    /// <c>Users.Read</c> vs <c>users.read</c>) so the test exercises the service's case-insensitive
    /// distinctness.
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
    /// Constructs a real <see cref="PermissionService"/> over the supplied context. The Identity
    /// managers, audit log service, current-user accessor, and logger are Moq doubles because
    /// <c>GetPermissionsForRolesAsync</c> consults only the <see cref="ApplicationDbContext"/>.
    /// </summary>
    private static PermissionService CreateService(ApplicationDbContext dbContext)
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var roleStore = new Mock<IRoleStore<ApplicationRole>>();
        var roleManager = new Mock<RoleManager<ApplicationRole>>(
            roleStore.Object, null!, null!, null!, null!);

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
    /// Seeds the supplied role → permission-key grant model into the database: inserts an
    /// <see cref="ApplicationRole"/> per role, inserts a single <see cref="Permission"/> per DISTINCT
    /// key (case-insensitive) referenced anywhere in the model, then inserts the
    /// <see cref="RolePermission"/> join rows. Returns the generated role IDs in the same order as
    /// the input model so callers can select subsets.
    /// </summary>
    /// <param name="dbContext">The context to seed.</param>
    /// <param name="roleGrants">
    /// Ordered list of per-role grant sets. Each inner list is the collection of permission keys to
    /// grant to the role at that position.
    /// </param>
    /// <returns>The database-generated role IDs, aligned by index with <paramref name="roleGrants"/>.</returns>
    private static List<string> SeedModel(ApplicationDbContext dbContext, IReadOnlyList<List<string>> roleGrants)
    {
        // Insert one Permission row per distinct key (case-insensitive) so RolePermission grants have
        // a valid FK target. The map keys on the canonical first-seen spelling, keyed case-insensitively.
        var permissionIdByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in roleGrants.SelectMany(g => g).Distinct(StringComparer.OrdinalIgnoreCase))
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

        // Insert a role per grant set and wire up its RolePermission join rows.
        var roleIds = new List<string>(roleGrants.Count);
        for (var i = 0; i < roleGrants.Count; i++)
        {
            var role = new ApplicationRole
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Role{i}",
                NormalizedName = $"ROLE{i}",
                DisplayName = $"Role {i}"
            };
            dbContext.Roles.Add(role);
            dbContext.SaveChanges();
            roleIds.Add(role.Id);

            // Deduplicate within a single role (case-insensitive) to avoid composite-PK collisions,
            // since the model generator may produce the same key twice for one role.
            foreach (var key in roleGrants[i].Distinct(StringComparer.OrdinalIgnoreCase))
            {
                dbContext.Set<RolePermission>().Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permissionIdByKey[key]
                });
            }

            dbContext.SaveChanges();
        }

        return roleIds;
    }

    #endregion

    #region Property 3: Union Resolution

    /// <summary>
    /// Property: for a randomly generated set of roles — each with a random set of permission grants
    /// drawn from <see cref="KeyPool"/> — querying
    /// <see cref="PermissionService.GetPermissionsForRolesAsync(System.Collections.Generic.IEnumerable{string})"/>
    /// for a random subset of the seeded role IDs returns exactly the DISTINCT (case-insensitive)
    /// union of the permission keys granted to those selected roles.
    /// **Validates: Requirements 4.1, 4.7, 5.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetPermissionsForRoles_ReturnsDistinctUnionOfSelectedRoleGrants()
    {
        // Generate 1..4 roles, each granted a random sub-list of the key pool (possibly empty).
        var grantGen = Gen.SubListOf(KeyPool).Select(keys => keys.ToList());
        var modelGen = Gen.Choose(1, 4)
            .SelectMany(roleCount => Gen.ArrayOf(grantGen, roleCount)
                .Select(grants => grants.ToList() as IReadOnlyList<List<string>>));

        return Prop.ForAll(Arb.From(modelGen), model =>
        {
            // For each model, also choose which seeded roles to query. We use a boolean selector mask
            // generated per role so the queried subset ranges over empty..all roles.
            var selectorGen = Gen.ArrayOf(Gen.Elements(true, false), model.Count);

            return Prop.ForAll(Arb.From(selectorGen), selectorMask =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    var roleIds = SeedModel(dbContext, model);

                    // Build the queried subset of role IDs from the selector mask.
                    var selectedRoleIds = new List<string>();
                    for (var i = 0; i < roleIds.Count; i++)
                    {
                        if (selectorMask[i])
                        {
                            selectedRoleIds.Add(roleIds[i]);
                        }
                    }

                    // Expected: distinct (case-insensitive) union of keys granted to the selected
                    // roles, computed by mapping each selected ID back to its seeded grant set.
                    var expected = BuildExpectedUnion(model, roleIds, selectedRoleIds);

                    var service = CreateService(dbContext);
                    var actual = service.GetPermissionsForRolesAsync(selectedRoleIds)
                        .GetAwaiter().GetResult();

                    var actualSet = new HashSet<string>(actual, StringComparer.OrdinalIgnoreCase);

                    // The result must equal the expected union AND contain no case-insensitive
                    // duplicates (i.e. the returned list length matches its distinct-set size).
                    var setMatches = actualSet.SetEquals(expected);
                    var noDuplicates = actual.Count == actualSet.Count;

                    return (setMatches && noDuplicates)
                        .Label($"Selected {selectedRoleIds.Count}/{roleIds.Count} roles. " +
                               $"Expected [{string.Join(", ", expected.OrderBy(k => k))}] " +
                               $"(count={expected.Count}). " +
                               $"Actual [{string.Join(", ", actual.OrderBy(k => k))}] " +
                               $"(count={actual.Count}, distinct={actualSet.Count}).");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
        });
    }

    /// <summary>
    /// Computes the expected distinct (case-insensitive) union of permission keys for the selected
    /// role IDs by mapping each selected ID back to its position in the seeded model.
    /// </summary>
    private static HashSet<string> BuildExpectedUnion(
        IReadOnlyList<List<string>> model,
        IReadOnlyList<string> roleIds,
        IEnumerable<string> selectedRoleIds)
    {
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var selectedId in selectedRoleIds)
        {
            var index = -1;
            for (var i = 0; i < roleIds.Count; i++)
            {
                if (roleIds[i] == selectedId)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                continue;
            }

            foreach (var key in model[index])
            {
                expected.Add(key);
            }
        }

        return expected;
    }

    #endregion
}
