using AspireWebAppTemplate.Core.Contracts.Roles;
using AspireWebAppTemplate.Core.Contracts.Users;
using AspireWebAppTemplate.UI.Components.Shared;
using AspireWebAppTemplate.UI.Utilities;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using AspireWebAppTemplate.Web.Abstractions;

namespace AspireWebAppTemplate.Web.Components.Pages.Admin.RoleManagement;

/// <summary>
/// Role details page. Displays all information about a role
/// organized in sections, including assigned users in a server-side data grid.
/// Admin role required. All operations delegated to the API via <see cref="ApiRoleService"/>.
/// </summary>
public partial class Details : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for role operations.
    /// </summary>
    [Inject] private ApiRoleService RoleService { get; set; } = default!;

    /// <summary>
    /// HTTP client service for user operations.
    /// </summary>
    [Inject] private ApiUserService UserService { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Provides user-aware datetime formatting in the viewer's configured time zone.
    /// </summary>
    [Inject] private IUserTimeZoneContext UserTimeZone { get; set; } = default!;

    #endregion

    #region Route Parameters

    /// <summary>
    /// The role's Identity ID from the route.
    /// </summary>
    [Parameter]
    public string RoleId { get; set; } = "";

    #endregion

    #region Server-Side Users Data Grid

    /// <summary>
    /// Reference to the users MudDataGrid component for triggering server-side reloads.
    /// </summary>
    private MudDataGrid<UserViewModel> usersDataGrid = null!;

    /// <summary>
    /// Server-side helper that applies column filters, multi-sort, global search,
    /// and pagination for the users grid.
    /// </summary>
    private readonly DataGridUtils<UserViewModel> _usersDataGridUtils = new DataGridUtils<UserViewModel>()
        .MapString(nameof(UserViewModel.UserName), x => x.UserName)
        .MapString(nameof(UserViewModel.DisplayName), x => x.DisplayName)
        .MapString(nameof(UserViewModel.Email), x => x.Email);

    /// <summary>
    /// The current search term for the users grid toolbar search box.
    /// </summary>
    private string? usersSearchString;

    /// <summary>
    /// Total number of users assigned to this role.
    /// </summary>
    protected int UsersInRoleCount { get; private set; }

    /// <summary>
    /// The set of currently selected users in the data grid.
    /// </summary>
    private HashSet<UserViewModel> selectedUsers = new();

    #endregion

    #region State

    /// <summary>
    /// The loaded role DTO.
    /// </summary>
    protected RoleDto? Role { get; private set; }

    /// <summary>
    /// Whether data is currently loading.
    /// </summary>
    protected bool IsLoading { get; private set; } = true;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads the role on initialization.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var result = await RoleService.GetRoleAsync(RoleId);
            if (result.Succeeded && result.Data is not null)
                Role = result.Data;
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Server-Side Users Data Loading

    /// <summary>
    /// Loads users assigned to the current role from the API.
    /// </summary>
    private async Task<IEnumerable<UserViewModel>> LoadUsersInRoleAsync()
    {
        if (Role is null) return [];

        var result = await RoleService.GetUsersInRoleAsync(Role.Id);
        if (!result.Succeeded || result.Data is null) return [];

        return result.Data.Select(u => new UserViewModel { User = u });
    }

    /// <summary>
    /// Server-side reload callback for the users MudDataGrid.
    /// </summary>
    private async Task<GridData<UserViewModel>> ServerReloadUsers(GridState<UserViewModel> state, CancellationToken cancellationToken)
    {
        async Task<IEnumerable<UserViewModel>> loader()
        {
            var users = await LoadUsersInRoleAsync();
            var userList = users.ToList();
            UsersInRoleCount = userList.Count;
            return userList;
        }

        IEnumerable<string> GlobalFields(UserViewModel u) => new[]
        {
            u.UserName,
            u.DisplayName,
            u.Email
        };

        void SetLine(UserViewModel item, int lineNo) => item.LineNumber = lineNo;

        var gridData = await _usersDataGridUtils.ServerReloadAsync(
            state,
            loader,
            globalSearchTerm: usersSearchString,
            globalSearchFieldSelector: GlobalFields,
            setLineNumber: SetLine);

        await InvokeAsync(StateHasChanged);

        return gridData;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles the search text change from the users grid toolbar.
    /// </summary>
    private Task OnUsersSearch(string text)
    {
        usersSearchString = text;
        return usersDataGrid.ReloadServerData();
    }

    /// <summary>
    /// Clears the current user selection.
    /// </summary>
    private void ClearUserSelection()
    {
        selectedUsers = new HashSet<UserViewModel>();
    }

    /// <summary>
    /// Removes all selected users from the current role via the API.
    /// </summary>
    protected async Task BulkRemoveUsersAsync()
    {
        if (Role is null || selectedUsers.Count == 0) return;

        // Guard: prevent removal that would leave a RequiresMinimumUser role empty
        if (Role.RequiresMinimumUser && selectedUsers.Count >= UsersInRoleCount)
        {
            Snackbar.Add($"Cannot remove all users from the role '{Role.Name}'. At least one user must remain assigned.", Severity.Error);
            return;
        }

        var targets = selectedUsers.ToList();

        var parameters = new DialogParameters<ConfirmationDialog>
        {
            { x => x.ContentText, $"Are you sure you want to remove {targets.Count} user(s) from the role '{Role.DisplayName ?? Role.Name}'?" },
            { x => x.SubmitBtnText, "Remove" },
            { x => x.DialogIcon, Icons.Material.Rounded.PersonRemove },
            { x => x.DialogIconColor, Color.Error }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.ExtraSmall, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Remove Users from Role", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        int successCount = 0, failedCount = 0;

        foreach (var userVm in targets)
        {
            var removeResult = await RoleService.RemoveUserFromRoleAsync(Role.Id, userVm.Id);
            if (removeResult.Succeeded) successCount++;
            else failedCount++;
        }

        if (failedCount == 0)
            Snackbar.Add($"Successfully removed {successCount} user(s) from role '{Role.DisplayName ?? Role.Name}'.", Severity.Success);
        else
            Snackbar.Add($"Removed {successCount} user(s), {failedCount} failed.", Severity.Warning);

        selectedUsers = new HashSet<UserViewModel>();
        await usersDataGrid.ReloadServerData();
    }

    /// <summary>
    /// Opens the Assign Users to Role dialog.
    /// </summary>
    protected async Task OpenAssignUsersDialog()
    {
        if (Role is null) return;

        // Get current users in role for the exclusion list
        var currentUsersResult = await RoleService.GetUsersInRoleAsync(Role.Id);
        var existingUserIds = (currentUsersResult.Succeeded && currentUsersResult.Data is not null)
            ? currentUsersResult.Data.Select(u => u.Id).ToList()
            : [];

        var parameters = new DialogParameters<AssignUsersToRoleDialog>
        {
            { x => x.RoleName, Role.Name },
            { x => x.RoleId, Role.Id },
            { x => x.ExistingUserIds, existingUserIds }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<AssignUsersToRoleDialog>("Assign Users to Role", parameters, options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            await usersDataGrid.ReloadServerData();
        }
    }

    /// <summary>
    /// Removes a single user from the current role via the API.
    /// </summary>
    protected async Task RemoveUserFromRoleAsync(UserViewModel user)
    {
        if (Role is null) return;

        var parameters = new DialogParameters<ConfirmationDialog>
        {
            { x => x.ContentText, $"Are you sure you want to remove '{user.DisplayName ?? user.UserName}' from the role '{Role.DisplayName ?? Role.Name}'?" },
            { x => x.SubmitBtnText, "Remove" },
            { x => x.DialogIcon, Icons.Material.Rounded.PersonRemove },
            { x => x.DialogIconColor, Color.Error }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.ExtraSmall, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Remove User from Role", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        // Guard: prevent removing the last user from a RequiresMinimumUser role
        if (Role.RequiresMinimumUser && UsersInRoleCount <= 1)
        {
            Snackbar.Add($"Cannot remove the last user from the role '{Role.Name}'. At least one user must remain assigned.", Severity.Error);
            return;
        }

        var removeResult = await RoleService.RemoveUserFromRoleAsync(Role.Id, user.Id);
        if (removeResult.Succeeded)
        {
            Snackbar.Add($"'{user.DisplayName ?? user.UserName}' removed from role '{Role.DisplayName ?? Role.Name}'.", Severity.Success);
            await usersDataGrid.ReloadServerData();
        }
        else
        {
            Snackbar.Add("Failed to remove user from role.", Severity.Error);
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Formats a UTC DateTime in the viewer's time zone.
    /// </summary>
    protected string FormatDateTime(DateTime utcDateTime)
        => UserTimeZone.FormatDateTime(utcDateTime, format: "dd/MM/yyyy hh:mm:ss tt");

    /// <summary>
    /// Formats a nullable UTC DateTime in the viewer's time zone.
    /// </summary>
    protected string FormatDateTime(DateTime? utcDateTime, string fallback = "Never")
        => UserTimeZone.FormatDateTime(utcDateTime, format: "dd/MM/yyyy hh:mm:ss tt", fallback: fallback);

    #endregion

    #region View Model

    /// <summary>
    /// Wrapper view model for the users-in-role data grid.
    /// Holds a <see cref="UserDto"/> reference and delegates properties.
    /// </summary>
    public class UserViewModel
    {
        /// <summary>Display line number (1-based, page-aware).</summary>
        public int LineNumber { get; set; }

        /// <summary>The underlying user DTO.</summary>
        public UserDto User { get; set; } = default!;

        // Delegated from DTO
        public string Id => User.Id;
        public string UserName => User.UserName;
        public string DisplayName => User.DisplayName ?? "";
        public string Email => User.Email ?? "";

        public override bool Equals(object? obj) => obj is UserViewModel other && Id == other.Id;

        public override int GetHashCode() => Id.GetHashCode();
    }

    #endregion
}
