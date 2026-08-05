using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Contracts.Roles;
using AspireWebAppTemplate.Application.Contracts.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspireWebAppTemplate.ApiService.Controllers;

/// <summary>
/// Manages application roles including CRUD operations, activation/deactivation,
/// and user-role assignment. This controller is intentionally thin — it handles HTTP
/// concerns only (request parsing, status code mapping) and delegates all business logic
/// to <see cref="IRoleService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Exception-to-HTTP-status mapping:
/// <list type="bullet">
///   <item><see cref="KeyNotFoundException"/> → 404 Not Found</item>
///   <item><see cref="InvalidOperationException"/> → 400 Bad Request</item>
///   <item><see cref="ArgumentException"/> → 400 Bad Request</item>
/// </list>
/// </para>
/// </remarks>
[Route("api/[controller]")]
[Authorize]
public class RolesController : BaseController
{
    #region Constructor

    private readonly IRoleService _roleService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RolesController"/> class.
    /// </summary>
    /// <param name="roleService">The role service for managing all role operations.</param>
    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    #endregion

    #region CRUD Operations

    /// <summary>
    /// Returns all roles with their user counts, ordered by position descending then name ascending.
    /// </summary>
    /// <returns>A list of all roles in the system with user counts.</returns>
    /// <response code="200">Returns the list of roles.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RoleDto>>> GetRoles()
    {
        var roles = await _roleService.GetAllAsync();
        return Ok(roles);
    }

    /// <summary>
    /// Returns a single role by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the role to retrieve.</param>
    /// <returns>The role matching the specified ID.</returns>
    /// <response code="200">Returns the role.</response>
    /// <response code="404">No role exists with the specified ID.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDto>> GetRole(string id)
    {
        try
        {
            var role = await _roleService.GetByIdAsync(id);
            return Ok(role);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>
    /// Creates a new role in the system.
    /// </summary>
    /// <param name="request">The role creation request containing name, display name, description, position, and active status.</param>
    /// <returns>The newly created role.</returns>
    /// <response code="201">The role was created successfully.</response>
    /// <response code="400">Validation failed (e.g., duplicate role name).</response>
    [HttpPost]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoleDto>> CreateRole([FromBody] CreateRoleRequest request)
    {
        try
        {
            var role = await _roleService.CreateAsync(request);
            return CreatedAtAction(nameof(GetRole), new { id = role.Id }, role);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Updates an existing role with the provided data.
    /// </summary>
    /// <param name="id">The unique identifier of the role to update.</param>
    /// <param name="request">The role update request containing the new property values.</param>
    /// <returns>200 OK on success.</returns>
    /// <response code="200">The role was updated successfully.</response>
    /// <response code="404">No role exists with the specified ID.</response>
    /// <response code="400">Business rule violation (system role, validation failure).</response>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateRole(string id, [FromBody] CreateRoleRequest request)
    {
        try
        {
            await _roleService.UpdateAsync(id, request);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Deletes an existing role from the system.
    /// </summary>
    /// <param name="id">The unique identifier of the role to delete.</param>
    /// <returns>200 OK on success.</returns>
    /// <response code="200">The role was deleted successfully.</response>
    /// <response code="404">No role exists with the specified ID.</response>
    /// <response code="400">Business rule violation (system role, users still assigned).</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteRole(string id)
    {
        try
        {
            await _roleService.DeleteAsync(id);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    #endregion

    #region Activation

    /// <summary>
    /// Activates a role, setting its IsActive status to true.
    /// </summary>
    /// <param name="id">The unique identifier of the role to activate.</param>
    /// <returns>200 OK on success.</returns>
    /// <response code="200">The role was activated successfully.</response>
    /// <response code="404">No role exists with the specified ID.</response>
    /// <response code="400">Business rule violation (system role cannot be modified).</response>
    [HttpPost("{id}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActivateRole(string id)
    {
        try
        {
            await _roleService.ActivateAsync(id);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Deactivates a role, setting its IsActive status to false.
    /// </summary>
    /// <param name="id">The unique identifier of the role to deactivate.</param>
    /// <returns>200 OK on success.</returns>
    /// <response code="200">The role was deactivated successfully.</response>
    /// <response code="404">No role exists with the specified ID.</response>
    /// <response code="400">Business rule violation (system role cannot be modified).</response>
    [HttpPost("{id}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeactivateRole(string id)
    {
        try
        {
            await _roleService.DeactivateAsync(id);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    #endregion

    #region User-Role Assignment

    /// <summary>
    /// Assigns one or more users to a role in bulk. Each user assignment is attempted
    /// independently — individual failures do not prevent other assignments from succeeding.
    /// </summary>
    /// <param name="id">The unique identifier of the role to assign users to.</param>
    /// <param name="userIds">An array of user identifiers to assign to the role.</param>
    /// <returns>A result containing the count of successful and failed assignments.</returns>
    /// <response code="200">Returns the assignment result with success/failed counts.</response>
    /// <response code="404">No role exists with the specified ID.</response>
    [HttpPost("{id}/users")]
    [ProducesResponseType(typeof(RoleAssignmentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignUsersToRole(string id, [FromBody] string[] userIds)
    {
        try
        {
            var result = await _roleService.AssignUsersAsync(id, userIds);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Removes a single user from a role.
    /// </summary>
    /// <param name="id">The unique identifier of the role to remove the user from.</param>
    /// <param name="userId">The unique identifier of the user to remove from the role.</param>
    /// <returns>200 OK on success.</returns>
    /// <response code="200">The user was removed from the role successfully.</response>
    /// <response code="404">No role or user exists with the specified ID.</response>
    /// <response code="400">Business rule violation (last user in required-minimum role).</response>
    [HttpDelete("{id}/users/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveUserFromRole(string id, string userId)
    {
        try
        {
            await _roleService.RemoveUserAsync(id, userId);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Returns the list of users currently assigned to a specific role.
    /// </summary>
    /// <param name="id">The unique identifier of the role whose users are being queried.</param>
    /// <returns>A list of users assigned to the specified role.</returns>
    /// <response code="200">Returns the list of users in the role.</response>
    /// <response code="404">No role exists with the specified ID.</response>
    [HttpGet("{id}/users")]
    [ProducesResponseType(typeof(List<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<UserDto>>> GetUsersInRole(string id)
    {
        try
        {
            var users = await _roleService.GetUsersInRoleAsync(id);
            return Ok(users);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    #endregion
}
