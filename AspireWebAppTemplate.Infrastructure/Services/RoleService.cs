using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Utilities;
using AspireWebAppTemplate.Application.Features.Template.AuditLog;
using AspireWebAppTemplate.Application.Features.Template.Roles;
using AspireWebAppTemplate.Application.Features.Template.Users;
using AspireWebAppTemplate.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AspireWebAppTemplate.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IRoleService"/> providing full role lifecycle management including
/// CRUD operations, activation/deactivation, and user-role assignment. All business rules,
/// database access, and audit logging for role operations are encapsulated here.
/// </summary>
/// <remarks>
/// Registered as a scoped service to align with the per-request <c>DbContext</c> lifetime.
/// Controllers delegate to this service without containing any business logic, RoleManager
/// usage, or ApplicationDbContext queries.
/// </remarks>
public class RoleService : IRoleService
{
    #region Constructor

    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserAccessor _currentUser;

    /// <summary>
    /// Static field list used by <see cref="AuditChangeHelper.Snapshot{T}"/> to capture
    /// role property values before and after mutations for old/new value change tracking.
    /// </summary>
    private static readonly (string Key, Func<ApplicationRole, object?> Getter)[] RoleAuditFields =
    [
        ("Name", r => r.Name),
        ("DisplayName", r => r.DisplayName),
        ("Description", r => r.Description),
        ("Position", r => r.Position),
        ("IsActive", r => r.IsActive),
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleService"/> class.
    /// </summary>
    /// <param name="roleManager">The ASP.NET Core Identity role manager for role CRUD operations.</param>
    /// <param name="userManager">The ASP.NET Core Identity user manager for user-role queries.</param>
    /// <param name="auditLogService">The audit log service for recording role-related actions.</param>
    /// <param name="currentUser">The accessor providing the authenticated user's identity for audit logging.</param>
    public RoleService(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IAuditLogService auditLogService,
        ICurrentUserAccessor currentUser)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
    }

    #endregion

    #region CRUD Operations

    /// <inheritdoc/>
    /// <summary>
    /// Retrieves all roles in the system ordered by Position descending then Name ascending,
    /// including user counts for each role.
    /// </summary>
    public async Task<List<RoleDto>> GetAllAsync()
    {
        var roles = await _roleManager.Roles
            .AsNoTracking()
            .OrderByDescending(r => r.Position)
            .ThenBy(r => r.Name)
            .ToListAsync();

        var roleDtos = new List<RoleDto>(roles.Count);

        foreach (var role in roles)
        {
            var users = await _userManager.GetUsersInRoleAsync(role.Name!);
            roleDtos.Add(MapToDto(role, users.Count));
        }

        return roleDtos;
    }

    /// <inheritdoc/>
    /// <summary>
    /// Retrieves a single role by its unique identifier including its user count.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when no role exists with the specified ID.</exception>
    public async Task<RoleDto> GetByIdAsync(string id)
    {
        var role = await _roleManager.FindByIdAsync(id)
            ?? throw new KeyNotFoundException($"Role with ID '{id}' was not found.");

        var users = await _userManager.GetUsersInRoleAsync(role.Name!);
        return MapToDto(role, users.Count);
    }

