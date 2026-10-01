// Feature: resource-based-authorization, Property 1: Admin role bypasses all permission checks
using System.Security.Claims;
using AspireWebAppTemplate.ApiService.Authorization;
using AspireWebAppTemplate.Application.Features.Permissions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace AspireWebAppTemplate.Tests.ResourceBasedAuthorization;

/// <summary>
/// Property-based tests verifying that <see cref="PermissionAuthorizationHandler"/> grants access
/// to any user holding the <c>Admin</c> role for <em>any</em> permission requirement — including
/// keys that are malformed or do not correspond to any defined permission — and does so without
/// ever consulting <see cref="IPermissionService"/> (no database query).
/// </summary>
/// <remarks>
/// **Validates: Requirements 3.1, 3.2, 3.5, 4.2, 6.4, 13.3**
/// Property 1: for any permission key string, an authenticated Admin user always satisfies the
/// requirement (<c>context.HasSucceeded == true</c>) via the handler's role-name bypass, and the
/// permission service's resolution methods (<see cref="IPermissionService.GetMyPermissionsAsync"/>
/// and <see cref="IPermissionService.GetPermissionsForRolesAsync"/>) are never invoked. Random
/// permission keys are drawn from both constructively valid <c>Module.Action</c> shapes and from
/// targeted malformed / nonexistent shapes; the Admin bypass must hold regardless.
/// </remarks>
public class AdminBypassTests
{
    #region Generators

