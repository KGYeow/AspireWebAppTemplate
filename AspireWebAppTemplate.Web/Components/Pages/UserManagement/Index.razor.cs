using BlazorWebAppTemplate.Abstractions;
using BlazorWebAppTemplate.Core.Domain.Enums;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using BlazorWebAppTemplate.Options;
using BlazorWebAppTemplate.UI.Components.Shared;
using BlazorWebAppTemplate.UI.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MudBlazor;

namespace BlazorWebAppTemplate.Components.Pages.UserManagement;

/// <summary>
/// User management page. Lists all users with multi-role management,
/// activation toggle, delete actions, and bulk operations. Admin role required.
/// Uses server-side filtering, sorting, and pagination via <see cref="DataGridUtils{T}"/>.
/// </summary>
public partial class Index : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages user accounts.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Manages roles.
    /// </summary>
    [Inject] private RoleManager<ApplicationRole> RoleManager { get; set; } = default!;

    /// <summary>
    /// Structured logger.
    /// </summary>
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    // [LDAP] LDAP settings to check if LDAP is enabled — remove if LDAP is not needed
    /// <summary>
    /// LDAP configuration options.
    /// </summary>
    [Inject] private IOptions<LdapSettings> LdapOptions { get; set; } = default!;

    // [LDAP] LDAP auth service for sync operations — remove if LDAP is not needed
    /// <summary>
    /// LDAP authentication service for fetching user attributes during sync.
    /// </summary>
    [Inject] private ILdapAuthService LdapAuthService { get; set; } = default!;

    /// <summary>
    /// Audit log service for recording user management actions.
    /// </summary>
    [Inject] private IAuditLogService AuditLogService { get; set; } = default!;

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
    /// The current logged-in user's Identity ID, used for audit log entries.
    /// </summary>
    private string? currentUserId;

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
    /// </summary>
    protected bool IsLdapEnabled => LdapOptions.Value.Enabled;

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
    /// Cancellation token source for the current sync operation.
    /// </summary>
    private CancellationTokenSource? syncCts;

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

            var allRoles = RoleManager.Roles.ToList();

            allRoleNames = allRoles
                .Select(r => r.Name!)
                .OrderBy(n => n)
                .ToList();

            // Compute actor's highest role position for authority checks
            if (currentUserName is not null)
            {
                var currentUser = await UserManager.FindByNameAsync(currentUserName);
                if (currentUser is not null)
                {
                    currentUserId = currentUser.Id;
                    var actorRoleNames = await UserManager.GetRolesAsync(currentUser);
                    actorHighestPosition = actorRoleNames
                        .Select(name => allRoles.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))?.Position ?? 0)
                        .DefaultIfEmpty(0)
                        .Max();
                }
            }

            // Filter assignable roles: only those with Position <= actor's highest position
            assignableRoleNames = allRoles
                .Where(r => r.Position <= actorHighestPosition)
                .Select(r => r.Name!)
                .OrderBy(n => n)
                .ToList();
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
    /// Loads all users, then delegates filtering, sorting, and pagination
    /// to <see cref="DataGridUtils{T}.ServerReloadAsync"/>.
    /// </summary>
    /// <param name="state">The current grid state containing page, page size, filters, and sort definitions.</param>
    /// <param name="cancellationToken">Cancellation token provided by the grid (unused but required by delegate signature).</param>
    /// <returns>A <see cref="GridData{T}"/> containing the paged items and total count.</returns>
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
    /// Loads all users from the Identity store and maps them to <see cref="UserViewModel"/> instances.
    /// This serves as the data loader for <see cref="DataGridUtils{T}.ServerReloadAsync"/>.
    /// </summary>
    /// <returns>An enumerable of <see cref="UserViewModel"/> representing all users.</returns>
    private async Task<IEnumerable<UserViewModel>> LoadUserViewModelsAsync()
    {
        var appUsers = UserManager.Users.OrderBy(u => u.UserName).ToList();
        var viewModels = new List<UserViewModel>();

        foreach (var user in appUsers)
        {
            var roles = await UserManager.GetRolesAsync(user);
            viewModels.Add(new UserViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? "",
                DisplayName = user.DisplayName ?? "",
                JobTitle = user.JobTitle ?? "",
                Email = user.Email ?? "",
                Department = user.Department ?? "",
                IsActive = user.IsActive,
                Roles = roles.OrderBy(r => r).ToList(),
                IsSelf = string.Equals(user.UserName, currentUserName, StringComparison.OrdinalIgnoreCase)
            });
        }

        return viewModels;
    }

    #endregion

    #region Bulk Actions Helpers

    /// <summary>
    /// Clears the current selection and reloads the grid.
    /// Called after any bulk action completes.
    /// </summary>
    private async Task ClearSelectionAndReloadAsync()
    {
        // Assign a new instance to trigger binding update in the grid
        selectedUsers = new HashSet<UserViewModel>();
        await dataGrid.ReloadServerData();
    }

    /// <summary>
    /// Clears the current selection without reloading the grid.
    /// Used by the "Clear selection" (X) button in the toolbar.
    /// </summary>
    private void ClearSelection()
    {
        selectedUsers = new HashSet<UserViewModel>();
    }

    #endregion

    #region Bulk Actions

    /// <summary>
    /// Activates all selected users (excluding self).
    /// Shows a confirmation dialog before executing.
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

        // Confirmation dialog
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
            var appUser = await UserManager.FindByIdAsync(user.Id);
            if (appUser is null) { failed++; continue; }

            appUser.IsActive = true;
            var updateResult = await UserManager.UpdateAsync(appUser);
            if (updateResult.Succeeded)
            {
                success++;

                // Audit: log user activation event (fire-and-forget safe — failures won't interrupt)
                try
                {
                    await AuditLogService.LogAsync(
                        currentUserId,
                        AuditActionType.UserActivated,
                        AuditEntityType.User,
                        appUser.Id,
                        appUser.DisplayName ?? appUser.UserName ?? "",
                        $"User '{appUser.DisplayName ?? appUser.UserName}' activated via bulk action.");
                }
                catch { /* audit failures must not interrupt the primary operation */ }
            }
            else failed++;
        }

        Snackbar.Add($"{success} activated, {skipped} skipped (self), {failed} failed.", Severity.Success);
        await ClearSelectionAndReloadAsync();
    }

    /// <summary>
    /// Deactivates all selected users (excluding self).
    /// Shows a confirmation dialog before executing.
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

        // Confirmation dialog
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

        int success = 0, failed = 0, guardSkipped = 0, positionSkipped = 0;
        foreach (var user in targets)
        {
            var appUser = await UserManager.FindByIdAsync(user.Id);
            if (appUser is null) { failed++; continue; }

            // Guard: skip if target user has a higher role position than actor
            var targetPosition = await GetHighestPositionForUserAsync(appUser);
            if (actorHighestPosition < targetPosition)
            {
                positionSkipped++;
                continue;
            }

            // Guard: skip if user is the last one in a RequiresMinimumUser role
            var blockingRole = await GetBlockingRequiresMinimumRoleAsync(appUser);
            if (blockingRole is not null)
            {
                guardSkipped++;
                continue;
            }

            appUser.IsActive = false;
            var updateResult = await UserManager.UpdateAsync(appUser);
            if (updateResult.Succeeded)
            {
                success++;

                // Audit: log user deactivation event (fire-and-forget safe — failures won't interrupt)
                try
                {
                    await AuditLogService.LogAsync(
                        currentUserId,
                        AuditActionType.UserDeactivated,
                        AuditEntityType.User,
                        appUser.Id,
                        appUser.DisplayName ?? appUser.UserName ?? "",
                        $"User '{appUser.DisplayName ?? appUser.UserName}' deactivated via bulk action.");
                }
                catch { /* audit failures must not interrupt the primary operation */ }
            }
            else failed++;
        }

        var message = $"{success} deactivated, {skipped} skipped (self)";
        if (positionSkipped > 0) message += $", {positionSkipped} skipped (higher position)";
        if (guardSkipped > 0) message += $", {guardSkipped} skipped (last user in required role)";
        message += $", {failed} failed.";
        Snackbar.Add(message, Severity.Success);
        await ClearSelectionAndReloadAsync();
    }

    /// <summary>
    /// Deletes all selected users (excluding self).
    /// Shows a confirmation dialog before executing.
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

        // Confirmation dialog
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

        int success = 0, failed = 0, guardSkipped = 0, positionSkipped = 0;
        foreach (var user in targets)
        {
            var appUser = await UserManager.FindByIdAsync(user.Id);
            if (appUser is null) { failed++; continue; }

            // Guard: skip if target user has a higher role position than actor
            var targetPosition = await GetHighestPositionForUserAsync(appUser);
            if (actorHighestPosition < targetPosition)
            {
                positionSkipped++;
                continue;
            }

            // Guard: skip if user is the last one in a RequiresMinimumUser role
            var blockingRole = await GetBlockingRequiresMinimumRoleAsync(appUser);
            if (blockingRole is not null)
            {
                guardSkipped++;
                continue;
            }

            // Capture display name before deletion for audit trail
            var deletedDisplayName = appUser.DisplayName ?? appUser.UserName ?? "";
            var deletedUserId = appUser.Id;

            var deleteResult = await UserManager.DeleteAsync(appUser);
            if (deleteResult.Succeeded)
            {
                success++;

                // Audit: log user deletion event (fire-and-forget safe — failures won't interrupt)
                try
                {
                    await AuditLogService.LogAsync(
                        currentUserId,
                        AuditActionType.UserDeleted,
                        AuditEntityType.User,
                        deletedUserId,
                        deletedDisplayName,
                        $"User '{deletedDisplayName}' deleted via bulk action.");
                }
                catch { /* audit failures must not interrupt the primary operation */ }
            }
            else failed++;
        }

        var message = $"{success} deleted, {skipped} skipped (self)";
        if (positionSkipped > 0) message += $", {positionSkipped} skipped (higher position)";
        if (guardSkipped > 0) message += $", {guardSkipped} skipped (last user in required role)";
        message += $", {failed} failed.";
        Snackbar.Add(message, Severity.Success);
        await ClearSelectionAndReloadAsync();
    }

    /// <summary>
    /// Opens a dialog to select a role, then assigns it to all selected users (excluding self).
    /// Supports two modes: Add (keeps existing roles) or Replace (removes existing roles first).
    /// Logs RoleAssigned/RoleUnassigned audit entries for each successful operation.
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

        // Show the bulk assign role dialog
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
            var appUser = await UserManager.FindByIdAsync(user.Id);
            if (appUser is null) { failed++; continue; }

            var currentRoles = await UserManager.GetRolesAsync(appUser);

            if (replaceExisting)
            {
                // Replace mode: remove all existing roles, then assign the selected one
                var removedRoles = currentRoles.ToList();
                if (currentRoles.Count > 0)
                {
                    var removeResult = await UserManager.RemoveFromRolesAsync(appUser, currentRoles);
                    if (!removeResult.Succeeded) { failed++; continue; }
                }

                var addResult = await UserManager.AddToRoleAsync(appUser, selectedRole);
                if (addResult.Succeeded)
                {
                    success++;

                    // Audit: log each removed role as RoleUnassigned and the new role as RoleAssigned
                    try
                    {
                        foreach (var removedRole in removedRoles)
                        {
                            await AuditLogService.LogAsync(
                                currentUserId,
                                AuditActionType.RoleUnassigned,
                                AuditEntityType.Role,
                                entityId: user.Id,
                                entityName: removedRole,
                                description: $"Role '{removedRole}' removed from user '{user.DisplayName ?? user.UserName}' (bulk replace).");
                        }

                        await AuditLogService.LogAsync(
                            currentUserId,
                            AuditActionType.RoleAssigned,
                            AuditEntityType.Role,
                            entityId: user.Id,
                            entityName: selectedRole,
                            description: $"Role '{selectedRole}' assigned to user '{user.DisplayName ?? user.UserName}' (bulk replace).");
                    }
                    catch
                    {
                        // Audit failures must not interrupt the primary operation
                    }
                }
                else failed++;
            }
            else
            {
                // Add mode: only add the role if the user doesn't already have it
                if (currentRoles.Contains(selectedRole, StringComparer.OrdinalIgnoreCase))
                {
                    alreadyHasRole++;
                    continue;
                }

                var addResult = await UserManager.AddToRoleAsync(appUser, selectedRole);
                if (addResult.Succeeded)
                {
                    success++;

                    // Audit: log role assignment
                    try
                    {
                        await AuditLogService.LogAsync(
                            currentUserId,
                            AuditActionType.RoleAssigned,
                            AuditEntityType.Role,
                            entityId: user.Id,
                            entityName: selectedRole,
                            description: $"Role '{selectedRole}' assigned to user '{user.DisplayName ?? user.UserName}' (bulk assign).");
                    }
                    catch
                    {
                        // Audit failures must not interrupt the primary operation
                    }
                }
                else failed++;
            }
        }

        // Build summary message
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
    /// Triggers a server-side reload with the new search term.
    /// </summary>
    /// <param name="text">The search text entered by the user.</param>
    /// <returns>A task representing the asynchronous reload operation.</returns>
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
        var parameters = new DialogParameters<AddUserDialog>
        {
            { x => x.AllRoleNames, allRoleNames }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<AddUserDialog>("Add User", parameters, options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            // Reload the grid to reflect the newly added user
            await dataGrid.ReloadServerData();
            Snackbar.Add("User added successfully.", Severity.Success);
        }
    }

    /// <summary>
    /// Opens the Edit User dialog for a user.
    /// </summary>
    /// <param name="user">The user view model to edit.</param>
    protected async Task OpenEditUserDialog(UserViewModel user)
    {
        // Position-based authority check
        var targetUser = await UserManager.FindByIdAsync(user.Id);
        if (targetUser is not null)
        {
            var targetPosition = await GetHighestPositionForUserAsync(targetUser);
            if (actorHighestPosition < targetPosition)
            {
                Snackbar.Add("You cannot modify a user with a higher role position.", Severity.Error);
                return;
            }
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
            // Reload the grid to reflect the updated user data
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
            // Reload the grid to reflect the newly provisioned LDAP user
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

    // [LDAP] Cancels the current sync operation — remove if LDAP is not needed
    /// <summary>
    /// Cancels the current LDAP sync operation.
    /// </summary>
    protected void CancelSync()
    {
        syncCts?.Cancel();
    }

    // [LDAP] Syncs all local users from LDAP — remove if LDAP is not needed
    /// <summary>
    /// Iterates all LDAP-sourced users, fetches their latest attributes,
    /// and updates changed fields. Reports progress via UI state.
    /// </summary>
    private async Task SyncAllUsersFromLdapAsync()
    {
        if (IsSyncing) return;

        IsSyncing = true;
        SyncedCount = 0;
        TotalToSync = 0;
        SyncMessage = null;
        ErrorMessage = null;
        syncCts = new CancellationTokenSource();

        int updated = 0;
        int failed = 0;

        try
        {
            // Snapshot only LDAP-sourced users
            var allUsers = UserManager.Users
                .Where(u => u.AuthSource == AuthSource.LDAP)
                .OrderBy(u => u.UserName)
                .ToList();
            TotalToSync = allUsers.Count;

            foreach (var user in allUsers)
            {
                syncCts.Token.ThrowIfCancellationRequested();

                try
                {
                    var attrs = await LdapAuthService.FetchUserAttributesAsync(user.UserName ?? "");
                    if (attrs is null)
                    {
                        failed++;
                    }
                    else
                    {
                        // Check for changes
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
                            var updateResult = await UserManager.UpdateAsync(user);
                            if (updateResult.Succeeded) updated++;
                            else failed++;
                        }
                    }
                }
                catch
                {
                    failed++;
                }

                SyncedCount++;
                await InvokeAsync(StateHasChanged);

                // Small delay to avoid hammering LDAP
                await Task.Delay(10, syncCts.Token);
            }

            SyncMessage = $"Sync completed. Updated {updated} of {TotalToSync} users; {failed} failed.";

            // Reload the grid to reflect synced user data
            await dataGrid.ReloadServerData();
        }
        catch (OperationCanceledException)
        {
            Snackbar.Add("User sync canceled.", Severity.Warning);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Sync error: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
            syncCts?.Dispose();
            syncCts = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    /// Opens the Manage Roles dialog for a single user.
    /// </summary>
    /// <param name="user">The user view model whose roles are being managed.</param>
    protected async Task OpenManageRolesDialog(UserViewModel user)
    {
        // Position-based authority check
        var targetUser = await UserManager.FindByIdAsync(user.Id);
        if (targetUser is not null)
        {
            var targetPosition = await GetHighestPositionForUserAsync(targetUser);
            if (actorHighestPosition < targetPosition)
            {
                Snackbar.Add("You cannot modify a user with a higher role position.", Severity.Error);
                return;
            }
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
    /// <param name="user">The user view model to activate or deactivate.</param>
    protected async Task ToggleActivationAsync(UserViewModel user)
    {
        // Position-based authority check (applies to both activate and deactivate)
        var targetUserForPosition = await UserManager.FindByIdAsync(user.Id);
        if (targetUserForPosition is not null)
        {
            var targetPosition = await GetHighestPositionForUserAsync(targetUserForPosition);
            if (actorHighestPosition < targetPosition)
            {
                Snackbar.Add("You cannot modify a user with a higher role position.", Severity.Error);
                return;
            }
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

        var appUser = await UserManager.FindByIdAsync(user.Id);
        if (appUser is null)
        {
            ErrorMessage = $"User '{user.UserName}' not found.";
            return;
        }

        // Guard: block deactivation if user is the last one in a RequiresMinimumUser role
        if (user.IsActive)
        {
            var blockingRole = await GetBlockingRequiresMinimumRoleAsync(appUser);
            if (blockingRole is not null)
            {
                Snackbar.Add($"Cannot deactivate the last user in role '{blockingRole}'.", Severity.Error);
                return;
            }
        }

        appUser.IsActive = !appUser.IsActive;
        appUser.UpdatedUtc = DateTime.UtcNow;
        var updateResult = await UserManager.UpdateAsync(appUser);

        if (updateResult.Succeeded)
        {
            Snackbar.Add($"User {(appUser.IsActive ? "activated" : "deactivated")} successfully.", Severity.Success);

            // Audit: log activation/deactivation event (fire-and-forget safe — failures won't interrupt)
            try
            {
                var auditAction = appUser.IsActive ? AuditActionType.UserActivated : AuditActionType.UserDeactivated;
                await AuditLogService.LogAsync(
                    currentUserId,
                    auditAction,
                    AuditEntityType.User,
                    appUser.Id,
                    appUser.DisplayName ?? appUser.UserName ?? "",
                    $"User '{appUser.DisplayName ?? appUser.UserName}' {(appUser.IsActive ? "activated" : "deactivated")}.");
            }
            catch { /* audit failures must not interrupt the primary operation */ }

            // Reload the grid to reflect the updated activation status
            await dataGrid.ReloadServerData();
        }
        else
        {
            ErrorMessage = "Failed to update user status: " +
                           string.Join(", ", updateResult.Errors.Select(e => e.Description));
        }
    }

    /// <summary>
    /// Deletes a single user with confirmation.
    /// </summary>
    /// <param name="user">The user view model to delete.</param>
    protected async Task DeleteUserAsync(UserViewModel user)
    {
        // Position-based authority check
        var targetUserForPosition = await UserManager.FindByIdAsync(user.Id);
        if (targetUserForPosition is not null)
        {
            var targetPosition = await GetHighestPositionForUserAsync(targetUserForPosition);
            if (actorHighestPosition < targetPosition)
            {
                Snackbar.Add("You cannot modify a user with a higher role position.", Severity.Error);
                return;
            }
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

        var appUser = await UserManager.FindByIdAsync(user.Id);
        if (appUser is null)
        {
            ErrorMessage = $"User '{user.UserName}' not found.";
            return;
        }

        // Guard: block deletion if user is the last one in a RequiresMinimumUser role
        var blockingRole = await GetBlockingRequiresMinimumRoleAsync(appUser);
        if (blockingRole is not null)
        {
            Snackbar.Add($"Cannot delete the last user in role '{blockingRole}'.", Severity.Error);
            return;
        }

        // Capture display name before deletion for audit trail
        var deletedDisplayName = appUser.DisplayName ?? appUser.UserName ?? "";
        var deletedUserId = appUser.Id;

        var deleteResult = await UserManager.DeleteAsync(appUser);
        if (deleteResult.Succeeded)
        {
            Snackbar.Add($"User '{user.DisplayName} ({user.UserName})' deleted.", Severity.Success);

            // Audit: log user deletion event (fire-and-forget safe — failures won't interrupt)
            try
            {
                await AuditLogService.LogAsync(
                    currentUserId,
                    AuditActionType.UserDeleted,
                    AuditEntityType.User,
                    deletedUserId,
                    deletedDisplayName,
                    $"User '{deletedDisplayName}' deleted.");
            }
            catch { /* audit failures must not interrupt the primary operation */ }

            await dataGrid.ReloadServerData();
        }
        else
        {
            ErrorMessage = "Failed to delete user: " +
                           string.Join(", ", deleteResult.Errors.Select(e => e.Description));
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Gets the highest role position for the given user.
    /// Returns 0 if the user has no roles.
    /// </summary>
    /// <param name="appUser">The application user to check.</param>
    /// <returns>The highest position value among the user's assigned roles.</returns>
    private async Task<int> GetHighestPositionForUserAsync(ApplicationUser appUser)
    {
        var roles = await UserManager.GetRolesAsync(appUser);
        if (roles.Count == 0) return 0;

        return await RoleManager.Roles
            .Where(r => roles.Contains(r.Name!))
            .MaxAsync(r => r.Position);
    }

    /// <summary>
    /// Checks whether the given user is the last user in any role that has
    /// <see cref="ApplicationRole.RequiresMinimumUser"/> set to true.
    /// </summary>
    /// <param name="appUser">The application user to check.</param>
    /// <returns>
    /// The name of the first role where the user is the last member and
    /// <c>RequiresMinimumUser</c> is true, or <c>null</c> if no such role exists.
    /// </returns>
    private async Task<string?> GetBlockingRequiresMinimumRoleAsync(ApplicationUser appUser)
    {
        var userRoles = await UserManager.GetRolesAsync(appUser);

        foreach (var roleName in userRoles)
        {
            var role = await RoleManager.FindByNameAsync(roleName);
            if (role is null || !role.RequiresMinimumUser) continue;

            var usersInRole = await UserManager.GetUsersInRoleAsync(roleName);
            if (usersInRole.Count == 1)
            {
                return roleName;
            }
        }

        return null;
    }

    /// <summary>
    /// Sets the exact roles for a user using diff-based logic:
    /// adds roles that are in <paramref name="desiredRoles"/> but not currently assigned,
    /// and removes roles that are currently assigned but not in <paramref name="desiredRoles"/>.
    /// After successful update, reloads the data grid and logs RoleAssigned/RoleUnassigned audit entries.
    /// </summary>
    /// <param name="user">The user view model to update.</param>
    /// <param name="desiredRoles">The desired set of role names.</param>
    private async Task SetRolesAsync(UserViewModel user, HashSet<string> desiredRoles)
    {
        var appUser = await UserManager.FindByIdAsync(user.Id);
        if (appUser is null)
        {
            ErrorMessage = $"User '{user.UserName}' not found.";
            return;
        }

        var currentRoles = new HashSet<string>(await UserManager.GetRolesAsync(appUser), StringComparer.OrdinalIgnoreCase);

        var rolesToAdd = desiredRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();
        var rolesToRemove = currentRoles.Except(desiredRoles, StringComparer.OrdinalIgnoreCase).ToList();

        // Remove roles no longer selected
        if (rolesToRemove.Count > 0)
        {
            var removeResult = await UserManager.RemoveFromRolesAsync(appUser, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                ErrorMessage = "Error removing roles: " +
                               string.Join(", ", removeResult.Errors.Select(e => e.Description));
                return;
            }
        }

        // Add newly selected roles
        if (rolesToAdd.Count > 0)
        {
            var addResult = await UserManager.AddToRolesAsync(appUser, rolesToAdd);
            if (!addResult.Succeeded)
            {
                // Roll back removals
                if (rolesToRemove.Count > 0)
                    await UserManager.AddToRolesAsync(appUser, rolesToRemove);

                ErrorMessage = "Error adding roles: " +
                               string.Join(", ", addResult.Errors.Select(e => e.Description));
                return;
            }
        }

        // Audit: log each role assignment and unassignment (fire-and-forget safe)
        try
        {
            foreach (var roleName in rolesToAdd)
            {
                await AuditLogService.LogAsync(
                    currentUserId,
                    AuditActionType.RoleAssigned,
                    AuditEntityType.Role,
                    entityId: user.Id,
                    entityName: roleName,
                    description: $"Role '{roleName}' assigned to user '{user.DisplayName ?? user.UserName}'.");
            }

            foreach (var roleName in rolesToRemove)
            {
                await AuditLogService.LogAsync(
                    currentUserId,
                    AuditActionType.RoleUnassigned,
                    AuditEntityType.Role,
                    entityId: user.Id,
                    entityName: roleName,
                    description: $"Role '{roleName}' removed from user '{user.DisplayName ?? user.UserName}'.");
            }
        }
        catch
        {
            // Audit failures must not interrupt the primary operation
        }

        var summary = new List<string>();
        if (rolesToAdd.Count > 0) summary.Add($"added: {string.Join(", ", rolesToAdd)}");
        if (rolesToRemove.Count > 0) summary.Add($"removed: {string.Join(", ", rolesToRemove)}");

        Snackbar.Add($"Roles updated for {user.DisplayName} ({string.Join("; ", summary)}).", Severity.Success);

        // Reload the grid to reflect the updated roles
        await dataGrid.ReloadServerData();
    }

    #endregion

    #region View Model

    /// <summary>
    /// Flattened view model for the user data grid.
    /// Properties are mapped to <see cref="DataGridUtils{T}"/> for server-side
    /// filtering and sorting support.
    /// </summary>
    public class UserViewModel
    {
        /// <summary>Display line number (1-based, page-aware). Set by <see cref="DataGridUtils{T}"/>.</summary>
        public int LineNumber { get; set; }

        /// <summary>The user's Identity ID.</summary>
        public string Id { get; set; } = "";

        /// <summary>The username.</summary>
        public string UserName { get; set; } = "";

        /// <summary>The display name.</summary>
        public string DisplayName { get; set; } = "";

        /// <summary>The job title.</summary>
        public string JobTitle { get; set; } = "";

        /// <summary>The email address.</summary>
        public string Email { get; set; } = "";

        /// <summary>The department.</summary>
        public string Department { get; set; } = "";

        /// <summary>Whether the user is active.</summary>
        public bool IsActive { get; set; }

        /// <summary>The user's assigned role names.</summary>
        public List<string> Roles { get; set; } = [];

        /// <summary>Whether this is the currently logged-in user.</summary>
        public bool IsSelf { get; set; }

        /// <summary>
        /// Determines equality by <see cref="Id"/> so the grid can match selected items
        /// across page reloads (where new object instances are created by ServerData).
        /// </summary>
        public override bool Equals(object? obj)
            => obj is UserViewModel other && Id == other.Id;

        /// <summary>
        /// Returns a hash code based on <see cref="Id"/>.
        /// </summary>
        public override int GetHashCode()
            => Id.GetHashCode();
    }

    #endregion
}
