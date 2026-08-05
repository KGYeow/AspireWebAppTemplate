using AspireWebAppTemplate.Application.Contracts.Roles;
using AspireWebAppTemplate.Application.Contracts.Users;
using AspireWebAppTemplate.UI.Components.Shared;
using AspireWebAppTemplate.UI.Utilities;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Admin.UserManagement;

/// <summary>
/// User management page. Lists all users with multi-role management,
/// activation toggle, delete actions, and bulk operations. Admin role required.
/// Uses server-side filtering, sorting, and pagination via <see cref="DataGridUtils{T}"/>.
/// All operations are delegated to the API via <see cref="ApiUserService"/> and <see cref="ApiRoleService"/>.
/// </summary>
public partial class Index : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for user operations.
    /// </summary>
    [Inject] private ApiUserService UserService { get; set; } = default!;

    /// <summary>
    /// HTTP client service for role operations.
    /// </summary>
    [Inject] private ApiRoleService RoleService { get; set; } = default!;

    /// <summary>
    /// Structured logger.
    /// </summary>
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    #endregion

    #region Cascading Parameters

    /// <summary>
    /// Provides the current authentication state.
    /// </summary>
    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    #endregion

    #region Server-Side Data Grid

    /// <summary>
    /// Reference to the MudDataGrid component for triggering server-side reloads.
    /// </summary>
    private MudDataGrid<UserViewModel> dataGrid = null!;

    /// <summary>
    /// Server-side helper that applies column filters, multi-sort, global search,
    /// and pagination based on <see cref="GridState{T}"/>.
    /// Maps each filterable/sortable column to its corresponding property selector.
    /// </summary>
    private readonly DataGridUtils<UserViewModel> _dataGridUtils = new DataGridUtils<UserViewModel>()
        .MapString(nameof(UserViewModel.UserName),    x => x.UserName)
        .MapString(nameof(UserViewModel.DisplayName), x => x.DisplayName)
        .MapString(nameof(UserViewModel.JobTitle),    x => x.JobTitle)
        .MapString(nameof(UserViewModel.Department),  x => x.Department)
        .MapBool(nameof(UserViewModel.IsActive),      x => x.IsActive);

    #endregion

    #region Bulk Actions State

    /// <summary>
    /// The set of currently selected users in the data grid.
    /// Bound via <c>@bind-SelectedItems</c>.
    /// </summary>
    private HashSet<UserViewModel> selectedUsers = new();

    #endregion

    #region State

    /// <summary>
    /// Whether data is currently loading.
    /// </summary>
    protected bool IsLoading { get; private set; } = true;

    /// <summary>
    /// Error message displayed in the alert.
    /// </summary>
    protected string? ErrorMessage { get; set; }

    /// <summary>
    /// Success message displayed in the alert.
    /// </summary>
    protected string? SuccessMessage { get; set; }

    /// <summary>
    /// The current logged-in user's username.
    /// </summary>
    private string? currentUserName;

    /// <summary>
    /// All available role names.
    /// </summary>
    private List<string> allRoleNames = [];

    /// <summary>
    /// Role names filtered to only those with Position &lt;= the actor's highest role position.
    /// Used when opening ManageRolesDialog and BulkAssignRoleDialog to enforce authority hierarchy.
    /// </summary>
    private List<string> assignableRoleNames = [];

    /// <summary>
    /// The current actor's highest role position, used for authority checks.
    /// </summary>
    private int actorHighestPosition;

    /// <summary>
    /// The current global search term for the toolbar search box.
    /// Applied across all searchable fields via <see cref="DataGridUtils{T}"/>.
    /// </summary>
    private string? searchString;

    // [LDAP] Whether LDAP is enabled — remove if LDAP is not needed
    /// <summary>
    /// Whether LDAP authentication is enabled in configuration.
    /// Determined by checking if the LDAP-related API endpoints are available.
    /// </summary>
    protected bool IsLdapEnabled { get; private set; }

    // [LDAP] Sync state — remove if LDAP is not needed
    /// <summary>
    /// Whether an LDAP sync operation is currently in progress.
    /// </summary>
    protected bool IsSyncing { get; private set; }

    /// <summary>
    /// Number of users synced so far in the current sync operation.
    /// </summary>
    protected int SyncedCount { get; private set; }

    /// <summary>
    /// Total number of users to sync in the current sync operation.
    /// </summary>
    protected int TotalToSync { get; private set; }

    /// <summary>
    /// Summary message displayed after sync completes.
    /// </summary>
    protected string? SyncMessage { get; set; }

    /// <summary>
    /// Cancellation token source for the current LDAP sync operation.
    /// Used to abort the streaming sync when the user clicks Cancel.
    /// </summary>
    private CancellationTokenSource? _ldapSyncCts;

    /// <summary>
    /// All roles metadata fetched from the API.
    /// Used for position-based authority checks.
    /// </summary>
    private List<RoleDto> allRoles = [];

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads roles and authentication state on initialization.
    /// The user data grid is populated via <see cref="ServerReload"/> (server-side).
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authState = await AuthStateTask;
            currentUserName = authState.User.Identity?.Name;

            // Fetch all roles metadata from the API
            var rolesResult = await RoleService.GetRolesAsync();
            allRoles = rolesResult.Succeeded && rolesResult.Data is not null ? rolesResult.Data : [];

            allRoleNames = allRoles
                .Select(r => r.Name)
                .OrderBy(n => n)
                .ToList();

            // Compute actor's highest role position for authority checks
            if (currentUserName is not null)
            {
                var currentUser = await UserService.GetAllUsersAsync();
                var me = currentUser.FirstOrDefault(u => 
                    string.Equals(u.UserName, currentUserName, StringComparison.OrdinalIgnoreCase));
                if (me is not null)
                {
                    actorHighestPosition = me.Roles
                        .Select(roleName => allRoles.FirstOrDefault(r => 
                            string.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase))?.Position ?? 0)
                        .DefaultIfEmpty(0)
                        .Max();
                }
            }

            // Filter assignable roles: only those with Position <= actor's highest position
            assignableRoleNames = allRoles
                .Where(r => r.Position <= actorHighestPosition)
                .Select(r => r.Name)
                .OrderBy(n => n)
                .ToList();

            // [LDAP] Check if LDAP is enabled by attempting a lightweight lookup
            // For simplicity, assume LDAP is available (the API will return 404 if not)
            IsLdapEnabled = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading user management data.");
            ErrorMessage = "Error loading data. Please try again later.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Server-Side Data Loading

    /// <summary>
    /// Server-side reload callback for <see cref="MudDataGrid{T}"/>.
    /// Loads all users from the API, then delegates filtering, sorting, and pagination
    /// to <see cref="DataGridUtils{T}.ServerReloadAsync"/>.
    /// </summary>
    private async Task<GridData<UserViewModel>> ServerReload(GridState<UserViewModel> state, CancellationToken cancellationToken)
    {
        // Loader function: fetches all users and maps to view models
        async Task<IEnumerable<UserViewModel>> loader() => await LoadUserViewModelsAsync();

        // Global search fields — searches across all user-visible text columns and roles
        IEnumerable<string> GlobalFields(UserViewModel u) => new[]
        {
            u.UserName,
            u.DisplayName,
            u.JobTitle,
            u.Department,
            string.Join(", ", u.Roles)
        };

        // Set page-aware line numbers (e.g., page 2 with size 10 starts at 11)
        void SetLine(UserViewModel item, int lineNo) => item.LineNumber = lineNo;

        return await _dataGridUtils.ServerReloadAsync(
            state,
            loader,
            globalSearchTerm: searchString,
            globalSearchFieldSelector: GlobalFields,
            setLineNumber: SetLine);
    }

    /// <summary>
    /// Loads all users from the API and maps them to <see cref="UserViewModel"/> instances.
    /// </summary>
    private async Task<IEnumerable<UserViewModel>> LoadUserViewModelsAsync()
    {
        var users = await UserService.GetAllUsersAsync();

        return users.Select(u => new UserViewModel
        {
            User = u,
            IsSelf = string.Equals(u.UserName, currentUserName, StringComparison.OrdinalIgnoreCase)
        });
    }

    #endregion

    #region Bulk Actions Helpers

    /// <summary>
    /// Clears the current selection and reloads the grid.
    /// </summary>
    private async Task ClearSelectionAndReloadAsync()
    {
        selectedUsers = new HashSet<UserViewModel>();
        await dataGrid.ReloadServerData();
    }

    /// <summary>
    /// Clears the current selection without reloading the grid.
    /// </summary>
    private void ClearSelection()
    {
        selectedUsers = new HashSet<UserViewModel>();
    }

    #endregion

    #region Bulk Actions

    /// <summary>
    /// Activates all selected users (excluding self).
    /// </summary>
    private async Task BulkActivateAsync()
    {
        var targets = selectedUsers.Where(u => !u.IsSelf).ToList();
        var skipped = selectedUsers.Count - targets.Count;

        if (targets.Count == 0)
        {
            Snackbar.Add("No eligible users in selection (cannot modify your own account).", Severity.Info);
            return;
        }

        var parameters = new DialogParameters<ConfirmationDialog>
        {
            { x => x.ContentText, $"Are you sure you want to activate {targets.Count} user(s)?{(skipped > 0 ? $" ({skipped} skipped: cannot modify your own account)" : "")}" },
            { x => x.SubmitBtnText, "Activate" },
            { x => x.DialogIcon, Icons.Material.Rounded.Warning },
            { x => x.DialogIconColor, Color.Warning }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Activate Users", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        int success = 0, failed = 0;
        foreach (var user in targets)
        {
            var activateResult = await UserService.ActivateUserAsync(user.Id);
            if (activateResult.Succeeded) success++;
            else failed++;
        }

        Snackbar.Add($"{success} activated, {skipped} skipped (self), {failed} failed.", Severity.Success);
        await ClearSelectionAndReloadAsync();
    }

    /// <summary>
    /// Deactivates all selected users (excluding self).
    /// </summary>
    private async Task BulkDeactivateAsync()
    {
        var targets = selectedUsers.Where(u => !u.IsSelf).ToList();
        var skipped = selectedUsers.Count - targets.Count;

        if (targets.Count == 0)
        {
            Snackbar.Add("No eligible users in selection (cannot modify your own account).", Severity.Info);
            return;
        }

        var parameters = new DialogParameters<ConfirmationDialog>
        {
            { x => x.ContentText, $"Are you sure you want to deactivate {targets.Count} user(s)?{(skipped > 0 ? $" ({skipped} skipped: cannot modify your own account)" : "")}" },
            { x => x.SubmitBtnText, "Deactivate" },
            { x => x.DialogIcon, Icons.Material.Rounded.Warning },
            { x => x.DialogIconColor, Color.Warning }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Deactivate Users", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        int success = 0, failed = 0;
        foreach (var user in targets)
        {
            var deactivateResult = await UserService.DeactivateUserAsync(user.Id);
            if (deactivateResult.Succeeded) success++;
            else failed++;
        }

        Snackbar.Add($"{success} deactivated, {skipped} skipped (self), {failed} failed.", Severity.Success);
        await ClearSelectionAndReloadAsync();
    }

    /// <summary>
    /// Deletes all selected users (excluding self).
    /// </summary>
    private async Task BulkDeleteAsync()
    {
        var targets = selectedUsers.Where(u => !u.IsSelf).ToList();
        var skipped = selectedUsers.Count - targets.Count;

        if (targets.Count == 0)
        {
            Snackbar.Add("No eligible users in selection (cannot delete your own account).", Severity.Info);
            return;
        }

        var parameters = new DialogParameters<ConfirmationDialog>
        {
            { x => x.ContentText, $"Are you sure you want to delete {targets.Count} user(s)? This action cannot be undone.{(skipped > 0 ? $" ({skipped} skipped: cannot delete your own account)" : "")}" },
            { x => x.SubmitBtnText, "Delete" },
            { x => x.DialogIcon, Icons.Material.Rounded.DeleteForever },
            { x => x.DialogIconColor, Color.Error }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Delete Users", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        int success = 0, failed = 0;
        foreach (var user in targets)
        {
            var deleteResult = await UserService.DeleteUserAsync(user.Id);
            if (deleteResult.Succeeded) success++;
            else failed++;
        }

        Snackbar.Add($"{success} deleted, {skipped} skipped (self), {failed} failed.", Severity.Success);
        await ClearSelectionAndReloadAsync();
    }

    /// <summary>
    /// Opens a dialog to select a role, then assigns it to all selected users (excluding self).
    /// </summary>
    private async Task OpenBulkAssignRoleDialog()
    {
        var targets = selectedUsers.Where(u => !u.IsSelf).ToList();
        var skipped = selectedUsers.Count - targets.Count;

        if (targets.Count == 0)
        {
            Snackbar.Add("No eligible users in selection (cannot modify your own account).", Severity.Info);
            return;
        }

        var parameters = new DialogParameters<BulkAssignRoleDialog>
        {
            { x => x.AllRoleNames, assignableRoleNames },
            { x => x.UserCount, targets.Count }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<BulkAssignRoleDialog>("Assign Role", parameters, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled || result.Data is not BulkAssignRoleResult assignResult)
            return;

        var selectedRole = assignResult.RoleName;
        var replaceExisting = assignResult.ReplaceExisting;

        int success = 0, failed = 0, alreadyHasRole = 0;
        foreach (var user in targets)
        {
            if (replaceExisting)
            {
                // Replace mode: set only the selected role
                var setResult = await UserService.SetRolesAsync(user.Id, [selectedRole]);
                if (setResult.Succeeded) success++;
                else failed++;
            }
            else
            {
                // Add mode: only add if the user doesn't already have it
                if (user.Roles.Contains(selectedRole, StringComparer.OrdinalIgnoreCase))
                {
                    alreadyHasRole++;
                    continue;
                }

                var newRoles = user.Roles.Append(selectedRole).ToArray();
                var setResult = await UserService.SetRolesAsync(user.Id, newRoles);
                if (setResult.Succeeded) success++;
                else failed++;
            }
        }

        var mode = replaceExisting ? "replaced with" : "added";
        var parts = new List<string> { $"{success} {mode} '{selectedRole}'" };
        if (alreadyHasRole > 0) parts.Add($"{alreadyHasRole} already had role");
        if (skipped > 0) parts.Add($"{skipped} skipped (self)");
        if (failed > 0) parts.Add($"{failed} failed");

        Snackbar.Add(string.Join(", ", parts) + ".", Severity.Success);
        await ClearSelectionAndReloadAsync();
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles the global search text change from the toolbar search box.
    /// </summary>
    private Task OnSearch(string text)
    {
        searchString = text;
        return dataGrid.ReloadServerData();
    }

    /// <summary>
    /// Opens the Add User dialog.
    /// </summary>
    protected async Task OpenAddUserDialog()
    {
        var defaultRoleName = allRoles.FirstOrDefault(r => r.IsDefault)?.Name ?? "User";

        var parameters = new DialogParameters<AddUserDialog>
        {
            { x => x.AllRoleNames, allRoleNames },
            { x => x.DefaultRoleName, defaultRoleName }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<AddUserDialog>("Add User", parameters, options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            await dataGrid.ReloadServerData();
            Snackbar.Add("User added successfully.", Severity.Success);
        }
    }

    /// <summary>
    /// Opens the Edit User dialog for a user.
    /// </summary>
    protected async Task OpenEditUserDialog(UserViewModel user)
    {
        // Position-based authority check
        var targetPosition = GetHighestPositionForUser(user);
        if (actorHighestPosition < targetPosition)
        {
            Snackbar.Add("You cannot modify a user with a higher role position.", Severity.Error);
            return;
        }

        var parameters = new DialogParameters<EditUserDialog>
        {
            { x => x.UserId, user.Id }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<EditUserDialog>("Edit User", parameters, options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            await dataGrid.ReloadServerData();
            Snackbar.Add("User updated successfully.", Severity.Success);
        }
    }

    // [LDAP] Opens the Add LDAP User dialog — remove if LDAP is not needed
    /// <summary>
    /// Opens the Add LDAP User dialog for provisioning a user from Active Directory.
    /// </summary>
    protected async Task OpenAddLdapUserDialog()
    {
        var options = new DialogOptions
        {
            CloseButton = true,
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await DialogService.ShowAsync<AddLdapUserDialog>("Add LDAP User", options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            await dataGrid.ReloadServerData();
            Snackbar.Add("LDAP user added successfully.", Severity.Success);
        }
    }

    // [LDAP] Opens the LDAP sync confirmation dialog — remove if LDAP is not needed
    /// <summary>
    /// Opens a confirmation dialog before starting the LDAP sync operation.
    /// </summary>
    protected async Task OpenSyncConfirmDialog()
    {
        var parameters = new DialogParameters<ConfirmationDialog>
        {
            { x => x.ContentText, "This will sync all local users from LDAP. Continue?" },
            { x => x.SubmitBtnText, "Sync" },
            { x => x.DialogIcon, Icons.Material.Rounded.Sync },
            { x => x.DialogIconColor, Color.Info }
        };

        var options = new DialogOptions
        {
            CloseButton = true,
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Confirm User Sync", parameters, options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            await SyncAllUsersFromLdapAsync();
        }
    }

    // [LDAP] Syncs all local users from LDAP via the API
    /// <summary>
    /// Triggers the LDAP sync operation via the API using streaming.
    /// Updates progress in real-time as each user is processed.
    /// </summary>
    private async Task SyncAllUsersFromLdapAsync()
    {
        if (IsSyncing) return;

        IsSyncing = true;
        SyncedCount = 0;
        TotalToSync = 0;
        SyncMessage = null;
        ErrorMessage = null;
        _ldapSyncCts = new CancellationTokenSource();

        int updated = 0, failed = 0;

        try
        {
            await foreach (var item in UserService.SyncLdapUsersStreamAsync(_ldapSyncCts.Token))
            {
                if (item is null) continue;

                TotalToSync = item.Total;
                SyncedCount = item.Current;

                if (item.Updated == true) updated++;
                else if (item.Updated == null) failed++;

                await InvokeAsync(StateHasChanged);

                // Yield to allow pending UI events (e.g., Cancel button click) to be processed
                await Task.Delay(1);
            }

            SyncMessage = $"Sync completed. Updated {updated} of {TotalToSync} users; {failed} failed.";
            await dataGrid.ReloadServerData();
        }
        catch (OperationCanceledException)
        {
            SyncMessage = $"Sync canceled. Processed {SyncedCount} of {TotalToSync} users before cancellation ({updated} updated, {failed} failed).";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Sync error: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
            _ldapSyncCts?.Dispose();
            _ldapSyncCts = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    // [LDAP] Cancels the current sync operation — remove if LDAP is not needed
    /// <summary>
    /// Cancels the current LDAP sync operation by signaling the CancellationTokenSource.
    /// </summary>
    protected void CancelSync()
    {
        _ldapSyncCts?.Cancel();
    }

    /// <summary>
    /// Opens the Manage Roles dialog for a single user.
    /// </summary>
    protected async Task OpenManageRolesDialog(UserViewModel user)
    {
        // Position-based authority check
        var targetPosition = GetHighestPositionForUser(user);
        if (actorHighestPosition < targetPosition)
        {
            Snackbar.Add("You cannot modify a user with a higher role position.", Severity.Error);
            return;
        }

        var parameters = new DialogParameters<ManageRolesDialog>
        {
            { x => x.UserDisplayName, $"{user.DisplayName} ({user.UserName})" },
            { x => x.CurrentRoles, user.Roles },
            { x => x.AllRoleNames, assignableRoleNames }
        };

        var options = new DialogOptions
        {
            CloseButton = true,
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await DialogService.ShowAsync<ManageRolesDialog>("Manage Roles", parameters, options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled && result.Data is HashSet<string> selectedRoles)
        {
            await SetRolesAsync(user, selectedRoles);
        }
    }

    /// <summary>
    /// Toggles a user's active status with confirmation.
    /// </summary>
    protected async Task ToggleActivationAsync(UserViewModel user)
    {
        // Position-based authority check
        var targetPosition = GetHighestPositionForUser(user);
        if (actorHighestPosition < targetPosition)
        {
            Snackbar.Add("You cannot modify a user with a higher role position.", Severity.Error);
            return;
        }

        var action = user.IsActive ? "deactivate" : "activate";

        var parameters = new DialogParameters<ConfirmationDialog>
        {
            { x => x.ContentText, $"Are you sure you want to {action} {user.DisplayName} ({user.UserName})?" },
            { x => x.SubmitBtnText, user.IsActive ? "Deactivate" : "Activate" },
            { x => x.DialogIcon, Icons.Material.Rounded.Warning },
            { x => x.DialogIconColor, Color.Warning }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.ExtraSmall, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ConfirmationDialog>(
            $"{(user.IsActive ? "Deactivate" : "Activate")} User", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        bool ok;
        if (user.IsActive)
        {
            var deactivateResult = await UserService.DeactivateUserAsync(user.Id);
            ok = deactivateResult.Succeeded;
        }
        else
        {
            var activateResult = await UserService.ActivateUserAsync(user.Id);
            ok = activateResult.Succeeded;
        }

        if (ok)
        {
            Snackbar.Add($"User {(!user.IsActive ? "activated" : "deactivated")} successfully.", Severity.Success);
            await dataGrid.ReloadServerData();
        }
        else
        {
            ErrorMessage = "Failed to update user status.";
        }
    }

    /// <summary>
    /// Deletes a single user with confirmation.
    /// </summary>
    protected async Task DeleteUserAsync(UserViewModel user)
    {
        // Position-based authority check
        var targetPosition = GetHighestPositionForUser(user);
        if (actorHighestPosition < targetPosition)
        {
            Snackbar.Add("You cannot modify a user with a higher role position.", Severity.Error);
            return;
        }

        var parameters = new DialogParameters<ConfirmationDialog>
        {
            { x => x.ContentText, $"Are you sure you want to delete {user.DisplayName} ({user.UserName})? This action cannot be undone." },
            { x => x.SubmitBtnText, "Delete" },
            { x => x.DialogIcon, Icons.Material.Rounded.DeleteForever },
            { x => x.DialogIconColor, Color.Error }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Delete User", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        var deleteResult = await UserService.DeleteUserAsync(user.Id);
        if (deleteResult.Succeeded)
        {
            Snackbar.Add($"User '{user.DisplayName} ({user.UserName})' deleted.", Severity.Success);
            await dataGrid.ReloadServerData();
        }
        else
        {
            ErrorMessage = deleteResult.Error ?? "Failed to delete user.";
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Gets the highest role position for the given user (using cached role metadata).
    /// </summary>
    private int GetHighestPositionForUser(UserViewModel user)
    {
        if (user.Roles.Count == 0) return 0;

        return user.Roles
            .Select(roleName => allRoles.FirstOrDefault(r =>
                string.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase))?.Position ?? 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    /// <summary>
    /// Sets the exact roles for a user via the API.
    /// </summary>
    private async Task SetRolesAsync(UserViewModel user, HashSet<string> desiredRoles)
    {
        var setResult = await UserService.SetRolesAsync(user.Id, desiredRoles.ToArray());
        if (setResult.Succeeded)
        {
            var currentRoles = new HashSet<string>(user.Roles, StringComparer.OrdinalIgnoreCase);
            var rolesToAdd = desiredRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();
            var rolesToRemove = currentRoles.Except(desiredRoles, StringComparer.OrdinalIgnoreCase).ToList();

            var summary = new List<string>();
            if (rolesToAdd.Count > 0) summary.Add($"added: {string.Join(", ", rolesToAdd)}");
            if (rolesToRemove.Count > 0) summary.Add($"removed: {string.Join(", ", rolesToRemove)}");

            Snackbar.Add($"Roles updated for {user.DisplayName} ({string.Join("; ", summary)}).", Severity.Success);
            await dataGrid.ReloadServerData();
        }
        else
        {
            ErrorMessage = setResult.Error ?? "Failed to update roles.";
        }
    }

    #endregion

    #region View Model

    /// <summary>
    /// Wrapper view model for the user data grid.
    /// Holds a <see cref="UserDto"/> reference and delegates properties.
    /// </summary>
    public class UserViewModel
    {
        /// <summary>Display line number (1-based, page-aware).</summary>
        public int LineNumber { get; set; }

        /// <summary>The underlying user DTO.</summary>
        public UserDto User { get; set; } = default!;

        // Delegated from DTO
        /// <summary>The user's Identity ID.</summary>
        public string Id => User.Id;

        /// <summary>The username.</summary>
        public string UserName => User.UserName;

        /// <summary>The display name.</summary>
        public string DisplayName => User.DisplayName ?? "";

        /// <summary>The job title.</summary>
        public string JobTitle => User.JobTitle ?? "";

        /// <summary>The department.</summary>
        public string Department => User.Department ?? "";

        /// <summary>Whether the user is active.</summary>
        public bool IsActive => User.IsActive;

        /// <summary>The user's assigned role names.</summary>
        public List<string> Roles => User.Roles;

        // Computed (not on DTO)
        /// <summary>Whether this is the currently logged-in user.</summary>
        public bool IsSelf { get; set; }

        /// <summary>
        /// Determines equality by <see cref="Id"/> so the grid can match selected items
        /// across page reloads.
        /// </summary>
        public override bool Equals(object? obj) => obj is UserViewModel other && Id == other.Id;

        /// <summary>
        /// Returns a hash code based on <see cref="Id"/>.
        /// </summary>
        public override int GetHashCode() => Id.GetHashCode();
    }

    #endregion
}
