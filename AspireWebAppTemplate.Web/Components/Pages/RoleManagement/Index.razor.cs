using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using BlazorWebAppTemplate.UI.Components.Shared;
using BlazorWebAppTemplate.UI.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace BlazorWebAppTemplate.Components.Pages.RoleManagement;

/// <summary>
/// Role management page. Lists all roles with multi-role management,
/// add/edit/delete actions, and bulk operations. Admin role required.
/// Uses server-side filtering, sorting, and pagination via <see cref="DataGridUtils{T}"/>.
/// </summary>
public partial class Index : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages roles.
    /// </summary>
    [Inject] private RoleManager<ApplicationRole> RoleManager { get; set; } = default!;

    /// <summary>
    /// Manages user accounts for counting users per role.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Structured logger.
    /// </summary>
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    #endregion

    #region Server-Side Data Grid

    /// <summary>
    /// Reference to the MudDataGrid component for triggering server-side reloads.
    /// </summary>
    private MudDataGrid<RoleViewModel> dataGrid = null!;

    /// <summary>
    /// Server-side helper that applies column filters, multi-sort, global search,
    /// and pagination based on <see cref="GridState{T}"/>.
    /// Maps each filterable/sortable column to its corresponding property selector.
    /// </summary>
    private readonly DataGridUtils<RoleViewModel> _dataGridUtils = new DataGridUtils<RoleViewModel>()
        .MapString(nameof(RoleViewModel.Name),          x => x.Name)
        .MapString(nameof(RoleViewModel.DisplayName),   x => x.DisplayName)
        .MapString(nameof(RoleViewModel.Description),   x => x.Description)
        .MapBool(nameof(RoleViewModel.IsActive),        x => x.IsActive)
        .MapInt(nameof(RoleViewModel.UserCount),        x => x.UserCount)
        .MapInt(nameof(RoleViewModel.Position),         x => x.Position);

    #endregion

    #region Bulk Actions State

    /// <summary>
    /// The set of currently selected roles in the data grid.
    /// Bound via <c>@bind-SelectedItems</c>.
    /// </summary>
    private HashSet<RoleViewModel> selectedRoles = new();

    #endregion

    #region State

    /// <summary>
    /// Whether data is currently loading.
    /// </summary>
    protected bool IsLoading { get; private set; } = true;

    /// <summary>
    /// Error message displayed in the alert.
    /// </summary>
    protected string? ErrorMessage { get; private set; }

    /// <summary>
    /// Success message displayed in the alert.
    /// </summary>
    protected string? SuccessMessage { get; private set; }

    /// <summary>
    /// The current global search term for the toolbar search box.
    /// Applied across all searchable fields via <see cref="DataGridUtils{T}"/>.
    /// </summary>
    private string? searchString;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the page. Role data is populated via
    /// <see cref="ServerReload"/> (server-side data grid callback).
    /// </summary>
    protected override Task OnInitializedAsync()
    {
        IsLoading = false;
        return Task.CompletedTask;
    }

    #endregion

    #region Server-Side Data Loading

    /// <summary>
    /// Server-side reload callback for <see cref="MudDataGrid{T}"/>.
    /// Loads all roles, then delegates filtering, sorting, and pagination
    /// to <see cref="DataGridUtils{T}.ServerReloadAsync"/>.
    /// </summary>
    /// <param name="state">The current grid state containing page, page size, filters, and sort definitions.</param>
    /// <param name="cancellationToken">Cancellation token provided by the grid (unused but required by delegate signature).</param>
    /// <returns>A <see cref="GridData{T}"/> containing the paged items and total count.</returns>
    private async Task<GridData<RoleViewModel>> ServerReload(GridState<RoleViewModel> state, CancellationToken cancellationToken)
    {
        // Loader function: fetches all roles and maps to view models
        async Task<IEnumerable<RoleViewModel>> loader() => await LoadRoleViewModelsAsync();

        // Global search fields — searches across all user-visible text columns
        IEnumerable<string> GlobalFields(RoleViewModel r) => new[]
        {
            r.Name,
            r.DisplayName,
            r.Description ?? "",
            r.UserCount.ToString(),
            r.IsActive ? "Active" : "Inactive"
        };

        // Set page-aware line numbers (e.g., page 2 with size 10 starts at 11)
        void SetLine(RoleViewModel item, int lineNo) => item.LineNumber = lineNo;

        return await _dataGridUtils.ServerReloadAsync(
            state,
            loader,
            globalSearchTerm: searchString,
            globalSearchFieldSelector: GlobalFields,
            setLineNumber: SetLine);
    }

    /// <summary>
    /// Loads all roles from the Identity store and maps them to <see cref="RoleViewModel"/> instances.
    /// This serves as the data loader for <see cref="DataGridUtils{T}.ServerReloadAsync"/>.
    /// </summary>
    /// <returns>An enumerable of <see cref="RoleViewModel"/> representing all roles.</returns>
    private async Task<IEnumerable<RoleViewModel>> LoadRoleViewModelsAsync()
    {
        var appRoles = RoleManager.Roles.OrderBy(r => r.Name).ToList();
        var viewModels = new List<RoleViewModel>();

        foreach (var role in appRoles)
        {
            // Fetch users in role to compute the count
            var usersInRole = await UserManager.GetUsersInRoleAsync(role.Name!);
            viewModels.Add(new RoleViewModel
            {
                Id = role.Id,
                Name = role.Name ?? "",
                DisplayName = role.DisplayName ?? "",
                Description = role.Description ?? "",
                IsActive = role.IsActive,
                UserCount = usersInRole.Count,
                CreatedUtc = role.CreatedUtc,
                UpdatedUtc = role.UpdatedUtc,
                IsSystem = role.IsSystem,
                Position = role.Position,
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
        selectedRoles = new HashSet<RoleViewModel>();
        await dataGrid.ReloadServerData();
    }

    /// <summary>
    /// Clears the current selection without reloading the grid.
    /// Used by the "Clear selection" (X) button in the toolbar.
    /// </summary>
    private void ClearSelection()
    {
        selectedRoles = new HashSet<RoleViewModel>();
    }

    #endregion

    #region Bulk Actions

    /// <summary>
    /// Activates all selected roles.
    /// Shows a confirmation dialog before executing.
    /// Sets <c>IsActive = true</c> and stamps <c>UpdatedUtc</c> for each role.
    /// </summary>
    private async Task BulkActivateAsync()
    {
        if (selectedRoles.Count == 0) return;

        // Filter out system roles — they cannot be activated/deactivated
        var targets = selectedRoles.Where(r => !r.IsSystem).ToList();
        var skippedSystem = selectedRoles.Count - targets.Count;

        if (targets.Count == 0)
        {
            Snackbar.Add("No eligible roles in selection — system roles cannot be modified.", Severity.Info);
            return;
        }

        // Confirmation dialog
        var parameters = new DialogParameters<ConfirmationDialog>
        {
            { x => x.ContentText, $"Are you sure you want to activate {targets.Count} role(s)?{(skippedSystem > 0 ? $" ({skippedSystem} skipped: system role)" : "")}" },
            { x => x.SubmitBtnText, "Activate" },
            { x => x.DialogIcon, Icons.Material.Rounded.Warning },
            { x => x.DialogIconColor, Color.Warning }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Activate Roles", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        int success = 0, failed = 0;
        foreach (var roleVm in targets)
        {
            try
            {
                var role = await RoleManager.FindByIdAsync(roleVm.Id);
                if (role is null) { failed++; continue; }

                role.IsActive = true;
                role.UpdatedUtc = DateTime.UtcNow;
                var updateResult = await RoleManager.UpdateAsync(role);
                if (updateResult.Succeeded) success++;
                else
                {
                    failed++;
                    Logger.LogWarning("Failed to activate role '{Role}': {Errors}",
                        role.Name, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                }
            }
            catch (Exception ex)
            {
                failed++;
                Logger.LogError(ex, "Unexpected error activating role '{RoleId}'.", roleVm.Id);
            }
        }

        Snackbar.Add($"{success} activated, {skippedSystem} skipped (system), {failed} failed.", Severity.Success);
        await ClearSelectionAndReloadAsync();
    }

    /// <summary>
    /// Deactivates all selected roles (excluding system roles).
    /// Shows a confirmation dialog before executing.
    /// Sets <c>IsActive = false</c> and stamps <c>UpdatedUtc</c> for each role.
    /// </summary>
    private async Task BulkDeactivateAsync()
    {
        if (selectedRoles.Count == 0) return;

        // Filter out system roles — they cannot be activated/deactivated
        var targets = selectedRoles.Where(r => !r.IsSystem).ToList();
        var skippedSystem = selectedRoles.Count - targets.Count;

        if (targets.Count == 0)
        {
            Snackbar.Add("No eligible roles in selection — system roles cannot be modified.", Severity.Info);
            return;
        }

        // Confirmation dialog
        var parameters = new DialogParameters<ConfirmationDialog>
        {
            { x => x.ContentText, $"Are you sure you want to deactivate {targets.Count} role(s)?{(skippedSystem > 0 ? $" ({skippedSystem} skipped: system role)" : "")}" },
            { x => x.SubmitBtnText, "Deactivate" },
            { x => x.DialogIcon, Icons.Material.Rounded.Warning },
            { x => x.DialogIconColor, Color.Warning }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Deactivate Roles", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        int success = 0, failed = 0;
        foreach (var roleVm in targets)
        {
            try
            {
                var role = await RoleManager.FindByIdAsync(roleVm.Id);
                if (role is null) { failed++; continue; }

                role.IsActive = false;
                role.UpdatedUtc = DateTime.UtcNow;
                var updateResult = await RoleManager.UpdateAsync(role);
                if (updateResult.Succeeded) success++;
                else
                {
                    failed++;
                    Logger.LogWarning("Failed to deactivate role '{Role}': {Errors}",
                        role.Name, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                }
            }
            catch (Exception ex)
            {
                failed++;
                Logger.LogError(ex, "Unexpected error deactivating role '{RoleId}'.", roleVm.Id);
            }
        }

        Snackbar.Add($"{success} deactivated, {skippedSystem} skipped (system), {failed} failed.", Severity.Success);
        await ClearSelectionAndReloadAsync();
    }

    /// <summary>
    /// Deletes all selected roles with a confirmation dialog.
    /// Skips roles that still have users assigned.
    /// </summary>
    private async Task BulkDeleteAsync()
    {
        if (selectedRoles.Count == 0) return;

        // Filter out system roles and roles that have users assigned
        var systemRoles = selectedRoles.Where(r => r.IsSystem).ToList();
        var skippedSystem = systemRoles.Count;

        var eligible = selectedRoles.Where(r => !r.IsSystem).ToList();
        var targets = eligible.Where(r => r.UserCount == 0).ToList();
        var skippedWithUsers = eligible.Count - targets.Count;

        if (targets.Count == 0)
        {
            var reasons = new List<string>();
            if (skippedSystem > 0) reasons.Add($"{skippedSystem} skipped (system role)");
            if (skippedWithUsers > 0) reasons.Add($"{skippedWithUsers} skipped (has users assigned)");
            Snackbar.Add($"No eligible roles in selection — {string.Join(", ", reasons)}.", Severity.Warning);
            return;
        }

        var parameters = new DialogParameters<ConfirmationDialog>
        {
            { x => x.ContentText, $"Are you sure you want to delete {targets.Count} role(s)? This action cannot be undone.{(skippedWithUsers > 0 || skippedSystem > 0 ? $" ({(skippedSystem > 0 ? $"{skippedSystem} skipped: system role" : "")}{(skippedSystem > 0 && skippedWithUsers > 0 ? ", " : "")}{(skippedWithUsers > 0 ? $"{skippedWithUsers} skipped: has users assigned" : "")})" : "")}" },
            { x => x.SubmitBtnText, "Delete" },
            { x => x.DialogIcon, Icons.Material.Rounded.DeleteForever },
            { x => x.DialogIconColor, Color.Error }
        };

        var options = new DialogOptions
        {
            CloseButton = true,
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Delete Roles", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        int success = 0, failed = 0;

        foreach (var roleVm in targets)
        {
            try
            {
                var role = await RoleManager.FindByIdAsync(roleVm.Id);
                if (role is null) { failed++; continue; }

                var deleteResult = await RoleManager.DeleteAsync(role);
                if (deleteResult.Succeeded) success++;
                else
                {
                    failed++;
                    Logger.LogWarning("Failed to delete role '{Role}': {Errors}",
                        role.Name, string.Join(", ", deleteResult.Errors.Select(e => e.Description)));
                }
            }
            catch (Exception ex)
            {
                failed++;
                Logger.LogError(ex, "Unexpected error deleting role '{RoleId}'.", roleVm.Id);
            }
        }

        var parts = new List<string> { $"{success} deleted" };
        if (skippedSystem > 0) parts.Add($"{skippedSystem} skipped (system role)");
        if (skippedWithUsers > 0) parts.Add($"{skippedWithUsers} skipped (has users)");
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
    /// Opens the Add Role dialog.
    /// </summary>
    protected async Task OpenAddRoleDialog()
    {
        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<AddRoleDialog>("Add Role", options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            // Reload the grid to reflect the newly added role
            await dataGrid.ReloadServerData();
            Snackbar.Add("Role created successfully.", Severity.Success);
        }
    }

    /// <summary>
    /// Opens the Edit Role dialog for a specific role.
    /// On success, reloads the grid.
    /// </summary>
    /// <param name="role">The role view model to edit.</param>
    protected async Task OpenEditRoleDialog(RoleViewModel role)
    {
        var parameters = new DialogParameters<EditRoleDialog>
        {
            { x => x.RoleId, role.Id },
            { x => x.IsSystem, role.IsSystem }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<EditRoleDialog>("Edit Role", parameters, options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            // Reload the grid to reflect the updated role data
            await dataGrid.ReloadServerData();
            Snackbar.Add("Role updated successfully.", Severity.Success);
        }
    }

    /// <summary>
    /// Toggles a role's active status with a confirmation dialog.
    /// Deactivation is allowed regardless of user count — a warning is shown if users are assigned.
    /// System roles cannot be deactivated.
    /// </summary>
    /// <param name="role">The role view model to activate or deactivate.</param>
    protected async Task ToggleActivationAsync(RoleViewModel role)
    {
        // Guard: system roles cannot be deactivated
        if (role.IsSystem && role.IsActive)
        {
            Snackbar.Add("Cannot deactivate a system role.", Severity.Error);
            return;
        }

        var action = role.IsActive ? "deactivate" : "activate";

        // Build confirmation content — include warning when deactivating a role with assigned users
        var contentText = $"Are you sure you want to {action} the role '{role.Name}'?";
        if (role.IsActive && role.UserCount > 0)
        {
            contentText += $"\n\nThis role has {role.UserCount} user(s) assigned. Deactivating it will not remove their assignment but the role will no longer be active.";
        }

        var parameters = new DialogParameters<ConfirmationDialog>
        {
            { x => x.ContentText, contentText },
            { x => x.SubmitBtnText, role.IsActive ? "Deactivate" : "Activate" },
            { x => x.DialogIcon, Icons.Material.Rounded.Warning },
            { x => x.DialogIconColor, Color.Warning }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.ExtraSmall, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ConfirmationDialog>(
            $"{(role.IsActive ? "Deactivate" : "Activate")} Role", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        var appRole = await RoleManager.FindByIdAsync(role.Id);
        if (appRole is null)
        {
            ErrorMessage = $"Role '{role.Name}' not found.";
            return;
        }

        appRole.IsActive = !appRole.IsActive;
        appRole.UpdatedUtc = DateTime.UtcNow;
        var updateResult = await RoleManager.UpdateAsync(appRole);

        if (updateResult.Succeeded)
        {
            Snackbar.Add($"Role '{role.Name}' {(appRole.IsActive ? "activated" : "deactivated")} successfully.", Severity.Success);

            // Reload the grid to reflect the updated activation status
            await dataGrid.ReloadServerData();
        }
        else
        {
            ErrorMessage = "Failed to update role status: " +
                           string.Join(", ", updateResult.Errors.Select(e => e.Description));
        }
    }

    /// <summary>
    /// Deletes a single role with a confirmation dialog.
    /// Prevents deletion if users are still assigned to the role.
    /// </summary>
    /// <param name="role">The role view model to delete.</param>
    protected async Task DeleteRoleAsync(RoleViewModel role)
    {
        // Guard: prevent deletion of system roles
        if (role.IsSystem)
        {
            Snackbar.Add(
                $"Cannot delete '{role.Name}' — it is a protected system role.",
                Severity.Error);
            return;
        }

        // Guard: prevent deletion if users are still assigned
        if (role.UserCount > 0)
        {
            Snackbar.Add(
                $"Cannot delete '{role.Name}' — {role.UserCount} user(s) are still assigned to this role.",
                Severity.Warning);
            return;
        }

        var parameters = new DialogParameters<ConfirmationDialog>
        {
            { x => x.ContentText, $"Are you sure you want to delete the role '{role.Name}'? This action cannot be undone." },
            { x => x.SubmitBtnText, "Delete" },
            { x => x.DialogIcon, Icons.Material.Rounded.DeleteForever },
            { x => x.DialogIconColor, Color.Error }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Delete Role", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        try
        {
            var appRole = await RoleManager.FindByIdAsync(role.Id);
            if (appRole is null)
            {
                ErrorMessage = $"Role '{role.Name}' not found.";
                return;
            }

            var deleteResult = await RoleManager.DeleteAsync(appRole);
            if (deleteResult.Succeeded)
            {
                Snackbar.Add($"Role '{role.Name}' deleted successfully.", Severity.Success);
                await dataGrid.ReloadServerData();
            }
            else
            {
                ErrorMessage = "Failed to delete role: " +
                               string.Join(", ", deleteResult.Errors.Select(e => e.Description));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting role '{RoleId}'.", role.Id);
            ErrorMessage = "An unexpected error occurred while deleting the role.";
        }
    }

    #endregion

    #region View Models

    /// <summary>
    /// Flattened view model for the role data grid.
    /// Properties are mapped to <see cref="DataGridUtils{T}"/> for server-side
    /// filtering and sorting support.
    /// </summary>
    public class RoleViewModel
    {
        /// <summary>Display line number (1-based, page-aware). Set by <see cref="DataGridUtils{T}"/>.</summary>
        public int LineNumber { get; set; }

        /// <summary>The role's Identity ID.</summary>
        public string Id { get; set; } = "";

        /// <summary>The technical role name used by Identity (e.g., "Admin").</summary>
        public string Name { get; set; } = "";

        /// <summary>The human-readable display name (e.g., "Administrator").</summary>
        public string DisplayName { get; set; } = "";

        /// <summary>The role description.</summary>
        public string? Description { get; set; }

        /// <summary>Whether the role is active.</summary>
        public bool IsActive { get; set; }

        /// <summary>Whether the role is a protected system role (cannot be deleted/deactivated/renamed).</summary>
        public bool IsSystem { get; set; }

        /// <summary>The role's position value indicating authority level (higher = more authority).</summary>
        public int Position { get; set; }

        /// <summary>Number of users currently assigned to this role.</summary>
        public int UserCount { get; set; }

        /// <summary>When the role was created (UTC).</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>When the role was last updated (UTC). Null if never updated.</summary>
        public DateTime? UpdatedUtc { get; set; }

        /// <summary>
        /// Determines equality by <see cref="Id"/> so the grid can match selected items
        /// across page reloads (where new object instances are created by ServerData).
        /// </summary>
        public override bool Equals(object? obj)
            => obj is RoleViewModel other && Id == other.Id;

        /// <summary>
        /// Returns a hash code based on <see cref="Id"/>.
        /// </summary>
        public override int GetHashCode()
            => Id.GetHashCode();
    }

    #endregion
}
