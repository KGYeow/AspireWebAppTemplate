using BlazorWebAppTemplate.Abstractions;
using BlazorWebAppTemplate.Core.Domain.Enums;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace BlazorWebAppTemplate.Components.Pages.RoleManagement;

/// <summary>
/// Dialog for searching and assigning multiple users to a role.
/// Displays a searchable list of available users (excluding those already assigned),
/// supports multi-selection, and calls UserManager.AddToRoleAsync for each selected user.
/// Returns success/failed counts via snackbar notification.
/// </summary>
public partial class AssignUsersToRoleDialog : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages user accounts. Used to search users and assign the role.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Audit log service for recording role assignment events.
    /// </summary>
    [Inject] private IAuditLogService AuditLogService { get; set; } = default!;

    #endregion

    #region Cascading Parameters

    /// <summary>
    /// The MudBlazor dialog instance.
    /// </summary>
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>
    /// Provides the current authentication state for identifying the acting user.
    /// </summary>
    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    #endregion

    #region Parameters

    /// <summary>
    /// The name of the role to assign the selected users to.
    /// </summary>
    [Parameter]
    public string RoleName { get; set; } = "";

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
    private List<ApplicationUser> SearchResults { get; set; } = [];

    /// <summary>
    /// The set of users selected for assignment.
    /// </summary>
    private HashSet<ApplicationUser> SelectedUsers { get; set; } = [];

    /// <summary>
    /// Controls the button disabled state and loading spinner
    /// to prevent duplicate submissions.
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
    /// Searches users by username or display name when the search term changes.
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
            var term = SearchTerm.ToLower();
            SearchResults = await UserManager.Users
                .Where(u => !ExistingUserIds.Contains(u.Id) &&
                    (u.UserName!.ToLower().Contains(term) ||
                    (u.DisplayName != null && u.DisplayName.ToLower().Contains(term))))
                .OrderBy(u => u.UserName)
                .Take(50)
                .ToListAsync();
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>
    /// Toggles a user's selection state.
    /// </summary>
    private void ToggleUserSelection(ApplicationUser user)
    {
        if (!SelectedUsers.Remove(user))
        {
            SelectedUsers.Add(user);
        }
    }

    /// <summary>
    /// Checks if a user is currently selected.
    /// </summary>
    private bool IsUserSelected(ApplicationUser user) => SelectedUsers.Contains(user);

    /// <summary>
    /// Removes a user from the selection.
    /// </summary>
    private void RemoveFromSelection(ApplicationUser user)
    {
        SelectedUsers.Remove(user);
    }

    /// <summary>
    /// Assigns all selected users to the role and closes the dialog on success.
    /// Displays a summary snackbar with success/failed counts.
    /// Logs a RoleAssigned audit entry for each successful assignment.
    /// </summary>
    private async Task OnConfirmAsync()
    {
        if (IsBusy || SelectedUsers.Count == 0) return;

        IsBusy = true;
        StatusMessage = null;

        try
        {
            // Resolve the acting user's ID for audit logging
            var authState = await AuthStateTask;
            var actingUserId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            int successCount = 0;
            int failedCount = 0;

            foreach (var user in SelectedUsers)
            {
                var result = await UserManager.AddToRoleAsync(user, RoleName);
                if (result.Succeeded)
                {
                    successCount++;

                    // Log audit entry for role assignment — failures are swallowed by the service
                    try
                    {
                        await AuditLogService.LogAsync(
                            actingUserId,
                            AuditActionType.RoleAssigned,
                            AuditEntityType.Role,
                            entityId: user.Id,
                            entityName: RoleName,
                            description: $"Role '{RoleName}' assigned to user '{user.DisplayName ?? user.UserName}'.");
                    }
                    catch
                    {
                        // Audit failures must not interrupt the primary operation
                    }
                }
                else
                {
                    failedCount++;
                }
            }

            // Show summary snackbar
            if (failedCount == 0)
            {
                Snackbar.Add($"Successfully assigned {successCount} user(s) to role '{RoleName}'.", Severity.Success);
            }
            else
            {
                Snackbar.Add($"Assigned {successCount} user(s), {failedCount} failed for role '{RoleName}'.", Severity.Warning);
            }

            MudDialog.Close(DialogResult.Ok(true));
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion
}
