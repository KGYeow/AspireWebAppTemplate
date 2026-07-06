using System.Text.Json;
using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.ApiService.Abstractions;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Contracts.Roles;
using AspireWebAppTemplate.Core.Contracts.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspireWebAppTemplate.ApiService.Controllers;

/// <summary>
/// Provides user management endpoints including CRUD operations, activation/deactivation,
/// role assignment, and LDAP synchronization. This controller is intentionally thin — it
/// handles HTTP concerns only (request parsing, status code mapping) and delegates all
/// business logic to <see cref="IUserService"/>.
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
[Authorize(Roles = "Admin")]
public class UsersController : BaseController
{
    #region Constructor

    private readonly IUserService _userService;
    private readonly ILdapAuthService _ldapAuthService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersController"/> class.
    /// </summary>
    /// <param name="userService">The user service for managing user lifecycle operations.</param>
    /// <param name="ldapAuthService">The LDAP auth service for directory attribute lookups.</param>
    public UsersController(IUserService userService, ILdapAuthService ldapAuthService)
    {
        _userService = userService;
        _ldapAuthService = ldapAuthService;
    }

    #endregion

    #region CRUD Operations

    /// <summary>
    /// Returns a list of users with their assigned roles.
    /// When page/pageSize are provided, returns a paged subset; otherwise returns all users.
    /// </summary>
    /// <param name="page">The zero-based page index. Defaults to null (return all).</param>
    /// <param name="pageSize">The maximum number of items per page. Defaults to null (return all).</param>
    /// <param name="searchTerm">Optional search term for filtering users by username, display name, email, first name, last name, or department.</param>
    /// <returns>A paged result containing matching users and total count metadata.</returns>
    /// <response code="200">Returns the paged user list.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UserDto>>> GetUsers(
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? searchTerm = null)
    {
        var result = await _userService.SearchAsync(page, pageSize, searchTerm);
        return Ok(result);
    }

    /// <summary>
    /// Returns a single user by ID, including roles and all profile fields.
    /// </summary>
    /// <param name="id">The unique identifier of the user to retrieve.</param>
    /// <returns>The user's full profile as a <see cref="UserDto"/>.</returns>
    /// <response code="200">Returns the user profile.</response>
    /// <response code="404">No user exists with the specified ID.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetUser(string id)
    {
        try
        {
            var user = await _userService.GetByIdAsync(id);
            return Ok(user);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>
    /// Creates a new user account with the specified email, display name, password, and optional role.
    /// </summary>
    /// <param name="request">The user creation request containing email, display name, password, and optional role.</param>
    /// <returns>The newly created user as a <see cref="UserDto"/>.</returns>
    /// <response code="201">The user was created successfully.</response>
    /// <response code="400">Validation failed (duplicate email, password policy violation, etc.).</response>
    [HttpPost]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var user = await _userService.CreateAsync(request);
            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Updates an existing user's profile information (display name, email, phone, etc.).
    /// Only non-null fields in the request are applied.
    /// </summary>
    /// <param name="id">The unique identifier of the user to update.</param>
    /// <param name="request">The update request containing the fields to modify.</param>
    /// <returns>200 OK on success.</returns>
    /// <response code="200">The user was updated successfully.</response>
    /// <response code="400">Validation failed (duplicate email, etc.).</response>
    /// <response code="404">No user exists with the specified ID.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            await _userService.UpdateAsync(id, request);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Permanently deletes a user account. Cannot delete the currently authenticated user
    /// or the last active administrator.
    /// </summary>
    /// <param name="id">The unique identifier of the user to delete.</param>
    /// <returns>200 OK on success.</returns>
    /// <response code="200">The user was deleted successfully.</response>
    /// <response code="400">Deletion blocked (self-deletion, last admin, etc.).</response>
    /// <response code="404">No user exists with the specified ID.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteUser(string id)
    {
        try
        {
            await _userService.DeleteAsync(id);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    #endregion

    #region Activation

    /// <summary>
    /// Activates a user account, allowing the user to sign in.
    /// </summary>
    /// <param name="id">The unique identifier of the user to activate.</param>
    /// <returns>200 OK on success.</returns>
    /// <response code="200">The user was activated successfully.</response>
    /// <response code="404">No user exists with the specified ID.</response>
    [HttpPost("{id}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateUser(string id)
    {
        try
        {
            await _userService.ActivateAsync(id);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>
    /// Deactivates a user account, preventing the user from signing in.
    /// Cannot deactivate the currently authenticated user.
    /// </summary>
    /// <param name="id">The unique identifier of the user to deactivate.</param>
    /// <returns>200 OK on success.</returns>
    /// <response code="200">The user was deactivated successfully.</response>
    /// <response code="400">Deactivation blocked (self-deactivation).</response>
    /// <response code="404">No user exists with the specified ID.</response>
    [HttpPost("{id}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeactivateUser(string id)
    {
        try
        {
            await _userService.DeactivateAsync(id);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Resets a user's password to a new value specified by an administrator.
    /// Notifies the affected user via in-app notification.
    /// </summary>
    /// <param name="id">The unique identifier of the user whose password is being reset.</param>
    /// <param name="request">The request containing the new password.</param>
    /// <returns>200 OK on success.</returns>
    /// <response code="200">The user's password was reset successfully.</response>
    /// <response code="400">Password reset failed (policy violation).</response>
    /// <response code="404">No user exists with the specified ID.</response>
    [HttpPost("{id}/reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(string id, [FromBody] AdminResetPasswordRequest request)
    {
        try
        {
            await _userService.ResetPasswordAsync(id, request.NewPassword);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    #endregion

    #region Roles

    /// <summary>
    /// Sets the roles for a user, replacing all existing role assignments with the provided set.
    /// </summary>
    /// <param name="id">The unique identifier of the user whose roles are being updated.</param>
    /// <param name="roleNames">The complete set of role names to assign. An empty array removes all role assignments.</param>
    /// <returns>200 OK on success.</returns>
    /// <response code="200">The user's roles were updated successfully.</response>
    /// <response code="400">Role assignment failed (invalid role name, etc.).</response>
    /// <response code="404">No user exists with the specified ID.</response>
    [HttpPost("{id}/roles")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetRoles(string id, [FromBody] string[] roleNames)
    {
        try
        {
            await _userService.SetRolesAsync(id, roleNames);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Returns all roles with metadata for use in role pickers and user management UI.
    /// Roles are ordered by position descending, then by name ascending.
    /// </summary>
    /// <returns>A list of role metadata DTOs.</returns>
    /// <response code="200">Returns the roles metadata list.</response>
    [HttpGet("roles-metadata")]
    [ProducesResponseType(typeof(List<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RoleDto>>> GetRolesMetadata()
    {
        var roles = await _userService.GetRolesMetadataAsync();
        return Ok(roles);
    }

    #endregion

    #region LDAP Operations

    /// <summary>
    /// Looks up a user in Active Directory by NTID or email and returns their directory attributes.
    /// </summary>
    /// <param name="identifier">The LDAP identifier to search for (NTID or email address).</param>
    /// <returns>The user's LDAP attributes if found.</returns>
    /// <response code="200">Returns the LDAP user attributes.</response>
    /// <response code="400">Identifier is missing or empty.</response>
    /// <response code="404">User not found in corporate directory.</response>
    [HttpGet("ldap-lookup")]
    [ProducesResponseType(typeof(LdapUserAttributes), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LdapUserAttributes>> LdapLookup([FromQuery] string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return BadRequest("Identifier is required.");

        var attrs = await _ldapAuthService.FetchUserAttributesAsync(identifier.Trim());
        if (attrs is null)
            return NotFound("User not found in corporate directory.");

        return Ok(attrs);
    }

    /// <summary>
    /// Creates a local user account from LDAP/Active Directory attributes.
    /// The user is created with the LDAP auth source and no local password.
    /// </summary>
    /// <param name="attributes">The LDAP attributes to use for account creation.</param>
    /// <returns>The newly created LDAP user as a <see cref="UserDto"/>.</returns>
    /// <response code="201">The LDAP user was created successfully.</response>
    /// <response code="400">Creation failed (duplicate username or email, identity error).</response>
    [HttpPost("ldap-create")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> CreateLdapUser([FromBody] LdapUserAttributes attributes)
    {
        try
        {
            var user = await _userService.CreateLdapUserAsync(attributes);
            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Syncs all LDAP-sourced users with Active Directory attributes.
    /// Streams progress per-user as NDJSON (newline-delimited JSON) for real-time UI updates.
    /// Each line contains a JSON object with total, current, userName, and updated fields.
    /// </summary>
    /// <response code="200">Streams NDJSON progress items until sync completes.</response>
    [HttpPost("ldap-sync")]
    public async Task SyncLdapUsers()
    {
        Response.ContentType = "application/x-ndjson";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        await foreach (var item in _userService.SyncLdapUsersAsync())
        {
            var json = JsonSerializer.Serialize(item);
            await Response.WriteAsync(json + "\n");
            await Response.Body.FlushAsync();
        }
    }

    #endregion
}
