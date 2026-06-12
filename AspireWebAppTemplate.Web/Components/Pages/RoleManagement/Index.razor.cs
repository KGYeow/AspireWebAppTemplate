using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.UI.Components.Shared;
using AspireWebAppTemplate.UI.Utilities;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.RoleManagement;

/// <summary>
/// Role management page. Lists all roles with multi-role management,
/// add/edit/delete actions, and bulk operations. Admin role required.
/// Uses server-side filtering, sorting, and pagination via <see cref="DataGridUtils{T}"/>.
/// All operations are delegated to the API via <see cref="ApiRoleService"/>.
/// </summary>
public partial class Index : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for role operations.
    /// </summary>
    [Inject] private ApiRoleService RoleService { get; set; } = default!;

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
    /// Loads all roles from the API, then delegates filtering, sorting, and pagination
    /// to <see cref="DataGridUtils{T}.ServerReloadAsync"/>.
    /// </summary>
    private async Task<GridData<RoleViewModel>> ServerReload(GridState<RoleViewModel> state, CancellationToken cancellationToken)
    {
        async Task<IEnumerable<RoleViewModel>> loader() => await LoadRoleViewModelsAsync();

        IEnumerable<string> GlobalFields(RoleViewModel r) => new[]
        {
            r.Name,
            r.DisplayName,
            r.Description ?? "",
            r.UserCount.ToString(),
            r.IsActive ? "Active" : "Inactive"
        };

        void SetLine(RoleViewModel item, int lineNo) => item.LineNumber = lineNo;

        return await _dataGridUtils.ServerReloadAsync(
            state,
            loader,
            globalSearchTerm: searchString,
            globalSearchFieldSelector: GlobalFields,
            setLineNumber: SetLine);
    }

    /// <summary>
    /// Loads all roles from the API and maps them to <see cref="RoleViewModel"/> instances.
    /// </summary>
    private async Task<IEnumerable<RoleViewModel>> LoadRoleViewModelsAsync()
    {
        var result = await RoleService.GetRolesAsync();
        if (!result.Succeeded || result.Data is null) return [];

        return result.Data.Select(r => new RoleViewModel
        {
            Id = r.Id,
            Name = r.Name,
            DisplayName = r.DisplayName ?? "",
            Description = r.Description ?? "",
            IsActive = r.IsActive,
            UserCount = r.UserCount,
            CreatedUtc = r.CreatedUtc,
            UpdatedUtc = r.UpdatedUtc,
            IsSystem = r.IsSystem,
            Position = r.Position,
        });
    }

    #endregion

    #region Bulk Actions Helpers

    /// <summary>
    /// Clears the current selection and reloads the grid.
    /// </summary>
    private async Task ClearSelectionAndReloadAsync()
    {
        selectedRoles = new HashSet<RoleViewModel>();
        await dataGrid.ReloadServerData();
    }

    /// <summary>
    /// Clears the current selection without reloading the grid.
    /// </summary>
    private void ClearSelection()
    {
        selectedRoles = new HashSet<RoleViewModel>();
    }

    #endregion

    #region Bulk Actions

    /// <summary>
    /// Activates all selected roles.
    /// </summary>
    private async Task BulkActivateAsync()
    {
        if (selectedRoles.Count == 0) return;

        var targets = selectedRoles.Where(r => !r.IsSystem).ToList();
        var skippedSystem = selectedRoles.Count - targets.Count;

        if (targets.Count == 0)
        {
            Snackbar.Add("No eligible roles in selection — system roles cannot be modified.", Severity.Info);
            return;
        }

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
            var activateResult = await RoleService.ActivateRoleAsync(roleVm.Id);
            if (activateResult.Succeeded) success++;
            else failed++;
        }

        Snackbar.Add($"{success} activated, {skippedSystem} skipped (system), {failed} failed.", Severity.Success);
        await ClearSelectionAndReloadAsync();
    }

    /// <summary>
    /// Deactivates all selected roles (excluding system roles).
    /// </summary>
    private async Task BulkDeactivateAsync()
    {
        if (selectedRoles.Count == 0) return;

        var targets = selectedRoles.Where(r => !r.IsSystem).ToList();
        var skippedSystem = selectedRoles.Count - targets.Count;

        if (targets.Count == 0)
        {
            Snackbar.Add("No eligible roles in selection — system roles cannot be modified.", Severity.Info);
            return;
        }

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
            var deactivateResult = await RoleService.DeactivateRoleAsync(roleVm.Id);
            if (deactivateResult.Succeeded) success++;
            else failed++;
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
            var deleteResult = await RoleService.DeleteRoleAsync(roleVm.Id);
            if (deleteResult.Succeeded) success++;
            else failed++;
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
    /// </summary>
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
            await dataGrid.ReloadServerData();
            Snackbar.Add("Role created successfully.", Severity.Success);
        }
    }

    /// <summary>
    /// Opens the Edit Role dialog for a specific role.
    /// </summary>
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
            await dataGrid.ReloadServerData();
            Snackbar.Add("Role updated successfully.", Severity.Success);
        }
    }

    /// <summary>
    /// Toggles a role's active status with a confirmation dialog.
    /// </summary>
    protected async Task ToggleActivationAsync(RoleViewModel role)
    {
        if (role.IsSystem && role.IsActive)
        {
            Snackbar.Add("Cannot deactivate a system role.", Severity.Error);
            return;
        }

        var action = role.IsActive ? "deactivate" : "activate";

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

        bool ok;
        if (role.IsActive)
        {
            var deactivateResult = await RoleService.DeactivateRoleAsync(role.Id);
            ok = deactivateResult.Succeeded;
        }
        else
        {
            var activateResult = await RoleService.ActivateRoleAsync(role.Id);
            ok = activateResult.Succeeded;
        }

        if (ok)
        {
            Snackbar.Add($"Role '{role.Name}' {(!role.IsActive ? "activated" : "deactivated")} successfully.", Severity.Success);
            await dataGrid.ReloadServerData();
        }
        else
        {
            ErrorMessage = "Failed to update role status.";
        }
    }

    /// <summary>
    /// Deletes a single role with a confirmation dialog.
    /// </summary>
    protected async Task DeleteRoleAsync(RoleViewModel role)
    {
        if (role.IsSystem)
        {
            Snackbar.Add($"Cannot delete '{role.Name}' — it is a protected system role.", Severity.Error);
            return;
        }

        if (role.UserCount > 0)
        {
            Snackbar.Add($"Cannot delete '{role.Name}' — {role.UserCount} user(s) are still assigned to this role.", Severity.Warning);
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

        var deleteResult = await RoleService.DeleteRoleAsync(role.Id);
        if (deleteResult.Succeeded)
        {
            Snackbar.Add($"Role '{role.Name}' deleted successfully.", Severity.Success);
            await dataGrid.ReloadServerData();
        }
        else
        {
            ErrorMessage = deleteResult.Error ?? "Failed to delete role.";
        }
    }

    #endregion

    #region View Models

    /// <summary>
    /// Flattened view model for the role data grid.
    /// </summary>
    public class RoleViewModel
    {
        /// <summary>Display line number (1-based, page-aware).</summary>
        public int LineNumber { get; set; }

        /// <summary>The role's Identity ID.</summary>
        public string Id { get; set; } = "";

        /// <summary>The technical role name.</summary>
        public string Name { get; set; } = "";

        /// <summary>The human-readable display name.</summary>
        public string DisplayName { get; set; } = "";

        /// <summary>The role description.</summary>
        public string? Description { get; set; }

        /// <summary>Whether the role is active.</summary>
        public bool IsActive { get; set; }

        /// <summary>Whether the role is a protected system role.</summary>
        public bool IsSystem { get; set; }

        /// <summary>The role's position value indicating authority level.</summary>
        public int Position { get; set; }

        /// <summary>Number of users currently assigned to this role.</summary>
        public int UserCount { get; set; }

        /// <summary>When the role was created (UTC).</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>When the role was last updated (UTC).</summary>
        public DateTime? UpdatedUtc { get; set; }

        /// <summary>
        /// Determines equality by <see cref="Id"/>.
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
