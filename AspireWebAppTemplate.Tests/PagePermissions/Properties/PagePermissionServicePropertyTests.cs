// Feature: page-access-permissions, Property 6: Permission Union Across Roles
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities.Template;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services;
using AspireWebAppTemplate.Infrastructure.Services.Template.PagePermissions;
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.Template.Navigation;
using AspireWebAppTemplate.Application.Common;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Gen = FsCheck.Fluent.Gen;
using Property = FsCheck.Property;

namespace AspireWebAppTemplate.Tests.PagePermissions.Properties;

/// <summary>
/// Property-based tests verifying that GetMyPagesAsync returns the exact union
/// of all page permissions across all roles assigned to a user.
/// </summary>
/// <remarks>
/// **Validates: Requirements 3.3**
/// </remarks>
public class PagePermissionServicePropertyTests
{
    /// <summary>
    /// Creates a SQLite in-memory ApplicationDbContext for testing.
    /// Foreign key enforcement is disabled to allow seeding PagePermission records
    /// without requiring full Identity role table setup via the ORM.
    /// </summary>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Disable foreign key enforcement so page permissions can be seeded
        // without requiring matching ApplicationRole records via the FK constraint.
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
    /// Creates a PagePermissionService with a mocked UserManager (to control role
    /// name resolution for the user) and a real RoleManager backed by EF Core
    /// (so that RoleManager.Roles supports IAsyncEnumerable for ToListAsync queries).
    /// </summary>
    private static PagePermissionService CreateService(
        ApplicationDbContext dbContext,
        ApplicationUser user,
        IList<string> userRoleNames)
    {
        // Mock UserManager — controls FindByIdAsync and GetRolesAsync responses
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        userManagerMock.Setup(m => m.FindByIdAsync(user.Id))
            .ReturnsAsync(user);
        userManagerMock.Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(userRoleNames);

        // Real RoleManager backed by EF Core RoleStore so that RoleManager.Roles
        // returns a proper IQueryable that supports async enumeration via EF.
        var roleStore = new RoleStore<ApplicationRole, ApplicationDbContext, string>(dbContext);
        var roleManager = new RoleManager<ApplicationRole>(
            roleStore, null, null, null, NullLogger<RoleManager<ApplicationRole>>.Instance);

        // Mock INavigationProvider (not needed for GetMyPagesAsync but required by constructor)
        var navigationProviderMock = new Mock<INavigationProvider>();
        navigationProviderMock.Setup(n => n.GetMainMenuItems())
            .Returns(new List<NavItem>());

        return new PagePermissionService(
            dbContext,
            userManagerMock.Object,
            roleManager,
            navigationProviderMock.Object,
            NullLogger<PagePermissionService>.Instance);
    }

    /// <summary>
    /// Pool of valid page paths used for Property 7 (PUT Idempotent Full Replacement).
    /// The mocked INavigationProvider returns NavItems for all these paths.
    /// Excludes paths that are in SystemPageDefaults.Paths (system pages bypass permissions).
    /// </summary>
    private static readonly string[] ValidPagePool =
    [
        "/counter",
        "/weather",
        "/auth",
        "/admin/user-management",
        "/admin/role-management",
        "/admin/audit-log",
        "/admin/page-permissions",
        "/dashboard",
        "/reports"
    ];

