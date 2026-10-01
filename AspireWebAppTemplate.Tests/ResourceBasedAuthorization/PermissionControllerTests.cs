// Feature: resource-based-authorization, Task 5.6: PermissionController unit tests
using System.Security.Claims;
using AspireWebAppTemplate.ApiService.Controllers;
using AspireWebAppTemplate.Application.Features.Permissions;
using AspireWebAppTemplate.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AspireWebAppTemplate.Tests.ResourceBasedAuthorization;

/// <summary>
/// Unit tests for <see cref="PermissionController"/> verifying its thin-controller contract:
/// delegation to <see cref="IPermissionService"/> and <see cref="RoleManager{ApplicationRole}"/>,
/// HTTP result shapes (200 OK with the service payload), the <see cref="RolePermissionsDto"/>
/// projection, use of <c>CurrentUserId</c> for the self-service query, and the fact that service
/// exceptions are not caught (they propagate to the central <c>ExceptionMappingHandler</c>).
/// </summary>
/// <remarks>
/// **Validates: Requirements 8.3, 8.5, 8.6, 8.7**
/// </remarks>
public class PermissionControllerTests
{
    #region Constructor

    /// <summary>
    /// Mock for the permission service the controller delegates all query/update logic to.
    /// </summary>
    private readonly Mock<IPermissionService> _mockPermissionService;

    /// <summary>
    /// Mock for the role manager used to resolve a role's display name in the by-role response.
    /// </summary>
    private readonly Mock<RoleManager<ApplicationRole>> _mockRoleManager;

    /// <summary>
    /// The controller under test.
    /// </summary>
    private readonly PermissionController _controller;

