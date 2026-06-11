using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.RoleManagement;

/// <summary>
/// Dialog for adding a new application role with a name, display name,
/// description, and active status.
/// Delegates all persistence to the API via <see cref="ApiRoleService"/>.
/// </summary>
public partial class AddRoleDialog : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for role operations.
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

    #region State

    /// <summary>
    /// The form input model.
    /// </summary>
    private InputModel Input { get; set; } = new();

    /// <summary>
    /// Drives the <c>EditForm</c> validation context.
    /// </summary>
    private EditContext editContext = default!;

    /// <summary>
    /// Status message displayed on error.
    /// </summary>
    protected string? StatusMessage { get; private set; }

    /// <summary>
    /// Controls the button disabled state and loading spinner.
    /// </summary>
    protected bool IsBusy { get; private set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the edit context.
    /// </summary>
    protected override void OnInitialized()
    {
        editContext = new EditContext(Input);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Creates the role on valid form submission via the API.
    /// </summary>
    protected async Task OnSubmitAsync()
    {
        if (IsBusy) return;
        if (!editContext.Validate()) return;

        IsBusy = true;
        StatusMessage = null;

        try
        {
            var request = new CreateRoleRequest
            {
                Name = Input.Name,
                DisplayName = Input.DisplayName,
                Description = Input.Description,
                Position = Input.Position
            };

            var (success, error) = await RoleService.CreateRoleAsync(request);
            if (!success)
            {
                StatusMessage = error ?? "Failed to create role.";
                return;
            }

            MudDialog.Close(DialogResult.Ok(true));
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Input Model

    /// <summary>
    /// Form model for the add role dialog.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>
        /// The technical role name used by Identity (e.g., "Admin").
        /// </summary>
        [Required]
        [StringLength(50, ErrorMessage = "Role name cannot exceed 50 characters.")]
        [Display(Name = "Role Name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// A human-readable label shown in the UI (e.g., "Administrator").
        /// </summary>
        [StringLength(100, ErrorMessage = "Display name cannot exceed 100 characters.")]
        [Display(Name = "Display Name")]
        public string? DisplayName { get; set; }

        /// <summary>
        /// Describes the purpose or permissions scope of this role.
        /// </summary>
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        /// <summary>
        /// Whether the role is active upon creation. Defaults to true.
        /// </summary>
        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// The authority position of the role.
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Position must be zero or positive.")]
        [Display(Name = "Position")]
        public int Position { get; set; } = 0;
    }

    #endregion
}
