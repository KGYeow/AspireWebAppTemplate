using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.AuditLog;
using AspireWebAppTemplate.Application.Features.Permissions;
using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AspireWebAppTemplate.Infrastructure.Services.Permissions;

/// <summary>
/// Implements the <see cref="IPermissionService"/> interface for the resource-based
/// (<c>Module.Action</c>) authorization model. A role's effective access is the set of permission
/// keys granted to it via <c>RolePermission</c> join records; a user's effective permissions are
/// the distinct union of grants across all roles assigned to that user.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Union resolution:</strong> <see cref="GetPermissionsForRolesAsync"/> computes effective
/// permissions with a single JOIN of <c>RolePermissions</c> and <c>Permissions</c>, returning the
/// distinct set of keys. An empty or absent role set yields an empty permission set.
/// </para>
/// <para>
/// <strong>Full-replacement strategy:</strong> <see cref="UpdateRolePermissionsAsync"/> uses a
/// delete-all-then-insert-new approach within a transaction. The supplied key list becomes the
/// role's entire grant set — an empty list clears all grants — so the database always reflects the
/// exact set the caller specified with no leftover grants from a prior configuration.
/// </para>
/// <para>
/// Registered as a scoped service to align with the per-request <see cref="ApplicationDbContext"/>
/// lifetime.
/// </para>
/// </remarks>
public class PermissionService : IPermissionService
{
    #region Constructor

    /// <summary>
    /// The application database context used to query permission definitions and role grants.
    /// </summary>
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// The ASP.NET Core Identity user manager used to resolve a user's assigned roles.
    /// </summary>
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>
    /// The ASP.NET Core Identity role manager used to validate role existence and resolve role IDs.
    /// </summary>
    private readonly RoleManager<ApplicationRole> _roleManager;

    /// <summary>
    /// The audit log service used to record security-sensitive permission changes.
    /// </summary>
    private readonly IAuditLogService _auditLogService;

    /// <summary>
    /// The accessor that supplies the acting user's identity and IP address for audit entries.
    /// </summary>
    private readonly ICurrentUserAccessor _currentUserAccessor;

    /// <summary>
    /// The logger used to record audit-logging failures without disrupting the primary operation.
    /// </summary>
    private readonly ILogger<PermissionService> _logger;

    /// <summary>
    /// The static mapping of admin page paths to the module that gates each page. Defined once as
    /// the canonical page-to-module correspondence used for module-based page visibility and access.
    /// </summary>
    private static readonly PageModuleMappingDto[] PageModuleMappings =
    [
        new() { PagePath = "/admin/user-management", Module = "Users" },
        new() { PagePath = "/admin/role-management", Module = "Roles" },
        new() { PagePath = "/admin/audit-log", Module = "AuditLog" },
        new() { PagePath = "/admin/permission-management", Module = "Permissions" },
        new() { PagePath = "/admin/announcements", Module = "Announcements" },
        new() { PagePath = "/admin/email-templates", Module = "EmailTemplates" }
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionService"/> class.
    /// </summary>
    /// <param name="dbContext">The application database context for querying permissions and grants.</param>
    /// <param name="userManager">The ASP.NET Core Identity user manager for resolving user role assignments.</param>
    /// <param name="roleManager">The ASP.NET Core Identity role manager for validating role existence and resolving role IDs.</param>
    /// <param name="auditLogService">The audit log service for recording permission changes.</param>
    /// <param name="currentUserAccessor">The accessor providing the acting user's identity and IP address.</param>
    /// <param name="logger">The logger for recording audit-logging failures.</param>
    public PermissionService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IAuditLogService auditLogService,
        ICurrentUserAccessor currentUserAccessor,
        ILogger<PermissionService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _auditLogService = auditLogService;
        _currentUserAccessor = currentUserAccessor;
        _logger = logger;
    }

    #endregion

    #region Query Operations

    /// <inheritdoc />
    public async Task<List<PermissionGroupDto>> GetAllPermissionsGroupedAsync()
    {
        // Load every defined permission, then group by module in memory so each module becomes a
        // single PermissionGroupDto carrying its ordered permission list for the matrix UI.
        var permissions = await _dbContext.Permissions
            .AsNoTracking()
            .ToListAsync();

        var grouped = permissions
            .GroupBy(p => p.Module)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new PermissionGroupDto
            {
                Module = g.Key,
                Permissions = g
                    .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(p => new PermissionDto
                    {
                        Id = p.Id,
                        Key = p.Key,
                        DisplayName = p.DisplayName,
                        Description = p.Description
                    })
                    .ToList()
            })
            .ToList();