    /// <summary>
    /// Generates a single PascalCase segment: an uppercase first letter followed by 0–49
    /// alphanumeric characters.
    /// </summary>
    private static Gen<string> SegmentGen()
    {
        var upper = Gen.Elements("ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray());
        var alnum = Gen.Elements(
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray());

        return upper.SelectMany(first =>
            Gen.Choose(0, 49).SelectMany(tailLen =>
                Gen.ArrayOf(alnum, tailLen)
                    .Select(tail => first + new string(tail))));
    }

    /// <summary>
    /// Generates a well-formed <c>Module.Action</c> permission key. These keys are syntactically
    /// valid but need not correspond to any seeded/defined permission.
    /// </summary>
    private static Gen<string> ValidKeyGen()
    {
        return SegmentGen().SelectMany(module =>
            SegmentGen().Select(action => $"{module}.{action}"));
    }

    /// <summary>
    /// Generates permission keys that are malformed or clearly nonexistent: no dot, multiple dots,
    /// lowercase starts, empty segments, illegal characters, and arbitrarily long junk. The Admin
    /// bypass must succeed for these just the same.
    /// </summary>
    private static Gen<string> MalformedOrNonexistentKeyGen()
    {
        return Gen.OneOf(
            Gen.Elements("Users", "DoesNotExist", "ThisPermissionIsNotDefined", string.Empty),
            Gen.Elements("Users.Read.Extra", "A.B.C", ".Read", "Users.", ".", "..", "Users..Read"),
            Gen.Elements("users.read", "Users.read", "u.R", "U.r"),
            Gen.Elements("Users.Read!", "Users .Read", "User-s.Read", "Users.Re@d", "   ", "\t"),
            Gen.Elements(new string('A', 60) + "." + new string('B', 60), "Nonexistent.Action"));
    }

    /// <summary>
    /// Generates an arbitrary permission key spanning both valid and malformed/nonexistent shapes,
    /// so the Admin bypass is exercised across the whole key space.
    /// </summary>
    private static Gen<string> AnyKeyGen()
    {
        return Gen.OneOf(ValidKeyGen(), MalformedOrNonexistentKeyGen());
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Builds an authenticated <see cref="ClaimsPrincipal"/> that is a member of the <c>Admin</c>
    /// role. The identity is constructed with an explicit authentication type (so
    /// <see cref="System.Security.Principal.IIdentity.IsAuthenticated"/> is <see langword="true"/>)
    /// and the standard <see cref="ClaimTypes.Role"/> role-claim type (so
    /// <see cref="ClaimsPrincipal.IsInRole(string)"/> for <c>"Admin"</c> returns <see langword="true"/>).
    /// </summary>
    /// <returns>An authenticated Admin <see cref="ClaimsPrincipal"/> with a NameIdentifier claim.</returns>
    private static ClaimsPrincipal BuildAdminPrincipal()
    {
        var identity = new ClaimsIdentity(
            authenticationType: "TestAuth",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role);

        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, "admin-user"));
        identity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));

        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Constructs a <see cref="PermissionAuthorizationHandler"/> wired to a strict mock permission
    /// service whose resolution methods, if ever called, would fail the test. Returns the handler
    /// alongside the mock so the test can assert the service was never invoked.
    /// </summary>
    /// <param name="user">The principal whose HTTP context the handler should see.</param>
    /// <returns>The handler and the permission-service mock.</returns>
    private static (PermissionAuthorizationHandler Handler, Mock<IPermissionService> ServiceMock)
        BuildHandler(ClaimsPrincipal user)
    {
        var serviceMock = new Mock<IPermissionService>(MockBehavior.Strict);

        var httpContext = new DefaultHttpContext { User = user };
        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns(httpContext);

        var loggerMock = new Mock<ILogger<PermissionAuthorizationHandler>>();

        var handler = new PermissionAuthorizationHandler(
            accessorMock.Object,
            serviceMock.Object,
            loggerMock.Object);

        return (handler, serviceMock);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Property: for any permission key, an authenticated Admin user satisfies the requirement
    /// and the permission service is never queried.
    /// **Validates: Requirements 3.1, 3.2, 3.5, 4.2, 6.4, 13.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public FsCheck.Property AdminSucceedsForAnyKey_WithoutQueryingPermissionService()
    {
        return Prop.ForAll(
            Arb.From(AnyKeyGen()),
            (string permissionKey) =>
            {
                var user = BuildAdminPrincipal();
                var (handler, serviceMock) = BuildHandler(user);

                var requirement = new PermissionRequirement(permissionKey);
                var context = new AuthorizationHandlerContext(
                    new[] { requirement },
                    user,
                    resource: null);

                handler.HandleAsync(context).GetAwaiter().GetResult();

                // Admin must always satisfy the requirement regardless of the key.
                var succeeded = context.HasSucceeded;

                // The permission store must never be consulted for an Admin.
                var neverQueriedMyPermissions = WasNeverCalled(serviceMock,
                    s => s.GetMyPermissionsAsync(It.IsAny<string>()));
                var neverQueriedForRoles = WasNeverCalled(serviceMock,
                    s => s.GetPermissionsForRolesAsync(It.IsAny<IEnumerable<string>>()));

                return (succeeded && neverQueriedMyPermissions && neverQueriedForRoles).Label(
                    $"Key '{permissionKey}': HasSucceeded={succeeded}, " +
                    $"GetMyPermissions calls OK={neverQueriedMyPermissions}, " +
                    $"GetPermissionsForRoles calls OK={neverQueriedForRoles}");
            });
    }

    #endregion

    #region Verification Helpers

    /// <summary>
    /// Confirms the specified value-returning invocation never occurred on the mock, using Moq's
    /// <c>Verify(..., Times.Never)</c> as a boolean oracle: it returns <see langword="true"/> when
    /// the call never happened and <see langword="false"/> otherwise. The strict mock guarantees no
    /// unexpected calls slip through.
    /// </summary>
    /// <param name="mock">The permission-service mock to inspect.</param>
    /// <param name="expression">The invocation to assert was never made.</param>
    /// <returns><see langword="true"/> if the invocation never occurred; otherwise <see langword="false"/>.</returns>
    private static bool WasNeverCalled(
        Mock<IPermissionService> mock,
        System.Linq.Expressions.Expression<Func<IPermissionService, Task<List<string>>>> expression)
    {
        try
        {
            mock.Verify(expression, Times.Never);
            return true;
        }
        catch (MockException)
        {
            return false;
        }
    }

    #endregion
}