    /// <inheritdoc/>
    /// <summary>
    /// Creates a new role in the system from the provided request data and logs the action.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when Identity validation fails (e.g., duplicate role name).
    /// </exception>
    public async Task<RoleDto> CreateAsync(CreateRoleRequest request)
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
            throw new InvalidOperationException(errors);
        }

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.RoleCreated,
            EntityType = AuditEntityType.Role,
            EntityId = role.Id,
            EntityName = role.DisplayName ?? role.Name ?? "",
            Description = $"Role '{role.DisplayName ?? role.Name}' was created.",
            IpAddress = _currentUser.IpAddress
        });

        return MapToDto(role, userCount: 0);
    }

    /// <inheritdoc/>
    /// <summary>
    /// Updates an existing role with the provided request data. System roles cannot be modified.
    /// Uses AuditChangeHelper to track old/new value changes for the audit log entry.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when no role exists with the specified ID.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the role is a system role or when Identity validation fails.
    /// </exception>
    public async Task UpdateAsync(string id, CreateRoleRequest request)
    {
        var role = await _roleManager.FindByIdAsync(id)
            ?? throw new KeyNotFoundException($"Role with ID '{id}' was not found.");

        if (role.IsSystem)
            throw new InvalidOperationException("System roles cannot be modified.");

        // Snapshot before mutation for old/new value tracking.
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
            throw new InvalidOperationException(errors);
        }

        // Snapshot after mutation and compute the diff.
        var after = AuditChangeHelper.Snapshot(role, RoleAuditFields);
        var (oldValues, newValues) = AuditChangeHelper.ComputeChanges(before, after);

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.RoleUpdated,
            EntityType = AuditEntityType.Role,
            EntityId = role.Id,
            EntityName = role.DisplayName ?? role.Name ?? "",
            Description = $"Role '{role.DisplayName ?? role.Name}' was updated.",
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = _currentUser.IpAddress
        });
    }

    /// <inheritdoc/>
    /// <summary>
    /// Deletes a role from the system. System roles and roles with assigned users cannot be deleted.
    /// Performs audit logging for the delete operation.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when no role exists with the specified ID.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the role is a system role or when users are still assigned to the role.
    /// </exception>
    public async Task DeleteAsync(string id)
    {
        var role = await _roleManager.FindByIdAsync(id)
            ?? throw new KeyNotFoundException($"Role with ID '{id}' was not found.");

        if (role.IsSystem)
            throw new InvalidOperationException("System roles cannot be deleted.");

        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
        if (usersInRole.Count > 0)
            throw new InvalidOperationException($"Cannot delete role '{role.Name}' — {usersInRole.Count} user(s) are still assigned.");

        // Capture display name before deletion since the entity may be detached afterwards.
        var displayName = role.DisplayName ?? role.Name ?? "";
        var roleId = role.Id;

        var result = await _roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.RoleDeleted,
            EntityType = AuditEntityType.Role,
            EntityId = roleId,
            EntityName = displayName,
            Description = $"Role '{displayName}' was deleted.",
            IpAddress = _currentUser.IpAddress
        });
    }

    #endregion

    #region Activation

    /// <inheritdoc/>
    /// <summary>
    /// Activates a role by setting its IsActive status to true. System roles cannot be modified.
    /// Performs audit logging for the activation operation.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when no role exists with the specified ID.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the role is a system role.
    /// </exception>
    public async Task ActivateAsync(string id)
    {
        var role = await _roleManager.FindByIdAsync(id)
            ?? throw new KeyNotFoundException($"Role with ID '{id}' was not found.");

        if (role.IsSystem)
            throw new InvalidOperationException("System roles cannot be modified.");

        role.IsActive = true;
        role.UpdatedUtc = DateTime.UtcNow;

        var result = await _roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.RoleUpdated,
            EntityType = AuditEntityType.Role,
            EntityId = role.Id,
            EntityName = role.DisplayName ?? role.Name ?? "",
            Description = $"Role '{role.DisplayName ?? role.Name}' was activated.",
            OldValues = AuditChangeHelper.Serialize(new { IsActive = false }),
            NewValues = AuditChangeHelper.Serialize(new { IsActive = true }),
            IpAddress = _currentUser.IpAddress
        });
    }

    /// <inheritdoc/>
    /// <summary>
    /// Deactivates a role by setting its IsActive status to false. System roles cannot be modified.
    /// Performs audit logging for the deactivation operation.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when no role exists with the specified ID.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the role is a system role.
    /// </exception>
    public async Task DeactivateAsync(string id)
    {
        var role = await _roleManager.FindByIdAsync(id)
            ?? throw new KeyNotFoundException($"Role with ID '{id}' was not found.");

        if (role.IsSystem)
            throw new InvalidOperationException("System roles cannot be modified.");

        role.IsActive = false;
        role.UpdatedUtc = DateTime.UtcNow;

        var result = await _roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.RoleUpdated,
            EntityType = AuditEntityType.Role,
            EntityId = role.Id,
            EntityName = role.DisplayName ?? role.Name ?? "",
            Description = $"Role '{role.DisplayName ?? role.Name}' was deactivated.",
            OldValues = AuditChangeHelper.Serialize(new { IsActive = true }),
            NewValues = AuditChangeHelper.Serialize(new { IsActive = false }),
            IpAddress = _currentUser.IpAddress
        });
    }

    #endregion

    #region User-Role Assignment

    /// <inheritdoc/>
    /// <summary>
    /// Assigns one or more users to a role in bulk. Each user assignment is attempted
    /// independently — individual failures do not prevent other assignments from succeeding.
    /// Performs audit logging with the list of successfully assigned user IDs.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when no role exists with the specified role ID.</exception>
    public async Task<RoleAssignmentResult> AssignUsersAsync(string roleId, string[] userIds)
    {
        var role = await _roleManager.FindByIdAsync(roleId)
            ?? throw new KeyNotFoundException($"Role with ID '{roleId}' was not found.");

        var success = 0;
        var failed = 0;
        var successfullyAssignedIds = new List<string>();

        foreach (var userId in userIds)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                failed++;
                continue;
            }

            var result = await _userManager.AddToRoleAsync(user, role.Name!);
            if (result.Succeeded)
            {
                success++;
                successfullyAssignedIds.Add(userId);
            }
            else
                failed++;
        }

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.RoleAssigned,
            EntityType = AuditEntityType.Role,
            EntityId = role.Id,
            EntityName = role.DisplayName ?? role.Name ?? "",
            Description = $"{success} user(s) assigned to role '{role.DisplayName ?? role.Name}'.",
            NewValues = AuditChangeHelper.Serialize(new { UserIds = successfullyAssignedIds }),
            IpAddress = _currentUser.IpAddress
        });

        return new RoleAssignmentResult { Success = success, Failed = failed };
    }

    /// <inheritdoc/>
    /// <summary>
    /// Removes a single user from a role. Roles with RequiresMinimumUser cannot have
    /// their last user removed. Performs audit logging with old values tracking.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no role exists with the specified role ID or the user is not found.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when removing the user would leave zero users in a role that requires a minimum user.
    /// </exception>
    public async Task RemoveUserAsync(string roleId, string userId)
    {
        var role = await _roleManager.FindByIdAsync(roleId)
            ?? throw new KeyNotFoundException($"Role with ID '{roleId}' was not found.");

        // Guard: prevent removing the last user from a role that requires at least one
        if (role.RequiresMinimumUser)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
            if (usersInRole.Count <= 1)
                throw new InvalidOperationException($"Cannot remove the last user from role '{role.Name}'. At least one user must remain assigned.");
        }

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        var result = await _userManager.RemoveFromRoleAsync(user, role.Name!);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.RoleUnassigned,
            EntityType = AuditEntityType.Role,
            EntityId = userId,
            EntityName = role.Name!,
            Description = $"Role '{role.Name}' removed from user '{user.DisplayName ?? user.UserName}'.",
            OldValues = AuditChangeHelper.Serialize(new { UserId = userId, RoleName = role.Name }),
            IpAddress = _currentUser.IpAddress
        });
    }

    /// <inheritdoc/>
    /// <summary>
    /// Retrieves the list of users currently assigned to a specific role.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when no role exists with the specified role ID.</exception>
    public async Task<List<UserDto>> GetUsersInRoleAsync(string roleId)
    {
        var role = await _roleManager.FindByIdAsync(roleId)
            ?? throw new KeyNotFoundException($"Role with ID '{roleId}' was not found.");

        var users = await _userManager.GetUsersInRoleAsync(role.Name!);

        return users.Select(MapToUserDto).ToList();
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Maps an <see cref="ApplicationRole"/> entity to a <see cref="RoleDto"/>.
    /// </summary>
    /// <param name="role">The role entity to map.</param>
    /// <param name="userCount">The number of users currently assigned to this role.</param>
    /// <returns>A <see cref="RoleDto"/> containing the role's details.</returns>
    private static RoleDto MapToDto(ApplicationRole role, int userCount)
    {
        return new RoleDto
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
            UserCount = userCount,
            CreatedUtc = role.CreatedUtc,
            UpdatedUtc = role.UpdatedUtc
        };
    }

    /// <summary>
    /// Maps an <see cref="ApplicationUser"/> entity to a <see cref="UserDto"/>.
    /// </summary>
    /// <param name="user">The user entity to map.</param>
    /// <returns>A <see cref="UserDto"/> containing the user's details.</returns>
    private static UserDto MapToUserDto(ApplicationUser user)
    {
        return new UserDto
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
            CreatedUtc = user.CreatedUtc,
            UpdatedUtc = user.UpdatedUtc,
            EmailConfirmed = user.EmailConfirmed,
            TwoFactorEnabled = user.TwoFactorEnabled,
            LockoutEnabled = user.LockoutEnabled,
            LockoutEnd = user.LockoutEnd,
            AccessFailedCount = user.AccessFailedCount,
            LastLoginUtc = user.LastLoginUtc,
            LastPasswordChangeUtc = user.LastPasswordChangeUtc,
            AvatarUrl = user.AvatarUrl,
            Locale = user.Locale,
            TenantId = user.TenantId,
            Theme = user.Theme,
            TimeZoneId = user.TimeZoneId,
            DateTimeFormat = user.DateTimeFormat,
            NotificationPopupsEnabled = user.NotificationPopupsEnabled
        };
    }

    #endregion
}
