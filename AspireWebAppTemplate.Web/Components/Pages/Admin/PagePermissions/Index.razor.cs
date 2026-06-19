using AspireWebAppTemplate.Core.Application.Abstractions;
using AspireWebAppTemplate.Core.Contracts.PagePermissions;
using AspireWebAppTemplate.Core.Contracts.Roles;
using AspireWebAppTemplate.Core.Extensions;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Admin.PagePermissions;

/// <summary>
/// Admin page for managing page-level permissions per role.
/// Displays a matrix with roles as columns and pages as rows.
/// Administrators can toggle access for each role-page combination.
/// The Admin role column is always fully checked and non-interactive.
/// System_Pages are excluded from the matrix since they are always accessible.
/// </summary>
public partial class Index : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for page permission CRUD operations.
    /// </summary>
    [Inject] private ApiPagePermissionService PermissionService { get; set; } = default!;

    /// <summary>
    /// HTTP client service for fetching available roles.
    /// </summary>
    [Inject] private ApiRoleService RoleService { get; set; } = default!;

    /// <summary>
    /// Provides the application's navigation structure to extract configurable pages.
    /// </summary>
    [Inject] private INavigationProvider NavigationProvider { get; set; } = default!;

    /// <summary>
    /// Structured logger for diagnostics.
    /// </summary>
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// Whether the page is currently loading initial data.
    /// </summary>
    private bool IsLoading { get; set; } = true;

    /// <summary>
    /// Error message displayed in the alert banner.
    /// </summary>
    private string? ErrorMessage { get; set; }

    /// <summary>
    /// The list of roles to display as columns in the matrix.
    /// </summary>
    private List<RoleDto> _roles = [];

    /// <summary>
    /// The list of configurable page rows extracted from the navigation provider.
    /// Excludes System_Pages that are always accessible.
    /// </summary>
    private List<PageRow> _pageRows = [];

    /// <summary>
    /// Current permission state: maps RoleId → set of granted PagePaths.
    /// Used for fast lookup when rendering toggle states.
    /// </summary>
    private Dictionary<string, HashSet<string>> _permissionsByRole = new();

    /// <summary>
    /// Tracks which roles are currently being saved (to disable toggles during save).
    /// </summary>
    private readonly HashSet<string> _savingRoles = new();

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads roles, permissions, and page list on component initialization.
    /// Fetches data from the API and navigation provider to build the matrix.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    #endregion

    #region Data Loading

    /// <summary>
    /// Loads all data required to render the permission matrix:
    /// roles from the roles API, permissions from the page permissions API,
    /// and configurable pages from the DefaultNavigationProvider.
    /// </summary>
    private async Task LoadDataAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // Fetch roles and permissions in parallel
            var rolesTask = RoleService.GetRolesAsync();
            var permissionsTask = PermissionService.GetAllPermissionsAsync();

            await Task.WhenAll(rolesTask, permissionsTask);

            var rolesResult = rolesTask.Result;
            var permissionsResult = permissionsTask.Result;

            // Process roles - only show active roles
            if (rolesResult.Succeeded && rolesResult.Data is not null)
            {
                _roles = rolesResult.Data
                    .Where(r => r.IsActive)
                    .OrderBy(r => r.Position)
                    .ToList();
            }
            else
            {
                ErrorMessage = "Failed to load roles.";
                Logger.LogError("Failed to load roles: {Error}", rolesResult.Error);
                return;
            }

            // Process permissions - build lookup dictionary for O(1) checks
            if (permissionsResult.Succeeded && permissionsResult.Data is not null)
            {
                _permissionsByRole = permissionsResult.Data.ToDictionary(
                    rp => rp.RoleId,
                    rp => new HashSet<string>(
                        rp.Pages.Select(p => p.PagePath),
                        StringComparer.OrdinalIgnoreCase));
            }
            else
            {
                ErrorMessage = "Failed to load permissions.";
                Logger.LogError("Failed to load permissions: {Error}", permissionsResult.Error);
                return;
            }

            // Extract configurable pages from navigation provider (Link items only, excluding System_Pages)
            _pageRows = ExtractPageRows();
        }
        catch (Exception ex)
        {
            ErrorMessage = "An unexpected error occurred while loading the permission matrix.";
            Logger.LogError(ex, "Unexpected error loading page permissions admin page");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Extracts all Link-type NavItems from the navigation provider hierarchy,
    /// excluding System_Pages. Uses the shared extension method for consistency.
    /// </summary>
    /// <returns>A flat list of <see cref="PageRow"/> entries for the matrix.</returns>
    private List<PageRow> ExtractPageRows()
    {
        return NavigationProvider.GetAllLinkPages()
            .Select(p => new PageRow
            {
                PagePath = p.PagePath,
                DisplayName = p.DisplayName
            })
            .ToList();
    }

    #endregion

    #region Permission Checks

    /// <summary>
    /// Determines whether a role is the Admin role.
    /// Admin role always has full access and its toggles are disabled.
    /// </summary>
    /// <param name="role">The role to check.</param>
    /// <returns>True if the role name is "Admin" (case-insensitive).</returns>
    private static bool IsAdminRole(RoleDto role)
        => string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks whether a specific permission grant exists for the given role and page.
    /// Uses the in-memory dictionary for O(1) lookup.
    /// </summary>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="pagePath">The page path to check.</param>
    /// <returns>True if the role has been granted access to the page.</returns>
    private bool IsPermissionGranted(string roleId, string pagePath)
    {
        if (_permissionsByRole.TryGetValue(roleId, out var pages))
            return pages.Contains(pagePath);
        return false;
    }

    /// <summary>
    /// Checks whether a role is currently being saved (toggle disabled state).
    /// </summary>
    /// <param name="roleId">The role identifier.</param>
    /// <returns>True if a save operation is in progress for this role.</returns>
    private bool IsRoleSaving(string roleId) => _savingRoles.Contains(roleId);

    #endregion

    #region Permission Toggle

    /// <summary>
    /// Handles a permission toggle change for a role-page combination.
    /// Sends the complete updated permission list via PUT endpoint (full replacement).
    /// On failure, reverts the toggle to its previous state and shows an error snackbar.
    /// </summary>
    /// <param name="roleId">The role whose permissions are being modified.</param>
    /// <param name="pagePath">The page path being toggled.</param>
    /// <param name="granted">The new desired state (true = grant, false = revoke).</param>
    private async Task OnTogglePermission(string roleId, string pagePath, bool granted)
    {
        // Prevent concurrent saves for the same role
        if (_savingRoles.Contains(roleId)) return;

        _savingRoles.Add(roleId);
        StateHasChanged();

        try
        {
            // Ensure we have a set for this role
            if (!_permissionsByRole.ContainsKey(roleId))
                _permissionsByRole[roleId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Optimistically update local state
            if (granted)
                _permissionsByRole[roleId].Add(pagePath);
            else
                _permissionsByRole[roleId].Remove(pagePath);

            // Build the full replacement list for the PUT request
            var updatedPaths = _permissionsByRole[roleId].ToList();
            var request = new UpdateRolePermissionsRequest { PagePaths = updatedPaths };

            // Call the API with the complete updated permission set
            var result = await PermissionService.UpdateRolePermissionsAsync(roleId, request);

            if (!result.Succeeded)
            {
                // Revert the optimistic update on failure
                if (granted)
                    _permissionsByRole[roleId].Remove(pagePath);
                else
                    _permissionsByRole[roleId].Add(pagePath);

                // Show error snackbar that auto-dismisses after 5 seconds
                Snackbar.Add(
                    $"Failed to update permission: {result.Error ?? "Unknown error"}",
                    Severity.Error,
                    config => config.VisibleStateDuration = 5000);

                Logger.LogError("Failed to update permissions for role {RoleId}: {Error}", roleId, result.Error);
            }
        }
        catch (Exception ex)
        {
            // Revert on unexpected exception
            if (granted)
                _permissionsByRole[roleId].Remove(pagePath);
            else
                _permissionsByRole[roleId].Add(pagePath);

            Snackbar.Add(
                "An unexpected error occurred while saving permissions.",
                Severity.Error,
                config => config.VisibleStateDuration = 5000);

            Logger.LogError(ex, "Unexpected error toggling permission for role {RoleId}, page {PagePath}", roleId, pagePath);
        }
        finally
        {
            _savingRoles.Remove(roleId);
            StateHasChanged();
        }
    }

    #endregion

    #region View Models

    /// <summary>
    /// Represents a single page row in the permission matrix.
    /// Extracted from the navigation provider's Link-type NavItems.
    /// </summary>
    public class PageRow
    {
        /// <summary>
        /// The route path of the page (e.g., "/admin/audit-log").
        /// </summary>
        public string PagePath { get; set; } = "";

        /// <summary>
        /// The human-readable display name of the page (e.g., "Audit Log").
        /// </summary>
        public string DisplayName { get; set; } = "";
    }

    #endregion
}
