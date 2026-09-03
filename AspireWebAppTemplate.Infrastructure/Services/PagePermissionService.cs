using Microsoft.Extensions.Logging;
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.Template.Navigation;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Extensions;
using AspireWebAppTemplate.Application.Features.Template.PagePermissions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AspireWebAppTemplate.Infrastructure.Services;

/// <summary>
/// Implements the <see cref="IPagePermissionService"/> interface to manage role-based page access
/// grants using a whitelist model. A <see cref="PagePermission"/> record existing for a role-page
/// combination grants access; absence denies access.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Whitelist model:</strong> Only pages explicitly granted via a <see cref="PagePermission"/>
/// record are accessible. If no record exists for a role-page pair, the role is denied access to
/// that page. The Admin role is a special case — it always has full access regardless of database
/// records and cannot be modified through this service.
/// </para>
/// <para>
/// <strong>Full-replacement strategy:</strong> The <see cref="UpdateRolePermissionsAsync"/> method
/// uses a delete-all-then-insert-new approach within a transaction. This simplifies concurrency
/// handling and ensures the database always reflects the exact set of permissions the caller
/// specified, with no leftover grants from previous configurations.
/// </para>
/// <para>
/// Registered as a scoped service to align with the per-request <see cref="ApplicationDbContext"/>
/// lifetime.
/// </para>
/// </remarks>
public class PagePermissionService : IPagePermissionService
{
    #region Constructor

    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly INavigationProvider _navigationProvider;
    private readonly ILogger<PagePermissionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PagePermissionService"/> class.
    /// </summary>
    /// <param name="dbContext">The application database context for querying and persisting page permissions.</param>
    /// <param name="userManager">The ASP.NET Core Identity user manager for resolving user role assignments.</param>
    /// <param name="roleManager">The ASP.NET Core Identity role manager for validating role existence.</param>
    /// <param name="navigationProvider">The navigation provider that defines the canonical set of valid page paths.</param>
    /// <param name="logger">The logger instance for recording warnings and errors.</param>
    public PagePermissionService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        INavigationProvider navigationProvider,
        ILogger<PagePermissionService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _navigationProvider = navigationProvider;
        _logger = logger;
    }

    #endregion

    #region Query Operations

    /// <inheritdoc />
    public async Task<List<RolePermissionsDto>> GetAllPermissionsAsync()
    {
        // Query all PagePermission records and group by role.
        // Include the Role navigation property to get the role's display name.
        var permissions = await _dbContext.PagePermissions
            .AsNoTracking()
            .Include(p => p.Role)
            .ToListAsync();

        // Group permissions by RoleId and project into DTOs.
        // Each group becomes a RolePermissionsDto containing all granted pages for that role.
        var grouped = permissions
            .GroupBy(p => new { p.RoleId, RoleName = p.Role.Name ?? p.RoleId })
            .Select(g => new RolePermissionsDto
            {
                RoleId = g.Key.RoleId,
                RoleName = g.Key.RoleName,
                Pages = g.Select(p => new PagePermissionDto
                {
                    PagePath = p.PagePath,
                    PageDisplayName = p.PageDisplayName
                }).ToList()
            })
            .ToList();

        return grouped;
    }

    /// <inheritdoc />
    public async Task<List<string>> GetMyPagesAsync(string userId)
    {
        // Resolve all roles assigned to the user via ASP.NET Core Identity.
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            // User not found — return empty list (no permissions).
            return [];
        }

        var roleNames = await _userManager.GetRolesAsync(user);
        if (roleNames.Count == 0)
        {
            // User has no assigned roles — return empty list per requirement.
            return [];
        }

        // Resolve role IDs from role names so we can query PagePermissions by RoleId.
        var roleIds = await _roleManager.Roles
            .AsNoTracking()
            .Where(r => roleNames.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync();

        if (roleIds.Count == 0)
        {
            return [];
        }

        // Query the union of all page paths across all of the user's roles.
        // Case-insensitive comparison is enforced by the database collation and
        // the consuming code (PagePermissionContext uses OrdinalIgnoreCase HashSet).
        var pagePaths = await _dbContext.PagePermissions
            .AsNoTracking()
            .Where(p => roleIds.Contains(p.RoleId))
            .Select(p => p.PagePath)
            .Distinct()
            .ToListAsync();

        return pagePaths;
    }

    #endregion

    #region Write Operations

    /// <inheritdoc />
    public async Task UpdateRolePermissionsAsync(string roleId, List<string> pagePaths)
    {
        // --- Validation Logic ---
        // Step 1: Validate that the roleId corresponds to an existing role (404 if not found).
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role is null)
        {
            throw new KeyNotFoundException($"Role with ID '{roleId}' was not found.");
        }

        // Step 2: Reject attempts to modify the Admin role's permissions.
        // The Admin role always has implicit full access to all pages — its permissions
        // cannot be modified through this endpoint to prevent accidental lockout.
        if (string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Admin role permissions cannot be modified.");
        }

        // Step 3: Reject attempts to modify system roles.
        // System roles (IsSystem = true) are protected from all modifications including
        // permission changes, to maintain system integrity.
        if (role.IsSystem)
        {
            throw new InvalidOperationException(
                $"Permissions for system role '{role.Name}' cannot be modified.");
        }

        // Step 4: Validate that all provided PagePaths exist in the DefaultNavigationProvider.
        // Only pages registered in the navigation provider are valid permission targets.
        // This prevents granting access to non-existent or decommissioned pages.
        var validPages = _navigationProvider.GetAllValidPagePaths();
        var invalidPaths = pagePaths
            .Where(path => !validPages.Contains(path))
            .ToList();

        if (invalidPaths.Count > 0)
        {
            throw new ArgumentException(
                $"The following page paths are not registered in the navigation provider: {string.Join(", ", invalidPaths)}");
        }

        // --- Full-Replacement Strategy ---
        // Delete all existing permissions for this role and insert the new set within a
        // transaction. This ensures atomicity: either all permissions are updated or none are.
        // Using ExecutionStrategy to handle SQL Server transient fault retry logic correctly
        // when wrapping operations in an explicit transaction.
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                // Delete all existing page permission records for this role.
                // This is the "full replacement" approach — we remove everything and re-insert.
                var existingPermissions = await _dbContext.PagePermissions
                    .Where(p => p.RoleId == roleId)
                    .ToListAsync();

                _dbContext.PagePermissions.RemoveRange(existingPermissions);

                // Insert new permission records for each provided page path.
                // Look up display names from the navigation provider for consistency.
                var pageDisplayNames = _navigationProvider.GetPageDisplayNameMap();

                var newPermissions = pagePaths.Select(path => new PagePermission
                {
                    RoleId = roleId,
                    PagePath = path,
                    PageDisplayName = pageDisplayNames.GetValueOrDefault(path, path)
                }).ToList();

                if (newPermissions.Count > 0)
                {
                    _dbContext.PagePermissions.AddRange(newPermissions);
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Updated page permissions for role '{RoleName}' (ID: {RoleId}). Granted {Count} page(s).",
                    role.Name,
                    roleId,
                    newPermissions.Count);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    #endregion
}
