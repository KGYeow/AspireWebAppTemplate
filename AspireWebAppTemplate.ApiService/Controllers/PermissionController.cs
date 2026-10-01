using AspireWebAppTemplate.Application.Features.Permissions;
using AspireWebAppTemplate.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AspireWebAppTemplate.ApiService.Controllers;

/// <summary>
/// Manages resource-based (<c>Module.Action</c>) authorization. Provides endpoints for
/// administrators to view the full permission catalog and a role's granted permissions,
/// to replace a role's permission grants, and for any authenticated user to query their own
/// effective permissions and the static page-to-module mapping.
/// </summary>
/// <remarks>
/// <para>
/// This controller is thin: it delegates all business logic to <see cref="IPermissionService"/>
/// and performs no data access, business validation, or audit logging (audit of permission
/// changes is a service-layer responsibility). Service exceptions are not caught here; they
/// propagate to the central <c>ExceptionMappingHandler</c>, which maps:
/// <list type="bullet">
///   <item><see cref="KeyNotFoundException"/> → 404 Not Found</item>
///   <item><see cref="InvalidOperationException"/> → 400 Bad Request</item>
///   <item><see cref="ArgumentException"/> → 400 Bad Request</item>
/// </list>
/// </para>
/// <para>
/// Management and catalog endpoints require the <c>Permissions.Manage</c> permission. The
/// self-service endpoints (<c>my-permissions</c> and <c>page-modules</c>) are reachable by any
/// authenticated user.
/// </para>
/// </remarks>
[Route("api/permissions")]
public class PermissionController : BaseController
{
    #region Constructor

    /// <summary>
    /// The permission service that implements the resource-based authorization model.
    /// </summary>
    private readonly IPermissionService _permissionService;

    /// <summary>
    /// The role manager, used to resolve a role's display name for the by-role response.
    /// </summary>
    private readonly RoleManager<ApplicationRole> _roleManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionController"/> class.
    /// </summary>
    /// <param name="permissionService">The permission service for querying and updating role grants.</param>
    /// <param name="roleManager">The role manager for resolving role display names.</param>
    public PermissionController(IPermissionService permissionService, RoleManager<ApplicationRole> roleManager)
    {
        _permissionService = permissionService;
        _roleManager = roleManager;
    }

    #endregion

    #region Permission Management

    /// <summary>
    /// Retrieves every defined permission grouped by its owning module, so the management UI can
    /// render the permission matrix with one row group per module.
    /// </summary>
    /// <returns>A list of <see cref="PermissionGroupDto"/>, one per module.</returns>
    /// <response code="200">Returns all permissions grouped by module.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User does not have the <c>Permissions.Manage</c> permission.</response>
    [HttpGet]
    [Authorize(Policy = "Permissions.Manage")]
    [ProducesResponseType(typeof(List<PermissionGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<PermissionGroupDto>>> GetAllPermissionsGrouped()
    {
        var groups = await _permissionService.GetAllPermissionsGroupedAsync();
        return Ok(groups);
    }

    /// <summary>
    /// Retrieves the permission keys currently granted to the specified role.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role whose grants are being queried.</param>
    /// <returns>
    /// A <see cref="RolePermissionsDto"/> containing the role's identifier, display name, and
    /// the list of granted permission keys.
    /// </returns>
    /// <response code="200">Returns the role's granted permission keys.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User does not have the <c>Permissions.Manage</c> permission.</response>
    /// <response code="404">The specified role was not found.</response>
    [HttpGet("roles/{roleId}")]
    [Authorize(Policy = "Permissions.Manage")]
    [ProducesResponseType(typeof(RolePermissionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RolePermissionsDto>> GetRolePermissions(string roleId)
    {
        // Service throws KeyNotFoundException for an unknown role, mapped to 404 by the central handler.
        var keys = await _permissionService.GetRolePermissionKeysAsync(roleId);

        // Resolve the display name for the response; the role is known to exist at this point.
        var role = await _roleManager.FindByIdAsync(roleId);

        return Ok(new RolePermissionsDto
        {
            RoleId = roleId,
            RoleName = role?.DisplayName ?? role?.Name ?? "",
            PermissionKeys = keys
        });
    }

    /// <summary>
    /// Replaces all permission grants for the specified role with the provided list of permission
    /// keys, applying a full-replacement strategy. An empty list removes all grants for the role.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role whose permissions are being updated.</param>
    /// <param name="request">The request body containing the complete list of permission keys to grant.</param>
    /// <returns>No content on success.</returns>
    /// <response code="200">Permissions were successfully updated.</response>
    /// <response code="400">
    /// The request is invalid: the role is the Admin role (immutable full access), or one or more
    /// permission keys do not correspond to a defined permission.
    /// </response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User does not have the <c>Permissions.Manage</c> permission.</response>
    /// <response code="404">The specified role was not found.</response>
    [HttpPut("roles/{roleId}")]
    [Authorize(Policy = "Permissions.Manage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRolePermissions(string roleId, [FromBody] UpdateRolePermissionsRequest request)
    {
        // Full replacement, validation, Admin-role protection, and audit logging all live in the service.
        await _permissionService.UpdateRolePermissionsAsync(roleId, request.PermissionKeys);
        return Ok();
    }

    #endregion

    #region Self-Service Queries

    /// <summary>
    /// Retrieves the effective permission keys for the currently authenticated user, computed as the
    /// union of all permissions granted to the user's assigned roles.
    /// </summary>
    /// <returns>
    /// A list of permission keys the current user effectively holds. Returns an empty list if the
    /// user has no assigned roles or no permissions are granted.
    /// </returns>
    /// <response code="200">Returns the current user's effective permission keys.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("my-permissions")]
    [Authorize]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<string>>> GetMyPermissions()
    {
        var permissions = await _permissionService.GetMyPermissionsAsync(CurrentUserId!);
        return Ok(permissions);
    }

    /// <summary>
    /// Retrieves the static mapping of admin page paths to the module that gates each page.
    /// Used by the Web project to drive module-based page visibility and access checks.
    /// </summary>
    /// <returns>A list of <see cref="PageModuleMappingDto"/> entries, one per known page path.</returns>
    /// <response code="200">Returns the page-to-module mappings.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("page-modules")]
    [Authorize]
    [ProducesResponseType(typeof(List<PageModuleMappingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<PageModuleMappingDto>>> GetPageModuleMappings()
    {
        var mappings = await _permissionService.GetPageModuleMappingsAsync();
        return Ok(mappings);
    }

    #endregion
}
