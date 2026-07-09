using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.ApiService.Abstractions;
using AspireWebAppTemplate.ApiService.Data.Entities;
using AspireWebAppTemplate.ApiService.Utilities;
using AspireWebAppTemplate.Core.Contracts.AuditLog;
using AspireWebAppTemplate.Core.Contracts.PagePermissions;
using AspireWebAppTemplate.Core.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AspireWebAppTemplate.ApiService.Controllers;

/// <summary>
/// Manages role-based page access permissions using a whitelist model.
/// Provides endpoints for administrators to view and update role permissions,
/// and for authenticated users to query their own accessible pages.
/// </summary>
/// <remarks>
/// <para>
/// This controller delegates all business logic to <see cref="IPagePermissionService"/>.
/// Exception-to-HTTP-status mapping:
/// <list type="bullet">
///   <item><see cref="KeyNotFoundException"/> → 404 Not Found</item>
///   <item><see cref="InvalidOperationException"/> → 400 Bad Request</item>
///   <item><see cref="ArgumentException"/> → 400 Bad Request</item>
/// </list>
/// </para>
/// </remarks>
[Route("api/page-permissions")]
public class PagePermissionsController : BaseController
{
    #region Constructor

    private readonly IPagePermissionService _pagePermissionService;
    private readonly IAuditLogService _auditLogService;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<PagePermissionsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PagePermissionsController"/> class.
    /// </summary>
    /// <param name="pagePermissionService">The page permission service for managing role-page grants.</param>
    /// <param name="auditLogService">The audit log service for recording permission changes.</param>
    /// <param name="roleManager">The role manager for looking up role details.</param>
    /// <param name="logger">The logger instance for recording controller-level events.</param>
    public PagePermissionsController(
        IPagePermissionService pagePermissionService,
        IAuditLogService auditLogService,
        RoleManager<ApplicationRole> roleManager,
        ILogger<PagePermissionsController> logger)
    {
        _pagePermissionService = pagePermissionService;
        _auditLogService = auditLogService;
        _roleManager = roleManager;
        _logger = logger;
    }

    #endregion

    #region Endpoints

    /// <summary>
    /// Retrieves all page permission records grouped by role.
    /// </summary>
    /// <returns>
    /// A list of <see cref="RolePermissionsDto"/> objects, each containing the role's
    /// identifier, display name, and the list of granted pages with their display names.
    /// </returns>
    /// <response code="200">Returns all permissions grouped by role.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User does not have the required page permission.</response>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(List<RolePermissionsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<RolePermissionsDto>>> GetAllPermissions()
    {
        var permissions = await _pagePermissionService.GetAllPermissionsAsync();
        return Ok(permissions);
    }

    /// <summary>
    /// Replaces all page permissions for the specified role with the provided list of page paths.
    /// Uses a full-replacement strategy: the provided list becomes the complete set of permissions
    /// for the role. An empty list removes all page permissions for that role.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role whose permissions are being updated.</param>
    /// <param name="request">The request body containing the complete list of page paths to grant.</param>
    /// <returns>No content on success.</returns>
    /// <response code="200">Permissions were successfully updated.</response>
    /// <response code="400">
    /// The request is invalid: the role is a system role, the role is the Admin role,
    /// or one or more page paths are not registered in the navigation provider.
    /// </response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User does not have the required page permission.</response>
    /// <response code="404">The specified role was not found.</response>
    [HttpPut("{roleId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRolePermissions(string roleId, [FromBody] UpdateRolePermissionsRequest request)
    {
        try
        {
            // Look up the role to get display name for the audit entry
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role is null)
                return NotFound($"Role with ID '{roleId}' was not found.");

            // Capture previous page paths for the role before the update
            var allPermissions = await _pagePermissionService.GetAllPermissionsAsync();
            var rolePermissions = allPermissions.FirstOrDefault(rp => rp.RoleId == roleId);
            var previousPaths = rolePermissions?.Pages.Select(p => p.PagePath).ToList() ?? new List<string>();

            // Perform the update
            await _pagePermissionService.UpdateRolePermissionsAsync(roleId, request.PagePaths);

            // Log the audit entry with old/new values
            await _auditLogService.LogAsync(new AuditLogRequest
            {
                UserId = CurrentUserId,
                ActionType = AuditActionType.SettingsChanged,
                EntityType = AuditEntityType.Role,
                EntityId = roleId,
                EntityName = role.DisplayName ?? role.Name ?? "",
                Description = $"Page permissions for role '{role.DisplayName ?? role.Name}' were updated.",
                OldValues = AuditChangeHelper.Serialize(new { PagePaths = previousPaths }),
                NewValues = AuditChangeHelper.Serialize(new { PagePaths = request.PagePaths }),
                IpAddress = ClientIpAddress
            });

            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            // Role not found in AspNetRoles — return 404.
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Admin role or system role modification attempted — return 400.
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            // Invalid page paths provided — return 400 with details.
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Retrieves the list of page paths accessible to the currently authenticated user,
    /// based on the union of all page permissions across all roles assigned to that user.
    /// </summary>
    /// <returns>
    /// A list of page path strings the current user is permitted to access.
    /// Returns an empty list if the user has no assigned roles or no permissions are granted.
    /// </returns>
    /// <response code="200">Returns the list of accessible page paths for the current user.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("my-pages")]
    [Authorize]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<string>>> GetMyPages()
    {
        // Retrieve the authenticated user's ID from the JWT/cookie claims.
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId))
        {
            // This shouldn't happen with [Authorize] in place, but guard defensively.
            return Unauthorized();
        }

        var pages = await _pagePermissionService.GetMyPagesAsync(userId);
        return Ok(pages);
    }

    #endregion
}