    /// <summary>
    /// Initializes test fixtures with mocked dependencies and the controller instance.
    /// </summary>
    public PermissionControllerTests()
    {
        _mockPermissionService = new Mock<IPermissionService>();

        var roleStore = new Mock<IRoleStore<ApplicationRole>>();
        _mockRoleManager = new Mock<RoleManager<ApplicationRole>>(
            roleStore.Object, null!, null!, null!, null!);

        _controller = new PermissionController(_mockPermissionService.Object, _mockRoleManager.Object);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Assigns an authenticated <see cref="ClaimsPrincipal"/> carrying the supplied user id as the
    /// <see cref="ClaimTypes.NameIdentifier"/> claim to the controller, so <c>CurrentUserId</c> resolves.
    /// </summary>
    /// <param name="userId">The user identifier to expose via the name-identifier claim.</param>
    private void SetCurrentUser(string userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)],
            authenticationType: "Test");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    #endregion

    #region GetAllPermissionsGrouped Tests

    /// <summary>
    /// Verifies that GetAllPermissionsGrouped returns 200 OK with the grouped list from the service.
    /// **Validates: Requirement 8.3**
    /// </summary>
    [Fact]
    public async Task GetAllPermissionsGrouped_ReturnsOkWithGroupedList()
    {
        // Arrange
        var groups = new List<PermissionGroupDto>
        {
            new()
            {
                Module = "Users",
                Permissions = [new PermissionDto { Id = 1, Key = "Users.Read", DisplayName = "View Users" }]
            },
            new()
            {
                Module = "Roles",
                Permissions = [new PermissionDto { Id = 2, Key = "Roles.Manage", DisplayName = "Manage Roles" }]
            }
        };
        _mockPermissionService.Setup(s => s.GetAllPermissionsGroupedAsync()).ReturnsAsync(groups);

        // Act
        var result = await _controller.GetAllPermissionsGrouped();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, okResult.StatusCode);
        var returned = Assert.IsType<List<PermissionGroupDto>>(okResult.Value);
        Assert.Same(groups, returned);
        Assert.Equal(2, returned.Count);
        _mockPermissionService.Verify(s => s.GetAllPermissionsGroupedAsync(), Times.Once);
    }

    #endregion

    #region GetRolePermissions Tests

    /// <summary>
    /// Verifies that GetRolePermissions returns 200 OK with a <see cref="RolePermissionsDto"/> that
    /// wraps the service's permission keys and the role name resolved via <c>RoleManager.FindByIdAsync</c>,
    /// preferring the role's <c>DisplayName</c>.
    /// **Validates: Requirements 8.5, 8.6**
    /// </summary>
    [Fact]
    public async Task GetRolePermissions_WhenRoleExists_ReturnsOkWithProjectedDto()
    {
        // Arrange
        const string roleId = "role-123";
        var keys = new List<string> { "Users.Read", "Users.Edit" };
        var role = new ApplicationRole { Id = roleId, Name = "editor", DisplayName = "Editor" };

        _mockPermissionService.Setup(s => s.GetRolePermissionKeysAsync(roleId)).ReturnsAsync(keys);
        _mockRoleManager.Setup(m => m.FindByIdAsync(roleId)).ReturnsAsync(role);

        // Act
        var result = await _controller.GetRolePermissions(roleId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, okResult.StatusCode);
        var dto = Assert.IsType<RolePermissionsDto>(okResult.Value);
        Assert.Equal(roleId, dto.RoleId);
        Assert.Equal("Editor", dto.RoleName);
        Assert.Same(keys, dto.PermissionKeys);
        _mockPermissionService.Verify(s => s.GetRolePermissionKeysAsync(roleId), Times.Once);
        _mockRoleManager.Verify(m => m.FindByIdAsync(roleId), Times.Once);
    }

    /// <summary>
    /// Verifies that GetRolePermissions falls back to the role's <c>Name</c> when <c>DisplayName</c>
    /// is null, matching the controller's projection logic.
    /// **Validates: Requirement 8.6**
    /// </summary>
    [Fact]
    public async Task GetRolePermissions_WhenDisplayNameNull_FallsBackToRoleName()
    {
        // Arrange
        const string roleId = "role-456";
        var keys = new List<string> { "Roles.Read" };
        var role = new ApplicationRole { Id = roleId, Name = "viewer", DisplayName = null };

        _mockPermissionService.Setup(s => s.GetRolePermissionKeysAsync(roleId)).ReturnsAsync(keys);
        _mockRoleManager.Setup(m => m.FindByIdAsync(roleId)).ReturnsAsync(role);

        // Act
        var result = await _controller.GetRolePermissions(roleId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<RolePermissionsDto>(okResult.Value);
        Assert.Equal("viewer", dto.RoleName);
    }

    /// <summary>
    /// Verifies that GetRolePermissions does NOT catch a <see cref="KeyNotFoundException"/> thrown by
    /// the service; it rethrows the same exception so the central handler maps it to 404.
    /// **Validates: Requirement 8.5**
    /// </summary>
    [Fact]
    public async Task GetRolePermissions_WhenServiceThrowsKeyNotFound_Propagates()
    {
        // Arrange
        const string roleId = "missing-role";
        var thrown = new KeyNotFoundException("Role not found.");
        _mockPermissionService.Setup(s => s.GetRolePermissionKeysAsync(roleId)).ThrowsAsync(thrown);

        // Act & Assert: the controller does not convert the exception; it propagates to the
        // central ExceptionMappingHandler, which maps KeyNotFoundException to 404 at the HTTP layer.
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => _controller.GetRolePermissions(roleId));
        Assert.Same(thrown, exception);
        _mockRoleManager.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region GetMyPermissions Tests

    /// <summary>
    /// Verifies that GetMyPermissions returns 200 OK with the service's list computed for the
    /// authenticated user's id (<c>CurrentUserId</c>).
    /// **Validates: Requirement 8.7**
    /// </summary>
    [Fact]
    public async Task GetMyPermissions_ReturnsOkWithPermissionsForCurrentUser()
    {
        // Arrange
        const string userId = "user-789";
        SetCurrentUser(userId);
        var permissions = new List<string> { "Users.Read", "Announcements.Read" };
        _mockPermissionService.Setup(s => s.GetMyPermissionsAsync(userId)).ReturnsAsync(permissions);

        // Act
        var result = await _controller.GetMyPermissions();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, okResult.StatusCode);
        var returned = Assert.IsType<List<string>>(okResult.Value);
        Assert.Same(permissions, returned);
        _mockPermissionService.Verify(s => s.GetMyPermissionsAsync(userId), Times.Once);
    }

    #endregion

    #region GetPageModuleMappings Tests

    /// <summary>
    /// Verifies that GetPageModuleMappings returns 200 OK with the mapping list from the service.
    /// **Validates: Requirement 8.7**
    /// </summary>
    [Fact]
    public async Task GetPageModuleMappings_ReturnsOkWithMappingList()
    {
        // Arrange
        var mappings = new List<PageModuleMappingDto>
        {
            new() { PagePath = "/admin/user-management", Module = "Users" },
            new() { PagePath = "/admin/role-management", Module = "Roles" }
        };
        _mockPermissionService.Setup(s => s.GetPageModuleMappingsAsync()).ReturnsAsync(mappings);

        // Act
        var result = await _controller.GetPageModuleMappings();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, okResult.StatusCode);
        var returned = Assert.IsType<List<PageModuleMappingDto>>(okResult.Value);
        Assert.Same(mappings, returned);
        Assert.Equal(2, returned.Count);
        _mockPermissionService.Verify(s => s.GetPageModuleMappingsAsync(), Times.Once);
    }

    #endregion

    #region UpdateRolePermissions Tests

    /// <summary>
    /// Verifies that UpdateRolePermissions delegates the full-replacement update to the service and
    /// returns 200 OK on success.
    /// **Validates: Requirement 8.5**
    /// </summary>
    [Fact]
    public async Task UpdateRolePermissions_WhenSuccessful_DelegatesAndReturnsOk()
    {
        // Arrange
        const string roleId = "role-123";
        var request = new UpdateRolePermissionsRequest
        {
            PermissionKeys = ["Users.Read", "Users.Edit"]
        };
        _mockPermissionService
            .Setup(s => s.UpdateRolePermissionsAsync(roleId, request.PermissionKeys))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.UpdateRolePermissions(roleId, request);

        // Assert
        var okResult = Assert.IsType<OkResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        _mockPermissionService.Verify(s => s.UpdateRolePermissionsAsync(roleId, request.PermissionKeys), Times.Once);
    }

    /// <summary>
    /// Verifies that UpdateRolePermissions does NOT catch an <see cref="InvalidOperationException"/>
    /// (e.g. attempting to modify the Admin role); it rethrows the same exception so the central
    /// handler maps it to 400.
    /// **Validates: Requirement 8.5**
    /// </summary>
    [Fact]
    public async Task UpdateRolePermissions_WhenServiceThrowsInvalidOperation_Propagates()
    {
        // Arrange
        const string roleId = "admin-role";
        var request = new UpdateRolePermissionsRequest { PermissionKeys = ["Users.Read"] };
        var thrown = new InvalidOperationException("The Admin role cannot be modified.");
        _mockPermissionService
            .Setup(s => s.UpdateRolePermissionsAsync(roleId, request.PermissionKeys))
            .ThrowsAsync(thrown);

        // Act & Assert: the controller does not convert the exception; it propagates to the
        // central ExceptionMappingHandler, which maps InvalidOperationException to 400 at the HTTP layer.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _controller.UpdateRolePermissions(roleId, request));
        Assert.Same(thrown, exception);
    }

    /// <summary>
    /// Verifies that UpdateRolePermissions does NOT catch an <see cref="ArgumentException"/>
    /// (e.g. an unknown permission key); it rethrows the same exception so the central handler
    /// maps it to 400.
    /// **Validates: Requirement 8.5**
    /// </summary>
    [Fact]
    public async Task UpdateRolePermissions_WhenServiceThrowsArgument_Propagates()
    {
        // Arrange
        const string roleId = "role-123";
        var request = new UpdateRolePermissionsRequest { PermissionKeys = ["Not.AKey"] };
        var thrown = new ArgumentException("One or more permission keys are invalid.");
        _mockPermissionService
            .Setup(s => s.UpdateRolePermissionsAsync(roleId, request.PermissionKeys))
            .ThrowsAsync(thrown);

        // Act & Assert: the controller does not convert the exception; it propagates to the
        // central ExceptionMappingHandler, which maps ArgumentException to 400 at the HTTP layer.
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _controller.UpdateRolePermissions(roleId, request));
        Assert.Same(thrown, exception);
    }

    #endregion
}
