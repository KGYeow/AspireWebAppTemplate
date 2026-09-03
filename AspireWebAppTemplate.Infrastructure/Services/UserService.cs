using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Utilities;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Features.Template.AuditLog.Contracts;
using AspireWebAppTemplate.Application.Features.Template.AuditLog;
using AspireWebAppTemplate.Application.Contracts.Email;
using AspireWebAppTemplate.Application.Contracts.Notifications;
using AspireWebAppTemplate.Application.Contracts.Roles;
using AspireWebAppTemplate.Application.Contracts.Users;
using AspireWebAppTemplate.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AspireWebAppTemplate.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IUserService"/> with full user lifecycle management including
/// CRUD operations, search/pagination, activation/deactivation, role assignment, and
/// LDAP synchronization. All database access for user management is encapsulated here.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a scoped service to align with the per-request <see cref="UserManager{TUser}"/>
/// and <see cref="RoleManager{TRole}"/> lifetimes. Uses <see cref="ICurrentUserAccessor"/> for
/// audit logging and self-operation protection (e.g., preventing self-deletion/deactivation).
/// </para>
/// <para>
/// <strong>Business rules enforced:</strong>
/// <list type="bullet">
///   <item>Cannot delete or deactivate the currently authenticated user (self-protection).</item>
///   <item>Cannot delete the last active administrator (lockout prevention).</item>
///   <item>Duplicate username/email on LDAP create throws <see cref="InvalidOperationException"/>.</item>
///   <item>Non-existent user IDs throw <see cref="KeyNotFoundException"/>.</item>
/// </list>
/// </para>
/// </remarks>
public class UserService : IUserService
{
    #region Constructor

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly ILdapAuthService _ldapAuthService;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;

    /// <summary>
    /// Static field definitions used by <see cref="AuditChangeHelper.Snapshot{T}"/> to capture
    /// user entity state before and after mutations for old/new value tracking in audit logs.
    /// </summary>
    private static readonly (string Key, Func<ApplicationUser, object?> Getter)[] UserAuditFields =
    [
        ("DisplayName", u => u.DisplayName),
        ("FirstName", u => u.FirstName),
        ("LastName", u => u.LastName),
        ("Email", u => u.Email),
        ("PhoneNumber", u => u.PhoneNumber),
        ("JobTitle", u => u.JobTitle),
        ("Department", u => u.Department),
        ("EmployeeNumber", u => u.EmployeeNumber),
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="UserService"/> class.
    /// </summary>
    /// <param name="userManager">The ASP.NET Core Identity user manager for user CRUD operations.</param>
    /// <param name="roleManager">The ASP.NET Core Identity role manager for role metadata queries.</param>
    /// <param name="auditLogService">The audit log service for recording user management actions.</param>
    /// <param name="currentUser">The current user accessor for identity and IP address resolution.</param>
    /// <param name="ldapAuthService">The LDAP authentication service for directory attribute fetching.</param>
    /// <param name="notificationService">The notification service for sending in-app notifications to affected users.</param>
    /// <param name="emailService">The email service for sending business notification emails to users.</param>
    public UserService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IAuditLogService auditLogService,
        ICurrentUserAccessor currentUser,
        ILdapAuthService ldapAuthService,
        INotificationService notificationService,
        IEmailService emailService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _ldapAuthService = ldapAuthService;
        _notificationService = notificationService;
        _emailService = emailService;
    }

    #endregion

    #region CRUD Operations

    /// <inheritdoc />
    public async Task<PagedResult<UserDto>> SearchAsync(UserQueryParams queryParams)
    {
        var query = _userManager.Users.AsNoTracking();

        // Apply case-insensitive search filter across multiple user fields
        if (!string.IsNullOrWhiteSpace(queryParams.SearchTerm))
        {
            var term = queryParams.SearchTerm.ToLower();
            query = query.Where(u =>
                (u.UserName != null     && u.UserName.ToLower().Contains(term)) ||
                (u.DisplayName != null  && u.DisplayName.ToLower().Contains(term)) ||
                (u.Email != null        && u.Email.ToLower().Contains(term)) ||
                (u.FirstName != null    && u.FirstName.ToLower().Contains(term)) ||
                (u.LastName != null     && u.LastName.ToLower().Contains(term)) ||
                (u.Department != null   && u.Department.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync();

        // Apply pagination only when both Page and PageSize are provided;
        // otherwise return all matching results.
        var page = queryParams.Page ?? 0;
        var pageSize = queryParams.PageSize ?? totalCount;

        var users = await query
            .OrderBy(u => u.UserName)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Map each user to a DTO including their roles
        var items = new List<UserDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(MapToUserDto(user, roles));
        }

        return new PagedResult<UserDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<UserDto> GetByIdAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            throw new KeyNotFoundException($"User with ID '{id}' was not found.");

        var roles = await _userManager.GetRolesAsync(user);
        return MapToUserDto(user, roles);
    }

    /// <inheritdoc />
    public async Task<UserDto> CreateAsync(CreateUserRequest request)
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
            throw new InvalidOperationException(errors);
        }

        // Assign role if specified
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            await _userManager.AddToRoleAsync(user, request.Role);
        }

