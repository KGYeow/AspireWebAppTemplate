using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.ApiService.Data.Entities;
using AspireWebAppTemplate.ApiService.Utilities;
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
/// Manages application roles including CRUD operations and user-role queries.
/// </summary>
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class RolesController : BaseController
{
    #region Constructor

    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;

    private static readonly (string, Func<ApplicationRole, object?>)[] RoleAuditFields =
    [
        ("Name", r => r.Name),
        ("DisplayName", r => r.DisplayName),
        ("Description", r => r.Description),
        ("Position", r => r.Position),
        ("IsActive", r => r.IsActive),
    ];

    public RolesController(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IAuditLogService auditLogService)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _auditLogService = auditLogService;
    }

    #endregion

    #region CRUD Operations

    /// <summary>
    /// Returns all roles with their user counts.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RoleDto>>> GetRoles()
    {
        var roles = await _roleManager.Roles
            .AsNoTracking()
            .OrderByDescending(r => r.Position)
            .ThenBy(r => r.Name)
            .ToListAsync();

        var roleDtos = new List<RoleDto>();
        foreach (var role in roles)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
            roleDtos.Add(new RoleDto
            {
                Id = role.Id,
                Name = role.Name ?? "",
                DisplayName = role.DisplayName,
                Description = role.Description,
                IsActive = role.IsActive,
                IsSystem = role.IsSystem,
                IsDefault = role.IsDefault,
                RequiresMinimumUser = role.RequiresMinimumUser,
                Position = role.Position,
                UserCount = usersInRole.Count,
                CreatedUtc = role.CreatedUtc,
                UpdatedUtc = role.UpdatedUtc
            });
        }

        return Ok(roleDtos);
    }

    /// <summary>
    /// Returns a single role by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDto>> GetRole(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null)
            return NotFound();

        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);

        return Ok(new RoleDto
        {
            Id = role.Id,
            Name = role.Name ?? "",
            DisplayName = role.DisplayName,
            Description = role.Description,
            IsActive = role.IsActive,
            IsSystem = role.IsSystem,
            IsDefault = role.IsDefault,
            RequiresMinimumUser = role.RequiresMinimumUser,
            Position = role.Position,
            UserCount = usersInRole.Count,
            CreatedUtc = role.CreatedUtc,
            UpdatedUtc = role.UpdatedUtc
        });
    }

    /// <summary>
    /// Creates a new role.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoleDto>> CreateRole([FromBody] CreateRoleRequest request)
    {
        var role = new ApplicationRole
        {
            Name = request.Name,
            DisplayName = request.DisplayName,
            Description = request.Description,
            Position = request.Position,
            IsActive = request.IsActive,
            CreatedUtc = DateTime.UtcNow
        };

        var result = await _roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return BadRequest(errors);
        }

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = CurrentUserId,
            ActionType = AuditActionType.RoleCreated,
            EntityType = AuditEntityType.Role,
            EntityId = role.Id,
            EntityName = role.DisplayName ?? role.Name ?? "",
            Description = $"Role '{role.DisplayName ?? role.Name}' was created.",
            IpAddress = ClientIpAddress
        });

        var dto = new RoleDto
        {
            Id = role.Id,
            Name = role.Name ?? "",
            DisplayName = role.DisplayName,
            Description = role.Description,
            IsActive = role.IsActive,
            IsSystem = role.IsSystem,
            IsDefault = role.IsDefault,
            RequiresMinimumUser = role.RequiresMinimumUser,
            Position = role.Position,
            UserCount = 0,
            CreatedUtc = role.CreatedUtc
        };

        return CreatedAtAction(nameof(GetRole), new { id = role.Id }, dto);
    }

    /// <summary>
    /// Updates an existing role.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateRole(string id, [FromBody] CreateRoleRequest request)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null)
            return NotFound();

        if (role.IsSystem)
            return BadRequest("System roles cannot be modified.");

        var before = AuditChangeHelper.Snapshot(role, RoleAuditFields);

        role.Name = request.Name;
        role.DisplayName = request.DisplayName;
        role.Description = request.Description;
        role.Position = request.Position;
        role.IsActive = request.IsActive;
        role.UpdatedUtc = DateTime.UtcNow;

        var result = await _roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return BadRequest(errors);
        }

        var after = AuditChangeHelper.Snapshot(role, RoleAuditFields);
        var (oldValues, newValues) = AuditChangeHelper.ComputeChanges(before, after);

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = CurrentUserId,
            ActionType = AuditActionType.RoleUpdated,
            EntityType = AuditEntityType.Role,
            EntityId = role.Id,
            EntityName = role.DisplayName ?? role.Name ?? "",
            Description = $"Role '{role.DisplayName ?? role.Name}' was updated.",
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ClientIpAddress
        });

        return Ok();
    }

    /// <summary>
    /// Deletes a role.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteRole(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null)
            return NotFound();

        if (role.IsSystem)
            return BadRequest("System roles cannot be deleted.");

        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
        if (usersInRole.Count > 0)
            return BadRequest($"Cannot delete role '{role.Name}' — {usersInRole.Count} user(s) are still assigned.");

        var displayName = role.DisplayName ?? role.Name ?? "";
        var result = await _roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return BadRequest(errors);
        }

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = CurrentUserId,
            ActionType = AuditActionType.RoleDeleted,
            EntityType = AuditEntityType.Role,
            EntityId = id,
            EntityName = displayName,
            Description = $"Role '{displayName}' was deleted.",
            IpAddress = ClientIpAddress
        });

        return Ok();
    }

    #endregion

    #region Activation

    /// <summary>
    /// Activates a role.
    /// </summary>
    [HttpPost("{id}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActivateRole(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null)
            return NotFound();

        if (role.IsSystem)
            return BadRequest("System roles cannot be modified.");

        role.IsActive = true;
        role.UpdatedUtc = DateTime.UtcNow;
        var result = await _roleManager.UpdateAsync(role);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        return Ok();
    }

    /// <summary>
    /// Deactivates a role.
    /// </summary>
    [HttpPost("{id}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeactivateRole(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null)
            return NotFound();

        if (role.IsSystem)
            return BadRequest("System roles cannot be modified.");

        role.IsActive = false;
        role.UpdatedUtc = DateTime.UtcNow;
        var result = await _roleManager.UpdateAsync(role);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        return Ok();
    }

    #endregion

    #region User-Role Assignment

    /// <summary>
    /// Assigns a user to this role.
    /// </summary>
    [HttpPost("{id}/users")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignUsersToRole(string id, [FromBody] string[] userIds)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null)
            return NotFound();

        int success = 0, failed = 0;
        var successfullyAssignedIds = new List<string>();
        foreach (var userId in userIds)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null) { failed++; continue; }

            var result = await _userManager.AddToRoleAsync(user, role.Name!);
            if (result.Succeeded)
            {
                success++;
                successfullyAssignedIds.Add(userId);
            }
            else failed++;
        }

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = CurrentUserId,
            ActionType = AuditActionType.RoleAssigned,
            EntityType = AuditEntityType.Role,
            EntityId = role.Id,
            EntityName = role.DisplayName ?? role.Name ?? "",
            Description = $"{success} user(s) assigned to role '{role.Name}'.",
            NewValues = AuditChangeHelper.Serialize(new { UserIds = successfullyAssignedIds }),
            IpAddress = ClientIpAddress
        });

        if (failed > 0)
            return Ok(new { success, failed });

        return Ok(new { success, failed = 0 });
    }

    /// <summary>
    /// Removes a user from this role.
    /// </summary>
    [HttpDelete("{id}/users/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveUserFromRole(string id, string userId)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null)
            return NotFound();

        // Guard: prevent removing the last user from a role that requires at least one
        if (role.RequiresMinimumUser)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
            if (usersInRole.Count <= 1)
                return BadRequest($"Cannot remove the last user from role '{role.Name}'. At least one user must remain assigned.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound("User not found.");

        var result = await _userManager.RemoveFromRoleAsync(user, role.Name!);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = CurrentUserId,
            ActionType = AuditActionType.RoleUnassigned,
            EntityType = AuditEntityType.Role,
            EntityId = userId,
            EntityName = role.Name!,
            Description = $"Role '{role.Name}' removed from user '{user.DisplayName ?? user.UserName}'.",
            OldValues = AuditChangeHelper.Serialize(new { UserId = userId, RoleName = role.Name }),
            IpAddress = ClientIpAddress
        });

        return Ok();
    }

    /// <summary>
    /// Returns users assigned to a specific role.
    /// </summary>
    [HttpGet("{id}/users")]
    [ProducesResponseType(typeof(List<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<UserDto>>> GetUsersInRole(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null)
            return NotFound();

        var users = await _userManager.GetUsersInRoleAsync(role.Name!);

        var userDtos = users.Select(user => new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            DisplayName = user.DisplayName,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsActive = user.IsActive,
            AuthSource = user.AuthSource.ToString(),
            CreatedUtc = user.CreatedUtc
        }).ToList();

        return Ok(userDtos);
    }

    #endregion
}