        return grouped;
    }

    /// <inheritdoc />
    public async Task<List<string>> GetRolePermissionKeysAsync(string roleId)
    {
        // Validate the role exists first so callers can distinguish "unknown role" (404) from
        // "known role with no grants" (empty list).
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role is null)
        {
            throw new KeyNotFoundException($"Role with ID '{roleId}' was not found.");
        }

        // Return the permission keys granted to this role via a JOIN of RolePermissions and
        // Permissions on PermissionId.
        var keys = await _dbContext.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission.Key)
            .ToListAsync();

        return keys;
    }

    /// <inheritdoc />
    public async Task<List<string>> GetPermissionsForRolesAsync(IEnumerable<string> roleIds)
    {
        // Materialize the role set once; an empty or absent set resolves to no permissions.
        var roleIdList = roleIds?.ToList() ?? [];
        if (roleIdList.Count == 0)
        {
            return [];
        }

        // Single query JOINing RolePermissions and Permissions, returning the distinct set of keys
        // granted to any of the supplied roles. Case-insensitive distinctness is applied in memory
        // to remain provider-agnostic (SQL Server vs SQLite differ in default collation behavior).
        var keys = await _dbContext.RolePermissions
            .AsNoTracking()
            .Where(rp => roleIdList.Contains(rp.RoleId))
            .Select(rp => rp.Permission.Key)
            .ToListAsync();

        return keys
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<List<string>> GetMyPermissionsAsync(string userId)
    {
        // Resolve the user, then the roles assigned to them. A missing user or no roles yields no
        // effective permissions.
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return [];
        }

        var roleNames = await _userManager.GetRolesAsync(user);
        if (roleNames.Count == 0)
        {
            return [];
        }

        // Map role names to role IDs so the union can be computed by the shared role-based query.
        var roleIds = await _roleManager.Roles
            .AsNoTracking()
            .Where(r => roleNames.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync();

        // Reuse the single-query union resolution for the user's roles.
        return await GetPermissionsForRolesAsync(roleIds);
    }

    /// <inheritdoc />
    public Task<List<PageModuleMappingDto>> GetPageModuleMappingsAsync()
    {
        // Return a fresh copy of the static page-to-module mapping so callers cannot mutate the
        // canonical definition.
        var mappings = PageModuleMappings
            .Select(m => new PageModuleMappingDto { PagePath = m.PagePath, Module = m.Module })
            .ToList();

        return Task.FromResult(mappings);
    }

    #endregion

    #region Write Operations

    /// <inheritdoc />
    public async Task UpdateRolePermissionsAsync(string roleId, List<string> permissionKeys)
    {
        // --- Validation ---
        // Step 1: Validate that the roleId corresponds to an existing role (404 if not found).
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role is null)
        {
            throw new KeyNotFoundException($"Role with ID '{roleId}' was not found.");
        }

        // Step 2: Reject attempts to modify the Admin role's permissions. The Admin role always
        // holds implicit full access and its grants are immutable, so modification is forbidden
        // regardless of what keys are supplied. Non-Admin system roles are intentionally allowed.
        if (string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Admin role permissions cannot be modified.");
        }

        // Step 3: Validate that every provided key matches an existing permission definition.
        // Resolve all defined keys to their IDs in one query, then surface any unknown keys so the
        // caller can correct the request before any mutation occurs.
        var requestedKeys = permissionKeys ?? [];

        var keyToId = await _dbContext.Permissions
            .AsNoTracking()
            .Where(p => requestedKeys.Contains(p.Key))
            .ToDictionaryAsync(p => p.Key, p => p.Id);

        var invalidKeys = requestedKeys
            .Where(key => !keyToId.ContainsKey(key))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (invalidKeys.Count > 0)
        {
            throw new ArgumentException(
                $"The following permission keys are not defined: {string.Join(", ", invalidKeys)}");
        }

        // --- Audit Snapshot (before) ---
        // Capture the role's CURRENT granted permission keys before any mutation so the audit entry
        // can record the exact old/new sets. Mirrors the GetRolePermissionKeysAsync query.
        var oldKeys = await _dbContext.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission.Key)
            .ToListAsync();

        // The new set is the requested key list (the full-replacement target).
        var newKeys = requestedKeys.ToList();

        // --- Full-Replacement Strategy ---
        // Delete every existing grant for this role and insert the new set within a transaction so
        // the operation is atomic. An empty key list therefore clears all of the role's grants.
        // Using an ExecutionStrategy so SQL Server transient-fault retry logic composes correctly
        // with the explicit transaction.
        var permissionIds = requestedKeys
            .Select(key => keyToId[key])
            .Distinct()
            .ToList();

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                // Remove all existing grants for this role — the "delete-all" half of the
                // full-replacement approach.
                var existingGrants = await _dbContext.RolePermissions
                    .Where(rp => rp.RoleId == roleId)
                    .ToListAsync();

                _dbContext.RolePermissions.RemoveRange(existingGrants);

                // Insert one grant per resolved permission ID — the "insert-new" half.
                var newGrants = permissionIds
                    .Select(permissionId => new RolePermission
                    {
                        RoleId = roleId,
                        PermissionId = permissionId
                    })
                    .ToList();

                if (newGrants.Count > 0)
                {
                    _dbContext.RolePermissions.AddRange(newGrants);
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });

        // --- Audit Logging (after successful replacement) ---
        // Record the permission change as a security-sensitive SettingsChanged event on the role.
        // Old/new values capture the before/after permission key sets as camelCase JSON objects
        // shaped as { "permissions": [...] }. Audit failures must never disrupt the primary
        // operation, so the LogAsync call is wrapped in try/catch and failures are swallowed after
        // being logged at Error level.
        try
        {
            await _auditLogService.LogAsync(new AuditLogRequest
            {
                UserId = _currentUserAccessor.UserId,
                ActionType = AuditActionType.SettingsChanged,
                EntityType = AuditEntityType.Role,
                EntityId = roleId,
                EntityName = role.DisplayName ?? role.Name ?? string.Empty,
                Description = $"Permissions for role '{role.DisplayName ?? role.Name}' were updated.",
                OldValues = AuditChangeHelper.Serialize(new { permissions = oldKeys }),
                NewValues = AuditChangeHelper.Serialize(new { permissions = newKeys }),
                IpAddress = _currentUserAccessor.IpAddress
            });
        }
        catch (Exception ex)
        {
            // Swallow audit failures — the permission change has already been committed and must
            // not be rolled back or reported as failed because the audit trail could not be written.
            _logger.LogError(
                ex,
                "Failed to write audit log entry for permission update on role '{RoleId}'.",
                roleId);
        }
    }

    #endregion
}