        // Log audit entry for user creation
        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.UserCreated,
            EntityType = AuditEntityType.User,
            EntityId = user.Id,
            EntityName = user.DisplayName ?? user.UserName ?? string.Empty,
            Description = $"User '{user.DisplayName ?? user.Email}' was created.",
            IpAddress = _currentUser.IpAddress
        });

        var roles = await _userManager.GetRolesAsync(user);
        return MapToUserDto(user, roles);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(string id, UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            throw new KeyNotFoundException($"User with ID '{id}' was not found.");

        // Snapshot entity state before mutation for audit change tracking
        var before = AuditChangeHelper.Snapshot(user, UserAuditFields);

        // Apply non-null fields from the request
        if (request.DisplayName is not null) user.DisplayName = request.DisplayName;
        if (request.FirstName is not null) user.FirstName = request.FirstName;
        if (request.LastName is not null) user.LastName = request.LastName;
        if (request.Email is not null) user.Email = request.Email;
        if (request.PhoneNumber is not null) user.PhoneNumber = request.PhoneNumber;
        if (request.JobTitle is not null) user.JobTitle = request.JobTitle;
        if (request.Department is not null) user.Department = request.Department;
        if (request.EmployeeNumber is not null) user.EmployeeNumber = request.EmployeeNumber;

        user.UpdatedUtc = DateTime.UtcNow;

        // Snapshot entity state after mutation and compute the diff
        var after = AuditChangeHelper.Snapshot(user, UserAuditFields);
        var (oldValues, newValues) = AuditChangeHelper.ComputeChanges(before, after);

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        // Log audit entry for user update with old/new value tracking
        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.UserUpdated,
            EntityType = AuditEntityType.User,
            EntityId = user.Id,
            EntityName = user.DisplayName ?? user.UserName ?? string.Empty,
            Description = $"User '{user.DisplayName ?? user.UserName}' was updated.",
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = _currentUser.IpAddress
        });
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            throw new KeyNotFoundException($"User with ID '{id}' was not found.");

        // Self-deletion protection: prevent the current user from deleting their own account
        if (user.Id == _currentUser.UserId)
            throw new InvalidOperationException("You cannot delete your own account.");

        // Last-admin protection: prevent deleting the last active administrator
        await EnsureNotLastActiveAdminAsync(user);

        var displayName = user.DisplayName ?? user.UserName ?? string.Empty;

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        // Log audit entry for user deletion
        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.UserDeleted,
            EntityType = AuditEntityType.User,
            EntityId = id,
            EntityName = displayName,
            Description = $"User '{displayName}' was deleted.",
            IpAddress = _currentUser.IpAddress
        });
    }

    #endregion

    #region Activation

    /// <inheritdoc />
    public async Task ActivateAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            throw new KeyNotFoundException($"User with ID '{id}' was not found.");

        user.IsActive = true;
        user.UpdatedUtc = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // Log audit entry for user activation with old/new state
        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.UserActivated,
            EntityType = AuditEntityType.User,
            EntityId = user.Id,
            EntityName = user.DisplayName ?? user.UserName ?? string.Empty,
            Description = $"User '{user.DisplayName ?? user.UserName}' was activated.",
            OldValues = AuditChangeHelper.Serialize(new { IsActive = false }),
            NewValues = AuditChangeHelper.Serialize(new { IsActive = true }),
            IpAddress = _currentUser.IpAddress
        });
    }

    /// <inheritdoc />
    public async Task DeactivateAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            throw new KeyNotFoundException($"User with ID '{id}' was not found.");

        // Self-deactivation protection: prevent the current user from deactivating their own account
        if (user.Id == _currentUser.UserId)
            throw new InvalidOperationException("You cannot deactivate your own account.");

        user.IsActive = false;
        user.UpdatedUtc = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // Log audit entry for user deactivation with old/new state
        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.UserDeactivated,
            EntityType = AuditEntityType.User,
            EntityId = user.Id,
            EntityName = user.DisplayName ?? user.UserName ?? string.Empty,
            Description = $"User '{user.DisplayName ?? user.UserName}' was deactivated.",
            OldValues = AuditChangeHelper.Serialize(new { IsActive = true }),
            NewValues = AuditChangeHelper.Serialize(new { IsActive = false }),
            IpAddress = _currentUser.IpAddress
        });

        // Notify the affected user that their account has been deactivated
        await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
        {
            UserId = user.Id,
            Category = NotificationCategory.Account,
            Title = "Account Deactivated",
            Message = "Your account has been deactivated by an administrator. You will no longer be able to sign in."
        });

        // Send account deactivation email (best-effort, respects EmailEnabled preference)
        await _emailService.TrySendEmailAsync(new TrySendEmailRequest
        {
            UserId = user.Id,
            RecipientEmail = user.Email,
            Category = NotificationCategory.Account,
            EmailType = EmailType.AccountDeactivated,
            Variables = new Dictionary<string, string>
            {
                ["UserName"] = user.DisplayName ?? user.UserName ?? string.Empty,
                ["DeactivationReason"] = "Deactivated by an administrator."
            }
        });
    }

    /// <inheritdoc />
    public async Task ResetPasswordAsync(string id, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            throw new KeyNotFoundException($"User with ID '{id}' was not found.");

        // Generate a password reset token and use it to set the new password
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        // Log audit entry for admin password reset (no password values in audit)
        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.PasswordChanged,
            EntityType = AuditEntityType.User,
            EntityId = user.Id,
            EntityName = user.DisplayName ?? user.UserName ?? string.Empty,
            Description = $"Password for user '{user.DisplayName ?? user.UserName}' was reset by an administrator.",
            IpAddress = _currentUser.IpAddress
        });

        // Notify the affected user that their password was reset by an admin
        await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
        {
            UserId = user.Id,
            Category = NotificationCategory.Account,
            Title = "Password Reset",
            Message = "Your password has been reset by an administrator. Please sign in with your new password."
        });

        // Send password changed email (best-effort, respects EmailEnabled preference)
        await _emailService.TrySendEmailAsync(new TrySendEmailRequest
        {
            UserId = user.Id,
            RecipientEmail = user.Email,
            Category = NotificationCategory.Account,
            EmailType = EmailType.PasswordChanged,
            Variables = new Dictionary<string, string>
            {
                ["UserName"] = user.DisplayName ?? user.UserName ?? string.Empty
            }
        });
    }

    #endregion

    #region Roles

    /// <inheritdoc />
    public async Task SetRolesAsync(string id, string[] roleNames)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            throw new KeyNotFoundException($"User with ID '{id}' was not found.");

        // Remove all current roles and replace with the new set
        var currentRoles = await _userManager.GetRolesAsync(user);

        var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!removeResult.Succeeded)
        {
            var errors = string.Join("; ", removeResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        if (roleNames.Length > 0)
        {
            var addResult = await _userManager.AddToRolesAsync(user, roleNames);
            if (!addResult.Succeeded)
            {
                var errors = string.Join("; ", addResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException(errors);
            }
        }

        // Log audit entry for role assignment change with old/new values
        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.RoleAssigned,
            EntityType = AuditEntityType.User,
            EntityId = user.Id,
            EntityName = user.DisplayName ?? user.UserName ?? string.Empty,
            Description = $"Roles for user '{user.DisplayName ?? user.UserName}' set to: {string.Join(", ", roleNames)}.",
            OldValues = AuditChangeHelper.Serialize(new { Roles = currentRoles }),
            NewValues = AuditChangeHelper.Serialize(new { Roles = roleNames }),
            IpAddress = _currentUser.IpAddress
        });
    }

    /// <inheritdoc />
    public async Task<List<RoleDto>> GetRolesMetadataAsync()
    {
        var roles = await _roleManager.Roles
            .AsNoTracking()
            .OrderByDescending(r => r.Position)
            .ThenBy(r => r.Name)
            .ToListAsync();

        return roles.Select(r => new RoleDto
        {
            Id = r.Id,
            Name = r.Name ?? string.Empty,
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
    }

    #endregion

    #region LDAP Operations

    /// <inheritdoc />
    public async Task<UserDto> CreateLdapUserAsync(LdapUserAttributes attributes)
    {
        // Check for duplicate username (NTID)
        var existingByName = await _userManager.FindByNameAsync(attributes.Ntid);
        if (existingByName is not null)
            throw new InvalidOperationException($"User '{attributes.Ntid}' already exists.");

        // Check for duplicate email if provided
        if (!string.IsNullOrEmpty(attributes.Email))
        {
            var existingByEmail = await _userManager.FindByEmailAsync(attributes.Email);
            if (existingByEmail is not null)
                throw new InvalidOperationException($"A user with email '{attributes.Email}' already exists.");
        }

        // Create user with LDAP auth source and no local password
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
        {
            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create user: {errors}");
        }

        // Assign the default role
        var defaultRole = await _roleManager.Roles.FirstOrDefaultAsync(r => r.IsDefault);
        var defaultRoleName = defaultRole?.Name ?? "User";
        await _userManager.AddToRoleAsync(user, defaultRoleName);

        // Log audit entry for LDAP user creation
        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,
            ActionType = AuditActionType.UserCreated,
            EntityType = AuditEntityType.User,
            EntityId = user.Id,
            EntityName = user.DisplayName ?? user.UserName ?? string.Empty,
            Description = $"LDAP user '{user.DisplayName ?? user.UserName}' was created.",
            IpAddress = _currentUser.IpAddress
        });

        var roles = await _userManager.GetRolesAsync(user);
        return MapToUserDto(user, roles);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LdapSyncProgressItem> SyncLdapUsersAsync()
    {
        // Query all LDAP-sourced users ordered by username
        var ldapUsers = await _userManager.Users
            .Where(u => u.AuthSource == AuthSource.LDAP)
            .OrderBy(u => u.UserName)
            .ToListAsync();

        var total = ldapUsers.Count;

        for (var i = 0; i < total; i++)
        {
            var user = ldapUsers[i];
            var progressItem = await SyncSingleLdapUserAsync(user, total, i + 1);
            yield return progressItem;
        }
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Synchronizes a single LDAP user's attributes from the directory and returns a progress item.
    /// Catches exceptions to report failures without interrupting the overall sync operation.
    /// </summary>
    /// <param name="user">The application user entity to synchronize.</param>
    /// <param name="total">The total number of LDAP users being synced (for progress reporting).</param>
    /// <param name="current">The 1-based index of the current user (for progress reporting).</param>
    /// <returns>A <see cref="LdapSyncProgressItem"/> indicating the sync outcome for this user.</returns>
    private async Task<LdapSyncProgressItem> SyncSingleLdapUserAsync(ApplicationUser user, int total, int current)
    {
        var progressItem = new LdapSyncProgressItem
        {
            Total = total,
            Current = current,
            UserName = user.UserName ?? string.Empty
        };

        try
        {
            // Fetch current attributes from LDAP directory
            var attrs = await _ldapAuthService.FetchUserAttributesAsync(user.UserName ?? string.Empty);

            if (attrs is null)
            {
                // User not found in LDAP — mark as failed
                progressItem.Updated = null;
                return progressItem;
            }

            // Compare fields and update if changed
            var changed = false;

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
                progressItem.Updated = updateResult.Succeeded;
            }
            else
            {
                progressItem.Updated = false; // no changes needed
            }
        }
        catch
        {
            // Mark as failed if any exception occurs during sync for this user
            progressItem.Updated = null;
        }

        return progressItem;
    }

    /// <summary>
    /// Ensures the specified user is not the last active administrator in the system.
    /// Prevents accidental lockout by blocking deletion of the sole remaining active admin.
    /// </summary>
    /// <param name="user">The user being deleted.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the user is an admin and no other active admins exist.
    /// </exception>
    private async Task EnsureNotLastActiveAdminAsync(ApplicationUser user)
    {
        // Check if the user is in the Admin role
        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        if (!isAdmin)
            return;

        // Count active users in the Admin role (excluding the target user)
        var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
        var otherActiveAdmins = adminUsers.Count(u => u.Id != user.Id && u.IsActive);

        if (otherActiveAdmins == 0)
            throw new InvalidOperationException("Cannot delete the last active administrator.");
    }

    /// <summary>
    /// Maps an <see cref="ApplicationUser"/> entity and its role names to a <see cref="UserDto"/>.
    /// Includes all profile, security, preference, and metadata fields.
    /// </summary>
    /// <param name="user">The application user entity to map.</param>
    /// <param name="roles">The collection of role names assigned to the user.</param>
    /// <returns>A fully populated <see cref="UserDto"/> instance.</returns>
    private static UserDto MapToUserDto(ApplicationUser user, IList<string> roles)
    {
        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
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
            UpdatedUtc = user.UpdatedUtc,
            Theme = user.Theme,
            TimeZoneId = user.TimeZoneId,
            DateTimeFormat = user.DateTimeFormat,
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
            NotificationPopupsEnabled = user.NotificationPopupsEnabled
        };
    }

    #endregion
}