    /// <summary>
    /// Creates a PagePermissionService configured for UpdateRolePermissionsAsync testing.
    /// Uses a real RoleManager backed by EF Core (for FindByIdAsync) and a mocked
    /// INavigationProvider that returns ValidPagePool entries.
    /// </summary>
    private static PagePermissionService CreateServiceForUpdate(ApplicationDbContext dbContext)
    {
        // Mock UserManager (not used by UpdateRolePermissionsAsync but required by constructor)
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        // Real RoleManager backed by EF Core so that FindByIdAsync queries the database
        var roleStore = new RoleStore<ApplicationRole, ApplicationDbContext, string>(dbContext);
        var roleManager = new RoleManager<ApplicationRole>(
            roleStore, null, new UpperInvariantLookupNormalizer(), null,
            NullLogger<RoleManager<ApplicationRole>>.Instance);

        // Mock INavigationProvider returning NavItems for all ValidPagePool entries
        var navigationProviderMock = new Mock<INavigationProvider>();
        var navItems = ValidPagePool.Select(path => new NavItem
        {
            Type = NavItemType.Link,
            Text = path.TrimStart('/').Replace("/", " - "),
            Href = path
        }).ToList<NavItem>();
        navigationProviderMock.Setup(n => n.GetMainMenuItems()).Returns(navItems.AsReadOnly());

        return new PagePermissionService(
            dbContext,
            userManagerMock.Object,
            roleManager,
            navigationProviderMock.Object,
            NullLogger<PagePermissionService>.Instance);
    }

