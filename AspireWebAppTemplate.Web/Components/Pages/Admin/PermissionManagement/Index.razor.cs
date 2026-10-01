using AspireWebAppTemplate.Application.Features.Permissions;
using AspireWebAppTemplate.Application.Features.Roles;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Admin.PermissionManagement;

/// <summary>
/// Admin page for managing resource-based (<c>Module.Action</c>) permissions per role.
/// Displays a matrix with active roles as columns (ordered by position) and permissions as rows,
/// grouped by their owning module with a section header per module.
/// Each non-Admin toggle auto-saves the role's complete permission set via a full-replacement update.
/// The Admin role column is always fully checked and non-interactive since Admin has immutable full access.
/// </summary>
public partial class Index : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for resource-based permission operations.
    /// </summary>
    [Inject] private ApiPermissionService PermissionService { get; set; } = default!;

    /// <summary>
    /// HTTP client service for fetching available roles (matrix columns).
    /// </summary>
    [Inject] private ApiRoleService RoleService { get; set; } = default!;

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
    /// Whether initial data loading failed. When true, the matrix is not rendered.
    /// </summary>
    private bool HasLoadError { get; set; }

    /// <summary>
    /// Error message displayed when initial data loading fails.
    /// </summary>
    private string? ErrorMessage { get; set; }

    /// <summary>
    /// The list of active roles to display as columns in the matrix, ordered by position ascending.
    /// </summary>
    private List<RoleDto> _roles = [];

    /// <summary>
    /// The permissions grouped by module, used to render the matrix rows with section headers.
    /// </summary>
    private List<PermissionGroupDto> _groups = [];

    /// <summary>
    /// Current permission state: maps RoleId to the set of granted permission keys.
    /// Used both for fast toggle-state lookup and to build the full-replacement update payload.
    /// </summary>
    private readonly Dictionary<string, HashSet<string>> _permissionsByRole = new();

    /// <summary>
    /// Tracks which roles are currently being saved, so their checkboxes can be disabled during save.
    /// </summary>
    private readonly HashSet<string> _savingRoles = new();

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads roles and permissions on component initialization to build the matrix.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    #endregion

    #region Data Loading

    /// <summary>
    /// Loads all data required to render the permission matrix: the grouped permission definitions
    /// (rows), the active roles (columns), and each non-Admin role's current permission set
    /// (toggle seed). On any failure, sets the load-error state so the matrix is not rendered.
    /// </summary>
    private async Task LoadDataAsync()
    {
        IsLoading = true;
        HasLoadError = false;
        ErrorMessage = null;

        try
        {
            // Fetch roles and grouped permissions in parallel.
            var rolesTask = RoleService.GetRolesAsync();
            var permissionsTask = PermissionService.GetAllPermissionsAsync();

            await Task.WhenAll(rolesTask, permissionsTask);

            var rolesResult = rolesTask.Result;
            var permissionsResult = permissionsTask.Result;

            // Columns: only active roles, ordered by position ascending (Req 9.9).
            if (rolesResult.Succeeded && rolesResult.Data is not null)
            {
                _roles = rolesResult.Data
                    .Where(r => r.IsActive)
                    .OrderBy(r => r.Position)
                    .ToList();
            }
            else
            {
                SetLoadError("Failed to load roles.");
                Logger.LogError("Failed to load roles: {Error}", rolesResult.Error);
                return;
            }

            // Rows: permissions grouped by module.
            if (permissionsResult.Succeeded && permissionsResult.Data is not null)
            {
                _groups = permissionsResult.Data;
            }
            else
            {
                SetLoadError("Failed to load permissions.");
                Logger.LogError("Failed to load permissions: {Error}", permissionsResult.Error);
                return;
            }

            // Seed each non-Admin role's current permission set so toggles reflect reality and the
            // full-replacement payload can be built from in-memory state. The Admin column is static.
            foreach (var role in _roles.Where(r => !IsAdminRole(r)))
            {
                var roleResult = await PermissionService.GetRolePermissionsAsync(role.Id);
                if (roleResult.Succeeded && roleResult.Data is not null)
                {
                    _permissionsByRole[role.Id] = new HashSet<string>(
                        roleResult.Data.PermissionKeys,
                        StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    SetLoadError("Failed to load role permissions.");
                    Logger.LogError("Failed to load permissions for role {RoleId}: {Error}", role.Id, roleResult.Error);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            SetLoadError("An unexpected error occurred while loading the permission matrix.");
            Logger.LogError(ex, "Unexpected error loading permission management admin page");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Sets the load-error state so the error message is shown and the matrix is suppressed.
    /// </summary>
    /// <param name="message">The human-readable error message to display.</param>
    private void SetLoadError(string message)
    {
        HasLoadError = true;
        ErrorMessage = message;
    }

    #endregion

    #region Permission Checks

    /// <summary>
    /// Determines whether a role is the Admin role. The Admin role always has full access and its
    /// toggles are rendered checked and disabled with no API call.
    /// </summary>
    /// <param name="role">The role to check.</param>
    /// <returns>True if the role name is "Admin" (case-insensitive).</returns>
    private static bool IsAdminRole(RoleDto role)
        => string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Produces the column label for a role, preferring its display name and falling back to its name.
    /// </summary>
    /// <param name="role">The role whose column label is requested.</param>
    /// <returns>The display name if present; otherwise the role name.</returns>
    private static string RoleColumnLabel(RoleDto role)
        => string.IsNullOrWhiteSpace(role.DisplayName) ? role.Name : role.DisplayName!;

    /// <summary>
    /// Checks whether a specific permission key is granted to the given role, using the in-memory
    /// state for an O(1) lookup.
    /// </summary>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="permissionKey">The permission key to check.</param>
    /// <returns>True if the role has been granted the permission.</returns>
    private bool IsPermissionGranted(string roleId, string permissionKey)
        => _permissionsByRole.TryGetValue(roleId, out var keys) && keys.Contains(permissionKey);

    /// <summary>
    /// Checks whether a role is currently being saved, used to disable its checkboxes during save.
    /// </summary>
    /// <param name="roleId">The role identifier.</param>
    /// <returns>True if a save operation is in progress for this role.</returns>
    private bool IsRoleSaving(string roleId) => _savingRoles.Contains(roleId);

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles a permission toggle change for a role-permission combination. Optimistically updates
    /// the in-memory state, then sends the role's complete permission key set via a full-replacement
    /// update. On success a success Snackbar is shown; on failure (or exception) the toggle is
    /// reverted to its previous state and an error Snackbar is shown. The Admin column never reaches
    /// this handler because its checkbox is disabled.
    /// </summary>
    /// <param name="roleId">The role whose permissions are being modified.</param>
    /// <param name="permissionKey">The permission key being toggled.</param>
    /// <param name="granted">The new desired state (true = grant, false = revoke).</param>
    private async Task OnTogglePermission(string roleId, string permissionKey, bool granted)
    {
        // Prevent concurrent saves for the same role.
        if (_savingRoles.Contains(roleId)) return;

        _savingRoles.Add(roleId);
        StateHasChanged();

        try
        {
            // Ensure a set exists for this role.
            if (!_permissionsByRole.TryGetValue(roleId, out var keys))
            {
                keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _permissionsByRole[roleId] = keys;
            }

            // Optimistically update local state.
            if (granted)
                keys.Add(permissionKey);
            else
                keys.Remove(permissionKey);

            // Build the full-replacement payload from the complete in-memory set for this role.
            var request = new UpdateRolePermissionsRequest { PermissionKeys = keys.ToList() };

            var result = await PermissionService.UpdateRolePermissionsAsync(roleId, request);

            if (result.Succeeded)
            {
                Snackbar.Add("Permissions updated.", Severity.Success);
            }
            else
            {
                // Revert the optimistic update on failure (Req 9.8).
                RevertToggle(keys, permissionKey, granted);

                Snackbar.Add(
                    $"Failed to update permission: {result.Error ?? "Unknown error"}",
                    Severity.Error,
                    config => config.VisibleStateDuration = 5000);

                Logger.LogError("Failed to update permissions for role {RoleId}: {Error}", roleId, result.Error);
            }
        }
        catch (Exception ex)
        {
            // Revert on unexpected exception.
            if (_permissionsByRole.TryGetValue(roleId, out var keys))
                RevertToggle(keys, permissionKey, granted);

            Snackbar.Add(
                "An unexpected error occurred while saving permissions.",
                Severity.Error,
                config => config.VisibleStateDuration = 5000);

            Logger.LogError(ex, "Unexpected error toggling permission {PermissionKey} for role {RoleId}", permissionKey, roleId);
        }
        finally
        {
            _savingRoles.Remove(roleId);
            StateHasChanged();
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Reverts an optimistic toggle on the supplied key set: a failed grant is removed and a failed
    /// revoke is re-added, restoring the previous state.
    /// </summary>
    /// <param name="keys">The role's in-memory permission key set.</param>
    /// <param name="permissionKey">The permission key that was toggled.</param>
    /// <param name="granted">The desired state that failed to persist.</param>
    private static void RevertToggle(HashSet<string> keys, string permissionKey, bool granted)
    {
        if (granted)
            keys.Remove(permissionKey);
        else
            keys.Add(permissionKey);
    }

    #endregion
}
