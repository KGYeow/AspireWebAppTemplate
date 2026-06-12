using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.RoleManagement;

/// <summary>
/// Dialog for searching and assigning multiple users to a role.
/// Displays a searchable list of available users (excluding those already assigned),
/// supports multi-selection, and calls the API to assign users.
/// </summary>
public partial class AssignUsersToRoleDialog : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for user operations (searching users).
    /// </summary>
    [Inject] private ApiUserService UserService { get; set; } = default!;

    /// <summary>
    /// HTTP client service for role operations (assigning users to role).
    /// </summary>
    [Inject] private ApiRoleService RoleService { get; set; } = default!;

    #endregion

    #region Cascading Parameters

    /// <summary>
    /// The MudBlazor dialog instance.
    /// </summary>
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    #endregion

    #region Parameters

    /// <summary>
    /// The name of the role to assign the selected users to.
    /// </summary>
    [Parameter]
    public string RoleName { get; set; } = "";

    /// <summary>
    /// The ID of the role to assign users to.
    /// </summary>
    [Parameter]
    public string RoleId { get; set; } = "";

    /// <summary>
    /// The IDs of users already assigned to the role.
    /// These users are excluded from search results.
    /// </summary>
    [Parameter]
    public List<string> ExistingUserIds { get; set; } = [];

    #endregion

    #region State

    /// <summary>
    /// The current search term entered by the user.
    /// </summary>
    private string SearchTerm { get; set; } = "";

    /// <summary>
    /// The list of users matching the search criteria.
    /// </summary>
    private List<UserDto> SearchResults { get; set; } = [];

    /// <summary>
    /// The set of users selected for assignment.
    /// </summary>
    private HashSet<UserDto> SelectedUsers { get; set; } = [];

    /// <summary>
    /// Controls the button disabled state.
    /// </summary>
    private bool IsBusy { get; set; }

    /// <summary>
    /// Whether a search is currently in progress.
    /// </summary>
    private bool IsSearching { get; set; }

    /// <summary>
    /// Status message displayed on error.
    /// </summary>
    private string? StatusMessage { get; set; }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Searches users by username or display name via the API.
    /// Excludes users already assigned to the role.
    /// </summary>
    private async Task OnSearchAsync(string value)
    {
        SearchTerm = value;
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(SearchTerm) || SearchTerm.Length < 2)
        {
            SearchResults = [];
            return;
        }

        IsSearching = true;

        try
        {
            var allUsers = await UserService.GetAllUsersAsync(SearchTerm);
            SearchResults = allUsers
                .Where(u => !ExistingUserIds.Contains(u.Id))
                .Take(50)
                .ToList();
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>
    /// Toggles a user's selection state.
    /// </summary>
    private void ToggleUserSelection(UserDto user)
    {
        if (!SelectedUsers.Remove(user))
        {
            SelectedUsers.Add(user);
        }
    }

    /// <summary>
    /// Checks if a user is currently selected.
    /// </summary>
    private bool IsUserSelected(UserDto user) => SelectedUsers.Any(u => u.Id == user.Id);

    /// <summary>
    /// Removes a user from the selection.
    /// </summary>
    private void RemoveFromSelection(UserDto user)
    {
        SelectedUsers.RemoveWhere(u => u.Id == user.Id);
    }

    /// <summary>
    /// Assigns all selected users to the role via the API.
    /// </summary>
    private async Task OnConfirmAsync()
    {
        if (IsBusy || SelectedUsers.Count == 0) return;

        IsBusy = true;
        StatusMessage = null;

        try
        {
            var userIds = SelectedUsers.Select(u => u.Id).ToArray();
            var result = await RoleService.AssignUsersToRoleAsync(RoleId, userIds);

            if (result.Succeeded)
            {
                Snackbar.Add($"Successfully assigned {userIds.Length} user(s) to role '{RoleName}'.", Severity.Success);
                MudDialog.Close(DialogResult.Ok(true));
            }
            else
            {
                StatusMessage = result.Error ?? "Failed to assign users to role.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion
}
