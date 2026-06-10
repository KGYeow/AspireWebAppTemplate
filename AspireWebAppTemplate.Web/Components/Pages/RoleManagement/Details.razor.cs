using BlazorWebAppTemplate.Abstractions;
using BlazorWebAppTemplate.Core.Domain.Enums;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using BlazorWebAppTemplate.UI.Components.Shared;
using BlazorWebAppTemplate.UI.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using MudBlazor;
using static BlazorWebAppTemplate.Components.Pages.UserManagement.Index;

namespace BlazorWebAppTemplate.Components.Pages.RoleManagement;

/// <summary>
/// Role details page. Displays all information about a role
/// organized in sections, including assigned users in a server-side data grid.
/// Admin role required.
/// </summary>
public partial class Details : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages application roles.
    /// </summary>
    [Inject] private RoleManager<ApplicationRole> RoleManager { get; set; } = default!;

    /// <summary>
    /// Manages user accounts.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Provides user-aware datetime formatting in the viewer's configured time zone.
    /// </summary>
    [Inject] private IUserTimeZoneContext UserTimeZone { get; set; } = default!;

    /// <summary>
    /// Audit log service for recording role unassignment events.
    /// </summary>
    [Inject] private IAuditLogService AuditLogService { get; set; } = default!;

    #endregion

    #region Cascading Parameters

    /// <summary>
    /// Provides the current authentication state for identifying the acting user.
    /// </summary>
    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

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
    /// Total number of users assigned to this role. Updated after each grid reload.
    /// Displayed in the "Assigned Users (N)" section title.
    /// </summary>
    protected int UsersInRoleCount { get; private set; }

    /// <summary>
    /// The set of currently selected users in the data grid.
    /// Bound via <c>@bind-SelectedItems</c>.
    /// </summary>
    private HashSet<UserViewModel> selectedUsers = new();

    #endregion

    #region State

    /// <summary>
    /// The loaded role entity.
    /// </summary>
    protected ApplicationRole? Role { get; private set; }

    /// <summary>
    /// Whether data is currently loading.
    /// </summary>
    protected bool IsLoading { get; private set; } = true;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads the role on initialization. Users are NOT pre-loaded here —
    /// the <see cref="ServerReloadUsers"/> callback handles all user data loading.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        try
        {
            Role = await RoleManager.FindByIdAsync(RoleId);
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Server-Side Users Data Loading

    /// <summary>
    /// Loads users assigned to the current role from the database and maps them to <see cref="UserViewModel"/> instances.
    /// Called by the <see cref="ServerReloadUsers"/> callback on every grid reload.
    /// </summary>
    private async Task<IEnumerable<UserViewModel>> LoadUsersInRoleAsync()
    {
        if (Role is null) return Enumerable.Empty<UserViewModel>();

        var users = await UserManager.GetUsersInRoleAsync(Role.Name!);

        return users.Select(u => new UserViewModel
        {
            Id = u.Id,
            UserName = u.UserName ?? "",
            DisplayName = u.DisplayName ?? "",
            Email = u.Email ?? ""
        });
    }

    /// <summary>
    /// Server-side reload callback for the users <see cref="MudDataGrid{T}"/>.
    /// Fetches fresh user data via <see cref="LoadUsersInRoleAsync"/>, then delegates
    /// filtering, sorting, pagination, and line numbering to <see cref="DataGridUtils{T}.ServerReloadAsync"/>.
    /// </summary>
    private async Task<GridData<UserViewModel>> ServerReloadUsers(GridState<UserViewModel> state, CancellationToken cancellationToken)
    {
        // Loader function: fetches users in role and maps to view models
        async Task<IEnumerable<UserViewModel>> loader()
        {
            var users = await LoadUsersInRoleAsync();
            var userList = users.ToList();

            // Update the total count for the section title (unfiltered)
            UsersInRoleCount = userList.Count;

            return userList;
        }

        // Global search fields — searches across username, display name, and email
        IEnumerable<string> GlobalFields(UserViewModel u) => new[]
        {
            u.UserName,
            u.DisplayName,
            u.Email
        };

        // Set page-aware line numbers (e.g., page 2 with size 10 starts at 11)
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
    /// Handles the search text change from the users grid toolbar search box.
    /// Triggers a server-side reload with the new search term.
    /// </summary>
    private Task OnUsersSearch(string text)
    {
        usersSearchString = text;
        return usersDataGrid.ReloadServerData();
    }

    /// <summary>
    /// Clears the current user selection in the data grid.
    /// </summary>
    private void ClearUserSelection()
    {
        selectedUsers = new HashSet<UserViewModel>();
    }

    /// <summary>
    /// Removes all selected users from the current role after showing a confirmation dialog.
    /// Displays a summary snackbar with success/failed counts and reloads the grid.
    /// Guards against removing all users from a role that requires at least one user.
    /// Logs a RoleUnassigned audit entry for each successful removal.
    /// </summary>
    protected async Task BulkRemoveUsersAsync()
    {
        if (Role is null || selectedUsers.Count == 0) return;

        // Guard: prevent bulk removal that would leave a RequiresMinimumUser role empty
        if (Role.RequiresMinimumUser)
        {
            var usersInRole = await UserManager.GetUsersInRoleAsync(Role.Name!);
            if (selectedUsers.Count >= usersInRole.Count)
            {
                Snackbar.Add($"Cannot remove all users from the role '{Role.Name}'. At least one user must remain assigned.", Severity.Error);
                return;
            }
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

        // Resolve the acting user's ID for audit logging
        var authState = await AuthStateTask;
        var actingUserId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        int successCount = 0;
        int failedCount = 0;

        foreach (var userVm in targets)
        {
            var appUser = await UserManager.FindByIdAsync(userVm.Id);
            if (appUser is null) { failedCount++; continue; }

            var removeResult = await UserManager.RemoveFromRoleAsync(appUser, Role.Name!);
            if (removeResult.Succeeded)
            {
                successCount++;

                // Log audit entry for role unassignment — failures are swallowed by the service
                try
                {
                    await AuditLogService.LogAsync(
                        actingUserId,
                        AuditActionType.RoleUnassigned,
                        AuditEntityType.Role,
                        entityId: userVm.Id,
                        entityName: Role.Name!,
                        description: $"Role '{Role.Name}' removed from user '{userVm.DisplayName ?? userVm.UserName}'.");
                }
                catch
                {
                    // Audit failures must not interrupt the primary operation
                }
            }
            else
                failedCount++;
        }

        if (failedCount == 0)
        {
            Snackbar.Add($"Successfully removed {successCount} user(s) from role '{Role.DisplayName ?? Role.Name}'.", Severity.Success);
        }
        else
        {
            Snackbar.Add($"Removed {successCount} user(s), {failedCount} failed.", Severity.Warning);
        }

        selectedUsers = new HashSet<UserViewModel>();
        await usersDataGrid.ReloadServerData();
    }

    /// <summary>
    /// Opens the Assign Users to Role dialog (multi-select).
    /// Fetches current users in role to build the exclusion list,
    /// then opens the dialog. On success, reloads the users grid.
    /// </summary>
    protected async Task OpenAssignUsersDialog()
    {
        if (Role is null) return;

        // Fetch current users in role to exclude from the dialog
        var currentUsers = await UserManager.GetUsersInRoleAsync(Role.Name!);

        var parameters = new DialogParameters<AssignUsersToRoleDialog>
        {
            { x => x.RoleName, Role.Name! },
            { x => x.ExistingUserIds, currentUsers.Select(u => u.Id).ToList() }
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
    /// Removes a user from the current role after showing a confirmation dialog.
    /// On success, reloads the users data grid and logs a RoleUnassigned audit entry.
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

        // Guard: prevent removing the last user from a role that requires at least one user
        if (Role.RequiresMinimumUser)
        {
            var usersInRole = await UserManager.GetUsersInRoleAsync(Role.Name!);
            if (usersInRole.Count <= 1)
            {
                Snackbar.Add($"Cannot remove the last user from the role '{Role.Name}'. At least one user must remain assigned.", Severity.Error);
                return;
            }
        }

        var appUser = await UserManager.FindByIdAsync(user.Id);
        if (appUser is null)
        {
            Snackbar.Add("User not found.", Severity.Error);
            return;
        }

        var removeResult = await UserManager.RemoveFromRoleAsync(appUser, Role.Name!);
        if (removeResult.Succeeded)
        {
            Snackbar.Add($"'{user.DisplayName ?? user.UserName}' removed from role '{Role.DisplayName ?? Role.Name}'.", Severity.Success);
            await usersDataGrid.ReloadServerData();

            // Log audit entry for role unassignment — failures are swallowed by the service
            try
            {
                var authState = await AuthStateTask;
                var actingUserId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                await AuditLogService.LogAsync(
                    actingUserId,
                    AuditActionType.RoleUnassigned,
                    AuditEntityType.Role,
                    entityId: user.Id,
                    entityName: Role.Name!,
                    description: $"Role '{Role.Name}' removed from user '{user.DisplayName ?? user.UserName}'.");
            }
            catch
            {
                // Audit failures must not interrupt the primary operation
            }
        }
        else
        {
            Snackbar.Add(
                "Failed to remove user: " + string.Join(", ", removeResult.Errors.Select(e => e.Description)),
                Severity.Error);
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
}
