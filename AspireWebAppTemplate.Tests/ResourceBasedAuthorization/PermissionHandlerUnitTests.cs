// Feature: resource-based-authorization, Task 4.4: PermissionAuthorizationHandler unit tests
using System.Security.Claims;
using AspireWebAppTemplate.ApiService.Authorization;
using AspireWebAppTemplate.Application.Features.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace AspireWebAppTemplate.Tests.ResourceBasedAuthorization;

/// <summary>
/// Unit tests for <see cref="PermissionAuthorizationHandler"/>, covering the fail-closed branches,
/// the required-permission success path, and the per-request permission-set caching.
/// </summary>
/// <remarks>
/// <para>
/// These tests exercise the handler through its public <see cref="AuthorizationHandler{TRequirement}.HandleAsync"/>
/// entry point against an <see cref="AuthorizationHandlerContext"/> carrying a
/// <see cref="PermissionRequirement"/> and a test <see cref="ClaimsPrincipal"/>. The handler's
/// collaborators — <see cref="IHttpContextAccessor"/>, <see cref="IPermissionService"/>, and
/// <see cref="ILogger{T}"/> — are Moq doubles. A real <see cref="DefaultHttpContext"/> backs the
/// accessor so the per-request <see cref="HttpContext.Items"/> cache behaves as in production.
/// </para>
/// <para>
/// **Validates: Requirements 4.7, 4.8, 4.9, 5.6, 5.7.**
/// </para>
/// </remarks>
public class PermissionHandlerUnitTests
{
    #region Test Infrastructure

    /// <summary>
    /// The permission key under test; every requirement in these tests is built around this key.
    /// </summary>
    private const string RequiredKey = "Users.Read";

    /// <summary>
    /// Builds an authenticated <see cref="ClaimsPrincipal"/> carrying a NameIdentifier claim and
    /// the supplied role claims, using an authentication type so <c>IsAuthenticated</c> is true.
    /// </summary>
    /// <param name="userId">The user id placed in the <see cref="ClaimTypes.NameIdentifier"/> claim.</param>
    /// <param name="roles">The role names placed in <see cref="ClaimTypes.Role"/> claims.</param>
    /// <returns>An authenticated principal suitable for driving the handler.</returns>
    private static ClaimsPrincipal BuildAuthenticatedUser(string userId, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        // A non-null authentication type makes ClaimsIdentity.IsAuthenticated return true.
        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth", nameType: ClaimTypes.Name, roleType: ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Builds an unauthenticated <see cref="ClaimsPrincipal"/> (an identity with no authentication
    /// type, so <c>IsAuthenticated</c> is false).
    /// </summary>
    /// <returns>An anonymous principal.</returns>
    private static ClaimsPrincipal BuildAnonymousUser() => new(new ClaimsIdentity());

    /// <summary>
    /// Creates an <see cref="IHttpContextAccessor"/> mock whose <c>HttpContext</c> returns the
    /// supplied context (used so the per-request <see cref="HttpContext.Items"/> cache is shared).
    /// </summary>
    /// <param name="httpContext">The context the accessor should return.</param>
    /// <returns>The configured accessor mock.</returns>
    private static Mock<IHttpContextAccessor> CreateAccessor(HttpContext httpContext)
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return accessor;
    }

