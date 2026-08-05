// Feature: page-access-permissions, Property 2: Validation Rejects Invalid PagePaths
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services;
using AspireWebAppTemplate.Application.Common;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AspireWebAppTemplate.Tests.PagePermissions.Properties;

/// <summary>
/// Property-based tests verifying that the PagePermissionService rejects invalid page paths
/// that do not start with "/" or are not registered in the navigation provider.
/// </summary>
/// <remarks>
/// **Validates: Requirements 1.7**
/// </remarks>
public class PagePermissionValidationPropertyTests
{
    /// <summary>
    /// Known valid pages returned by the mocked INavigationProvider.
    /// These are intentionally specific paths that random strings are extremely unlikely to match.
    /// </summary>
    private static readonly List<NavItem> KnownValidPages =
    [
        new NavItem { Type = NavItemType.Link, Text = "Dashboard", Href = "/dashboard" },
        new NavItem { Type = NavItemType.Link, Text = "Settings", Href = "/admin/settings" },
        new NavItem { Type = NavItemType.Link, Text = "Users", Href = "/admin/users" }
    ];

    /// <summary>
    /// Creates a SQLite in-memory ApplicationDbContext for testing.
    /// FK enforcement is disabled so we can test without full Identity scaffolding.
    /// </summary>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

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
    /// Creates a mocked INavigationProvider that returns a known set of valid pages.
    /// </summary>
    private static INavigationProvider CreateMockNavigationProvider()
    {
        var mock = new Mock<INavigationProvider>();
        mock.Setup(x => x.GetMainMenuItems()).Returns(KnownValidPages);
        return mock.Object;
    }

    /// <summary>
    /// Creates a mocked RoleManager that recognizes a single valid non-Admin, non-system role.
    /// </summary>
    private static RoleManager<ApplicationRole> CreateMockRoleManager(string roleId, string roleName)
    {
        var store = new Mock<IRoleStore<ApplicationRole>>();
        var roleManager = new Mock<RoleManager<ApplicationRole>>(
            store.Object, null!, null!, null!, null!);

        roleManager.Setup(x => x.FindByIdAsync(roleId))
            .ReturnsAsync(new ApplicationRole
            {
                Id = roleId,
                Name = roleName,
                NormalizedName = roleName.ToUpperInvariant(),
                IsSystem = false
            });

        return roleManager.Object;
    }

    /// <summary>
    /// Creates a mocked UserManager (not used by UpdateRolePermissionsAsync but required by constructor).
    /// </summary>
    private static UserManager<ApplicationUser> CreateMockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!).Object;
    }

    /// <summary>
    /// Property: For any string that does not start with "/" or is not registered in the
    /// navigation provider, calling UpdateRolePermissionsAsync SHALL throw an ArgumentException
    /// indicating the path is invalid.
    /// **Validates: Requirements 1.7**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property InvalidPaths_AreRejectedWithArgumentException()
    {
        // Generator for invalid page paths:
        // - Paths missing leading "/" (e.g., "dashboard", "admin/page")
        // - Paths exceeding 256 characters
        // - Paths with query strings (e.g., "/page?param=value")
        // - Paths with fragments (e.g., "/page#section")
        // All of these should NOT match any page in the known navigation provider.
        var missingSlashGen = Gen.Elements("dashboard", "admin/users", "page", "settings/general", "a")
            .Select(s => s.TrimStart('/'));

        var longPathGen = Gen.Choose(257, 300)
            .Select(len => "/" + new string('x', len - 1));

        var queryStringGen = Gen.Elements("/unknown-page?param=value", "/fake?q=1&b=2", "/test?x=y");

        var fragmentGen = Gen.Elements("/unknown-page#section", "/fake#top", "/test#anchor");

        var invalidPathGen = Gen.OneOf(missingSlashGen, longPathGen, queryStringGen, fragmentGen);

        return Prop.ForAll(
            Arb.From(invalidPathGen),
            (string invalidPath) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    var roleId = "test-role-id";
                    var roleName = "TestRole";

                    var service = new PagePermissionService(
                        dbContext,
                        CreateMockUserManager(),
                        CreateMockRoleManager(roleId, roleName),
                        CreateMockNavigationProvider(),
                        Mock.Of<ILogger<PagePermissionService>>());

                    // Attempt to update permissions with the invalid path
                    var exception = Record.ExceptionAsync(async () =>
                        await service.UpdateRolePermissionsAsync(roleId, [invalidPath])).Result;

                    // The service should reject the invalid path with ArgumentException
                    var isArgumentException = exception is ArgumentException;
                    var containsInvalidPath = exception?.Message.Contains(invalidPath,
                        StringComparison.OrdinalIgnoreCase) ?? false;

                    return (isArgumentException && containsInvalidPath).Label(
                        $"Expected ArgumentException containing '{invalidPath}'. " +
                        $"Got: {exception?.GetType().Name ?? "no exception"} - {exception?.Message ?? "N/A"}");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
