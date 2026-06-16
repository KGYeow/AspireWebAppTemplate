using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Admin.UserManagement;

/// <summary>
/// Dialog for selecting a role to assign in bulk to multiple users.
/// Supports two modes: Add (default) or Replace existing roles.
/// Returns a <see cref="BulkAssignRoleResult"/> on confirmation.
/// </summary>
public partial class BulkAssignRoleDialog : ComponentBase
{
    #region Cascading Parameters

    /// <summary>
    /// The MudBlazor dialog instance.
    /// </summary>
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    #endregion

    #region Parameters

    /// <summary>
    /// All available role names displayed in the dropdown.
    /// </summary>
    [Parameter]
    public List<string> AllRoleNames { get; set; } = [];

    /// <summary>
    /// The number of users that will be affected by this bulk action.
    /// Displayed in the dialog content for user awareness.
    /// </summary>
    [Parameter]
    public int UserCount { get; set; }

    #endregion

    #region State

    /// <summary>
    /// The currently selected role in the dropdown.
    /// </summary>
    private string? SelectedRole { get; set; }

    /// <summary>
    /// Whether to replace all existing roles (true) or add the role to existing ones (false).
    /// Defaults to false (add mode) — the safer, non-destructive option.
    /// </summary>
    private bool ReplaceExisting { get; set; }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Cancels the dialog without applying changes.
    /// </summary>
    private void Cancel() => MudDialog.Cancel();

    /// <summary>
    /// Confirms the role selection and returns the result.
    /// </summary>
    private void Confirm()
    {
        if (!string.IsNullOrWhiteSpace(SelectedRole))
        {
            var result = new BulkAssignRoleResult(SelectedRole, ReplaceExisting);
            MudDialog.Close(DialogResult.Ok(result));
        }
    }

    #endregion
}

/// <summary>
/// Result returned by <see cref="BulkAssignRoleDialog"/> containing
/// the selected role and whether to replace existing roles.
/// </summary>
/// <param name="RoleName">The role to assign.</param>
/// <param name="ReplaceExisting">If true, remove all existing roles before assigning. If false, add to existing roles.</param>
public record BulkAssignRoleResult(string RoleName, bool ReplaceExisting);