    /// <summary>
    /// Builds an <see cref="AuthorizationHandlerContext"/> for the supplied user carrying a single
    /// <see cref="PermissionRequirement"/> for <see cref="RequiredKey"/>.
    /// </summary>
    /// <param name="user">The principal under evaluation.</param>
    /// <returns>The authorization context plus the requirement instance it carries.</returns>
    private static (AuthorizationHandlerContext context, PermissionRequirement requirement) BuildContext(ClaimsPrincipal user)
    {
        var requirement = new PermissionRequirement(RequiredKey);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, resource: null);
        return (context, requirement);
    }

    #endregion

    #region Fail-Closed: Missing Permission

    /// <summary>
    /// An authenticated non-Admin user whose effective permission set does NOT contain the required
    /// key leaves the requirement unsatisfied (which the pipeline turns into a 403). (Req 4.7)
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenPermissionMissing_DoesNotSucceed()
    {
        var user = BuildAuthenticatedUser("user-1", "Viewer");
        var permissionService = new Mock<IPermissionService>();
        permissionService
            .Setup(s => s.GetMyPermissionsAsync("user-1"))
            .ReturnsAsync(new List<string> { "Roles.Read", "AuditLog.Read" });

        var handler = new PermissionAuthorizationHandler(
            CreateAccessor(new DefaultHttpContext()).Object,
            permissionService.Object,
            Mock.Of<ILogger<PermissionAuthorizationHandler>>());

        var (context, _) = BuildContext(user);
        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    #endregion

    #region Success: Has Required Permission

    /// <summary>
    /// An authenticated non-Admin user whose effective permission set contains the required key
    /// satisfies the requirement.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenPermissionPresent_Succeeds()
    {
        var user = BuildAuthenticatedUser("user-1", "Editor");
        var permissionService = new Mock<IPermissionService>();
        permissionService
            .Setup(s => s.GetMyPermissionsAsync("user-1"))
            .ReturnsAsync(new List<string> { "Users.Read", "Users.Update" });

        var handler = new PermissionAuthorizationHandler(
            CreateAccessor(new DefaultHttpContext()).Object,
            permissionService.Object,
            Mock.Of<ILogger<PermissionAuthorizationHandler>>());

        var (context, _) = BuildContext(user);
        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    /// <summary>
    /// Permission matching is case-insensitive: a stored key that differs only by casing from the
    /// required key still satisfies the requirement.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenPermissionPresentDifferentCase_Succeeds()
    {
        var user = BuildAuthenticatedUser("user-1", "Editor");
        var permissionService = new Mock<IPermissionService>();
        permissionService
            .Setup(s => s.GetMyPermissionsAsync("user-1"))
            .ReturnsAsync(new List<string> { "users.read" });

        var handler = new PermissionAuthorizationHandler(
            CreateAccessor(new DefaultHttpContext()).Object,
            permissionService.Object,
            Mock.Of<ILogger<PermissionAuthorizationHandler>>());

        var (context, _) = BuildContext(user);
        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    #endregion

    #region Fail-Closed: Unauthenticated

    /// <summary>
    /// An unauthenticated user (no authenticated identity) never satisfies a permission requirement,
    /// and the permission service is never consulted. (Req 4.9)
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenUnauthenticated_DoesNotSucceedAndSkipsLookup()
    {
        var user = BuildAnonymousUser();
        var permissionService = new Mock<IPermissionService>();

        var handler = new PermissionAuthorizationHandler(
            CreateAccessor(new DefaultHttpContext()).Object,
            permissionService.Object,
            Mock.Of<ILogger<PermissionAuthorizationHandler>>());

        var (context, _) = BuildContext(user);
        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        permissionService.Verify(s => s.GetMyPermissionsAsync(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region Fail-Closed: No Role Claims

    /// <summary>
    /// An authenticated user with a NameIdentifier but zero role claims has no granted permissions,
    /// so the requirement is left unsatisfied and the permission service is never queried. (Req 12.4)
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenNoRoleClaims_DoesNotSucceedAndSkipsLookup()
    {
        var user = BuildAuthenticatedUser("user-1");
        var permissionService = new Mock<IPermissionService>();

        var handler = new PermissionAuthorizationHandler(
            CreateAccessor(new DefaultHttpContext()).Object,
            permissionService.Object,
            Mock.Of<ILogger<PermissionAuthorizationHandler>>());

        var (context, _) = BuildContext(user);
        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        permissionService.Verify(s => s.GetMyPermissionsAsync(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region Fail-Closed: Load Error

    /// <summary>
    /// When loading the effective permission set throws, the handler fails closed (requirement not
    /// satisfied) and logs the failure at Error level. (Req 5.7)
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenPermissionLoadThrows_DoesNotSucceedAndLogsError()
    {
        var user = BuildAuthenticatedUser("user-1", "Editor");
        var permissionService = new Mock<IPermissionService>();
        permissionService
            .Setup(s => s.GetMyPermissionsAsync("user-1"))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var logger = new Mock<ILogger<PermissionAuthorizationHandler>>();

        var handler = new PermissionAuthorizationHandler(
            CreateAccessor(new DefaultHttpContext()).Object,
            permissionService.Object,
            logger.Object);

        var (context, _) = BuildContext(user);
        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Per-Request Cache Reuse

    /// <summary>
    /// Two requirement evaluations within the SAME HTTP request (sharing one <see cref="HttpContext"/>)
    /// resolve the permission set only once — the second evaluation reads the cached set from
    /// <see cref="HttpContext.Items"/>. (Req 4.8 / 5.3)
    /// </summary>
    [Fact]
    public async Task HandleAsync_TwoChecksInSameRequest_ResolvesPermissionsOnce()
    {
        var user = BuildAuthenticatedUser("user-1", "Editor");
        var permissionService = new Mock<IPermissionService>();
        permissionService
            .Setup(s => s.GetMyPermissionsAsync("user-1"))
            .ReturnsAsync(new List<string> { "Users.Read" });

        // One shared HttpContext across both evaluations so HttpContext.Items (the cache) persists.
        var sharedHttpContext = new DefaultHttpContext();
        var handler = new PermissionAuthorizationHandler(
            CreateAccessor(sharedHttpContext).Object,
            permissionService.Object,
            Mock.Of<ILogger<PermissionAuthorizationHandler>>());

        var (firstContext, _) = BuildContext(user);
        await handler.HandleAsync(firstContext);

        var (secondContext, _) = BuildContext(user);
        await handler.HandleAsync(secondContext);

        Assert.True(firstContext.HasSucceeded);
        Assert.True(secondContext.HasSucceeded);
        permissionService.Verify(s => s.GetMyPermissionsAsync("user-1"), Times.Once);
    }

    #endregion
}