    // Feature: page-access-permissions, Property 7: PUT Idempotent Full Replacement
    /// <summary>
    /// Property: For any valid role and any subset of valid page paths, after calling
    /// UpdateRolePermissionsAsync with that list, querying the DB for that role's
    /// permissions returns exactly that list (no more, no less). Repeating the same
    /// call produces an identical result (idempotency).
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 2)]
    public Property UpdateRolePermissionsAsync_IsIdempotent_FullReplacement()
    {
        // Generate a random subset of valid page paths
        var subsetGen = Gen.SubListOf(ValidPagePool)
            .Select(paths => paths.ToList());

        return Prop.ForAll(Arb.From(subsetGen), (List<string> selectedPaths) =>
        {
            var (dbContext, connection) = CreateDbContext();
            try
            {
                // Create a non-Admin, non-system role in the test database
                var testRole = new ApplicationRole
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "TestRole",
                    NormalizedName = "TESTROLE",
                    IsSystem = false
                };
                dbContext.Roles.Add(testRole);
                dbContext.SaveChanges();

                var service = CreateServiceForUpdate(dbContext);

                // First call: update permissions with the selected paths
                service.UpdateRolePermissionsAsync(testRole.Id, selectedPaths)
                    .GetAwaiter().GetResult();

                // Verify: query DB for this role's permissions — should match exactly
                var permissionsAfterFirst = dbContext.PagePermissions
                    .Where(p => p.RoleId == testRole.Id)
                    .Select(p => p.PagePath)
                    .ToList();

                var firstResultSet = new HashSet<string>(permissionsAfterFirst, StringComparer.OrdinalIgnoreCase);
                var expectedSet = new HashSet<string>(selectedPaths, StringComparer.OrdinalIgnoreCase);

                var firstCallCorrect = firstResultSet.SetEquals(expectedSet);
                var firstCountCorrect = permissionsAfterFirst.Count == expectedSet.Count;

                // Second call: repeat the same operation (idempotency check)
                service.UpdateRolePermissionsAsync(testRole.Id, selectedPaths)
                    .GetAwaiter().GetResult();

                var permissionsAfterSecond = dbContext.PagePermissions
                    .Where(p => p.RoleId == testRole.Id)
                    .Select(p => p.PagePath)
                    .ToList();

                var secondResultSet = new HashSet<string>(permissionsAfterSecond, StringComparer.OrdinalIgnoreCase);
                var secondCallCorrect = secondResultSet.SetEquals(expectedSet);
                var secondCountCorrect = permissionsAfterSecond.Count == expectedSet.Count;

                return (firstCallCorrect && firstCountCorrect && secondCallCorrect && secondCountCorrect)
                    .Label($"Expected {expectedSet.Count} paths. " +
                           $"First call: {firstResultSet.Count} paths (match={firstCallCorrect}). " +
                           $"Second call: {secondResultSet.Count} paths (match={secondCallCorrect}).");
            }
            finally
            {
                dbContext.Dispose();
                connection.Dispose();
            }
        });
    }

    /// <summary>
    /// Property: For any user with multiple roles, the set of accessible pages returned by
    /// GetMyPagesAsync SHALL equal the union of all PagePermission records across all roles
    /// assigned to that user.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public Property GetMyPagesAsync_ReturnsExactUnion_OfAllRolePermissions()
    {
        // Generator for number of roles (2-5 roles)
        var roleCountGen = Gen.Choose(2, 5);

        // Generator for number of page paths per role (0-4 pages per role)
        var pageCountPerRoleGen = Gen.Choose(0, 4);

        // Pool of valid page paths to pick from
        var allPagePaths = new[]
        {
            "/dashboard", "/counter", "/admin/audit-log", "/admin/users",
            "/admin/roles", "/account/settings", "/account/profile", "/reports",
            "/admin/page-permissions", "/weather"
        };

        // Generator for a subset of page paths for a single role
        var pageSubsetGen = pageCountPerRoleGen.SelectMany(count =>
            Gen.ArrayOf(Gen.Elements(allPagePaths), Math.Min(count, allPagePaths.Length))
                .Select(arr => arr.Distinct(StringComparer.OrdinalIgnoreCase).ToList()));

        // Generator for the number of roles the user is assigned to (1 to roleCount)
        var gen = roleCountGen.SelectMany(roleCount =>
            Gen.ArrayOf(pageSubsetGen, roleCount).SelectMany(rolePages =>
                Gen.Choose(1, roleCount).Select(assignedCount =>
                    (roleCount, rolePages, assignedCount))));

        return Prop.ForAll(Arb.From(gen), ((int roleCount, List<string>[] rolePages, int assignedCount) input) =>
        {
            var (dbContext, connection) = CreateDbContext();
            try
            {
                // Create roles and seed them into the database
                var roles = new List<ApplicationRole>();
                for (var i = 0; i < input.roleCount; i++)
                {
                    var role = new ApplicationRole
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = $"Role_{i}",
                        NormalizedName = $"ROLE_{i}"
                    };
                    roles.Add(role);
                    dbContext.Roles.Add(role);
                }

                dbContext.SaveChanges();

                // Seed page permissions for each role
                for (var i = 0; i < input.roleCount; i++)
                {
                    foreach (var pagePath in input.rolePages[i])
                    {
                        dbContext.PagePermissions.Add(new PagePermission
                        {
                            RoleId = roles[i].Id,
                            PagePath = pagePath,
                            PageDisplayName = pagePath.TrimStart('/'),
                            Role = roles[i]
                        });
                    }
                }

                dbContext.SaveChanges();

                // Select a subset of roles to assign to the user
                var assignedRoles = roles.Take(input.assignedCount).ToList();
                var assignedRoleNames = assignedRoles.Select(r => r.Name!).ToList();

                // Create a test user
                var user = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = "testuser",
                    NormalizedUserName = "TESTUSER",
                    Email = "test@test.com",
                    NormalizedEmail = "TEST@TEST.COM"
                };

                // Create the service with mocked UserManager and real RoleManager
                var service = CreateService(dbContext, user, assignedRoleNames);

                // Act: call GetMyPagesAsync
                var result = service.GetMyPagesAsync(user.Id).GetAwaiter().GetResult();

                // Expected: union of all page paths across assigned roles (case-insensitive distinct)
                var expectedPages = assignedRoles
                    .Select(r => roles.IndexOf(r))
                    .SelectMany(idx => input.rolePages[idx])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var resultSet = new HashSet<string>(result, StringComparer.OrdinalIgnoreCase);

                // Verify exact equality of the sets
                var setsEqual = resultSet.SetEquals(expectedPages);

                return setsEqual.Label(
                    $"RoleCount={input.roleCount}, AssignedCount={input.assignedCount}, " +
                    $"ExpectedPages={expectedPages.Count}, ResultPages={resultSet.Count}, " +
                    $"Missing={string.Join(",", expectedPages.Except(resultSet, StringComparer.OrdinalIgnoreCase))}, " +
                    $"Extra={string.Join(",", resultSet.Except(expectedPages, StringComparer.OrdinalIgnoreCase))}");
            }
            finally
            {
                dbContext.Dispose();
                connection.Dispose();
            }
        });
    }
}
