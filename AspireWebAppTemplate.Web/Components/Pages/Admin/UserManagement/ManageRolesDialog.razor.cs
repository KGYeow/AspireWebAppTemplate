using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Admin.UserManagement;

/// <summary>
/// Dialog for managing a user's roles with multi-selection.
/// Returns the selected roles as <see cref="HashSet{String}"/> on confirmation.
/// </summary>
public partial class ManageRolesDialog : ComponentBase
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
    /// Display text showing the user's name and username.
    /// </summary>
    [Parameter]
    public string UserDisplayName { get; set; } = "";

    /// <summary>
    /// The user's current roles, used to pre-select checkboxes.
    /// </summary>
    [Parameter]
    public List<string> CurrentRoles { get; set; } = [];

    /// <summary>
    /// All available role names displayed as table rows.
    /// </summary>
    [Parameter]
    public List<string> AllRoleNames { get; set; } = [];

    #endregion

    #region State

    /// <summary>
    /// The currently selected roles in the multi-select table.
    /// </summary>
    protected HashSet<string> SelectedRoles { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The original roles at dialog open, used to detect changes.
    /// </summary>
    private HashSet<string> originalRoles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the selected roles differ from the original roles.
    /// </summary>
    protected bool HasChanges => !SelectedRoles.SetEquals(originalRoles);

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the selected and original role sets from the current roles parameter.
    /// </summary>
    protected override void OnInitialized()
    {
        SelectedRoles = new HashSet<string>(CurrentRoles, StringComparer.OrdinalIgnoreCase);
        originalRoles = new HashSet<string>(CurrentRoles, StringComparer.OrdinalIgnoreCase);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Called when the table selection changes. Updates the selected roles set.
    /// </summary>
    /// <param name="values">The new set of selected role names.</param>
    private void OnRolesChanged(HashSet<string> values)
    {
        SelectedRoles = values ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Cancels the dialog without saving.
    /// </summary>
    private void Cancel() => MudDialog.Cancel();

    /// <summary>
    /// Confirms the role changes and returns the selected roles.
    /// </summary>
    private void Confirm()
    {
        if (HasChanges)
        {
            MudDialog.Close(DialogResult.Ok(SelectedRoles));
        }
    }

    #endregion
}
