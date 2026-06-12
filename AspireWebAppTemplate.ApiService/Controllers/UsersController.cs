using System.Security.Claims;
using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.ApiService.Data.Entities;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Contracts.Auth;
using AspireWebAppTemplate.Core.Contracts.AuditLog;
using AspireWebAppTemplate.Core.Contracts.Roles;
using AspireWebAppTemplate.Core.Contracts.Users;
using AspireWebAppTemplate.Core.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AspireWebAppTemplate.ApiService.Controllers;

/// <summary>
/// Manages user accounts including CRUD operations, activation, and role assignment.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    #region Constructor

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IAuditLogService _auditLogService;
    private readonly ILdapAuthService _ldapAuthService;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IAuditLogService auditLogService,
        ILdapAuthService ldapAuthService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _auditLogService = auditLogService;
        _ldapAuthService = ldapAuthService;
    }

    #endregion

    #region CRUD Operations

    /// <summary>
    /// Returns a paged list of users with their assigned roles.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UserDto>>> GetUsers(
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null)
    {
        var query = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(u =>
                (u.UserName != null && u.UserName.ToLower().Contains(term)) ||
                (u.DisplayName != null && u.DisplayName.ToLower().Contains(term)) ||
                (u.Email != null && u.Email.ToLower().Contains(term)) ||
                (u.FirstName != null && u.FirstName.ToLower().Contains(term)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(term)) ||
                (u.Department != null && u.Department.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync();

        var users = await query
            .OrderBy(u => u.UserName)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userDtos = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userDtos.Add(new UserDto
            {
                Id = user.Id,
                UserName = user.UserName ?? "",
                DisplayName = user.DisplayName,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                JobTitle = user.JobTitle,
                Department = user.Department,
                EmployeeNumber = user.EmployeeNumber,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                AuthSource = user.AuthSource.ToString(),
                Roles = roles.ToList(),
                CreatedUtc = user.CreatedUtc,
                UpdatedUtc = user.UpdatedUtc
            });
        }

        return Ok(new PagedResult<UserDto>
        {
            Items = userDtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// Returns a single user by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            DisplayName = user.DisplayName,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            JobTitle = user.JobTitle,
            Department = user.Department,
            EmployeeNumber = user.EmployeeNumber,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            AuthSource = user.AuthSource.ToString(),
            Roles = roles.ToList(),
            CreatedUtc = user.CreatedUtc,
            UpdatedUtc = user.UpdatedUtc
        });
    }

    /// <summary>
    /// Creates a new user account.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            EmailConfirmed = true,
            IsActive = true,
            CreatedUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return BadRequest(errors);
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            await _userManager.AddToRoleAsync(user, request.Role);
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditLogService.LogAsync(
            currentUserId,
            AuditActionType.UserCreated,
            AuditEntityType.User,
            user.Id,
            user.DisplayName ?? user.UserName ?? "",
            $"User '{user.DisplayName ?? user.Email}' was created.",
            ipAddress: ipAddress);

        var roles = await _userManager.GetRolesAsync(user);
        var dto = new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            DisplayName = user.DisplayName,
            Email = user.Email,
            IsActive = user.IsActive,
            AuthSource = user.AuthSource.ToString(),
            Roles = roles.ToList(),
            CreatedUtc = user.CreatedUtc
        };

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, dto);
    }

    /// <summary>
    /// Updates an existing user's profile information.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        if (request.DisplayName is not null) user.DisplayName = request.DisplayName;
        if (request.FirstName is not null) user.FirstName = request.FirstName;
        if (request.LastName is not null) user.LastName = request.LastName;
        if (request.Email is not null)
        {
            user.Email = request.Email;
            user.UserName = request.Email;
        }
        if (request.PhoneNumber is not null) user.PhoneNumber = request.PhoneNumber;
        if (request.JobTitle is not null) user.JobTitle = request.JobTitle;
        if (request.Department is not null) user.Department = request.Department;
        if (request.EmployeeNumber is not null) user.EmployeeNumber = request.EmployeeNumber;

        user.UpdatedUtc = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return BadRequest(errors);
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditLogService.LogAsync(
            currentUserId,
            AuditActionType.UserUpdated,
            AuditEntityType.User,
            user.Id,
            user.DisplayName ?? user.UserName ?? "",
            $"User '{user.DisplayName ?? user.UserName}' was updated.",
            ipAddress: ipAddress);

        return Ok();
    }

    /// <summary>
    /// Deletes a user account.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (user.Id == currentUserId)
            return BadRequest("You cannot delete your own account.");

        var displayName = user.DisplayName ?? user.UserName ?? "";
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return BadRequest(errors);
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditLogService.LogAsync(
            currentUserId,
            AuditActionType.UserDeleted,
            AuditEntityType.User,
            id,
            displayName,
            $"User '{displayName}' was deleted.",
            ipAddress: ipAddress);

        return Ok();
    }

    #endregion

    #region Activation

    /// <summary>
    /// Activates a user account.
    /// </summary>
    [HttpPost("{id}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        user.IsActive = true;
        user.UpdatedUtc = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditLogService.LogAsync(
            currentUserId,
            AuditActionType.UserActivated,
            AuditEntityType.User,
            user.Id,
            user.DisplayName ?? user.UserName ?? "",
            $"User '{user.DisplayName ?? user.UserName}' was activated.",
            ipAddress: ipAddress);

        return Ok();
    }

    /// <summary>
    /// Deactivates a user account.
    /// </summary>
    [HttpPost("{id}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeactivateUser(string id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id == currentUserId)
            return BadRequest("You cannot deactivate your own account.");

        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        user.IsActive = false;
        user.UpdatedUtc = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditLogService.LogAsync(
            currentUserId,
            AuditActionType.UserDeactivated,
            AuditEntityType.User,
            user.Id,
            user.DisplayName ?? user.UserName ?? "",
            $"User '{user.DisplayName ?? user.UserName}' was deactivated.",
            ipAddress: ipAddress);

        return Ok();
    }

    #endregion

    #region Roles

    /// <summary>
    /// Sets the roles for a user, replacing all existing role assignments.
    /// </summary>
    [HttpPost("{id}/roles")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetRoles(string id, [FromBody] string[] roleNames)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        var currentRoles = await _userManager.GetRolesAsync(user);
        var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!removeResult.Succeeded)
        {
            var errors = string.Join("; ", removeResult.Errors.Select(e => e.Description));
            return BadRequest(errors);
        }

        if (roleNames.Length > 0)
        {
            var addResult = await _userManager.AddToRolesAsync(user, roleNames);
            if (!addResult.Succeeded)
            {
                var errors = string.Join("; ", addResult.Errors.Select(e => e.Description));
                return BadRequest(errors);
            }
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditLogService.LogAsync(
            currentUserId,
            AuditActionType.RoleAssigned,
            AuditEntityType.User,
            user.Id,
            user.DisplayName ?? user.UserName ?? "",
            $"Roles for user '{user.DisplayName ?? user.UserName}' set to: {string.Join(", ", roleNames)}.",
            ipAddress: ipAddress);

        return Ok();
    }

    #endregion

    #region LDAP Operations

    /// <summary>
    /// [LDAP] Looks up a user from Active Directory by NTID or email.
    /// </summary>
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
    /// [LDAP] Creates a local user account from LDAP attributes.
    /// </summary>
    [HttpPost("ldap-create")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> CreateLdapUser([FromBody] LdapUserAttributes attributes)
    {
        // Check for duplicate by username
        var existingByName = await _userManager.FindByNameAsync(attributes.Ntid);
        if (existingByName is not null)
            return BadRequest($"User '{attributes.Ntid}' already exists.");

        // Check for duplicate by email
        if (!string.IsNullOrEmpty(attributes.Email))
        {
            var existingByEmail = await _userManager.FindByEmailAsync(attributes.Email);
            if (existingByEmail is not null)
                return BadRequest($"A user with email '{attributes.Email}' already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = attributes.Ntid,
            Email = attributes.Email,
            EmailConfirmed = true,
            DisplayName = attributes.DisplayName,
            FirstName = attributes.FirstName,
            LastName = attributes.LastName,
            JobTitle = attributes.JobTitle,
            Department = attributes.Department,
            EmployeeNumber = attributes.EmployeeNumber,
            IsActive = true,
            AuthSource = AuthSource.LDAP,
            CreatedUtc = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
            return BadRequest("Failed to create user: " + string.Join(", ", createResult.Errors.Select(e => e.Description)));

        // Assign the default role
        var defaultRole = await _roleManager.Roles.FirstOrDefaultAsync(r => r.IsDefault);
        var defaultRoleName = defaultRole?.Name ?? "User";
        await _userManager.AddToRoleAsync(user, defaultRoleName);

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditLogService.LogAsync(
            currentUserId,
            AuditActionType.UserCreated,
            AuditEntityType.User,
            user.Id,
            user.DisplayName ?? user.UserName ?? "",
            $"LDAP user '{user.DisplayName ?? user.UserName}' was created.",
            ipAddress: ipAddress);

        var roles = await _userManager.GetRolesAsync(user);
        var dto = new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            DisplayName = user.DisplayName,
            Email = user.Email,
            IsActive = user.IsActive,
            AuthSource = user.AuthSource.ToString(),
            Roles = roles.ToList(),
            CreatedUtc = user.CreatedUtc
        };

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, dto);
    }

    /// <summary>
    /// [LDAP] Syncs all LDAP-sourced users with Active Directory attributes.
    /// Returns a summary of how many were updated/failed.
    /// </summary>
    [HttpPost("ldap-sync")]
    [ProducesResponseType(typeof(LdapSyncResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<LdapSyncResult>> SyncLdapUsers()
    {
        var ldapUsers = await _userManager.Users
            .Where(u => u.AuthSource == AuthSource.LDAP)
            .OrderBy(u => u.UserName)
            .ToListAsync();

        int updated = 0, failed = 0;

        foreach (var user in ldapUsers)
        {
            try
            {
                var attrs = await _ldapAuthService.FetchUserAttributesAsync(user.UserName ?? "");
                if (attrs is null) { failed++; continue; }

                bool changed = false;
                if (!string.Equals(user.DisplayName, attrs.DisplayName, StringComparison.Ordinal))
                { user.DisplayName = attrs.DisplayName; changed = true; }
                if (!string.Equals(user.FirstName, attrs.FirstName, StringComparison.Ordinal))
                { user.FirstName = attrs.FirstName; changed = true; }
                if (!string.Equals(user.LastName, attrs.LastName, StringComparison.Ordinal))
                { user.LastName = attrs.LastName; changed = true; }
                if (!string.Equals(user.Email, attrs.Email, StringComparison.OrdinalIgnoreCase))
                { user.Email = attrs.Email; changed = true; }
                if (!string.Equals(user.JobTitle, attrs.JobTitle, StringComparison.Ordinal))
                { user.JobTitle = attrs.JobTitle; changed = true; }
                if (!string.Equals(user.Department, attrs.Department, StringComparison.Ordinal))
                { user.Department = attrs.Department; changed = true; }
                if (!string.Equals(user.EmployeeNumber, attrs.EmployeeNumber, StringComparison.Ordinal))
                { user.EmployeeNumber = attrs.EmployeeNumber; changed = true; }

                if (changed)
                {
                    user.UpdatedUtc = DateTime.UtcNow;
                    var updateResult = await _userManager.UpdateAsync(user);
                    if (updateResult.Succeeded) updated++;
                    else failed++;
                }
            }
            catch { failed++; }
        }

        return Ok(new LdapSyncResult { Total = ldapUsers.Count, Updated = updated, Failed = failed });
    }

    #endregion

    #region Roles

    /// <summary>
    /// Returns all roles with metadata (for frontend to use in authority checks and role pickers).
    /// Duplicates the roles endpoint but scoped to user context needs.
    /// </summary>
    [HttpGet("roles-metadata")]
    [ProducesResponseType(typeof(List<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RoleDto>>> GetRolesMetadata()
    {
        var roles = await _roleManager.Roles
            .AsNoTracking()
            .OrderByDescending(r => r.Position)
            .ThenBy(r => r.Name)
            .ToListAsync();

        var roleDtos = roles.Select(r => new RoleDto
        {
            Id = r.Id,
            Name = r.Name ?? "",
            DisplayName = r.DisplayName,
            Description = r.Description,
            IsActive = r.IsActive,
            IsSystem = r.IsSystem,
            IsDefault = r.IsDefault,
            RequiresMinimumUser = r.RequiresMinimumUser,
            Position = r.Position,
            CreatedUtc = r.CreatedUtc,
            UpdatedUtc = r.UpdatedUtc
        }).ToList();

        return Ok(roleDtos);
    }

    #endregion
}

/// <summary>
/// Result returned by the LDAP sync operation.
/// </summary>
public sealed class LdapSyncResult
{
    public int Total { get; set; }
    public int Updated { get; set; }
    public int Failed { get; set; }
}
