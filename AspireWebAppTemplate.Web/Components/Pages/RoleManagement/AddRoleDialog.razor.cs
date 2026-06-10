using System.ComponentModel.DataAnnotations;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using MudBlazor;

namespace BlazorWebAppTemplate.Components.Pages.RoleManagement;

/// <summary>
/// Dialog for adding a new application role with a name, display name,
/// description, and active status.
/// </summary>
public partial class AddRoleDialog : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages roles.
    /// </summary>
    [Inject] private RoleManager<ApplicationRole> RoleManager { get; set; } = default!;

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
    /// Controls the button disabled state and loading spinner
    /// to prevent duplicate submissions.
    /// </summary>
    protected bool IsBusy { get; private set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the edit context bound to <see cref="Input"/>.
    /// </summary>
    protected override void OnInitialized()
    {
        editContext = new EditContext(Input);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Creates the role on valid form submission.
    /// Checks for duplicate role names before creating.
    /// Closes the dialog with <see cref="DialogResult.Ok{T}"/> on success.
    /// </summary>
    protected async Task OnSubmitAsync()
    {
        if (IsBusy) return;
        if (!editContext.Validate()) return;

        IsBusy = true;
        StatusMessage = null;

        try
        {
            // Guard: check for existing role with the same name
            var existing = await RoleManager.FindByNameAsync(Input.Name);
            if (existing is not null)
            {
                StatusMessage = $"A role with the name '{Input.Name}' already exists.";
                return;
            }

            var role = new ApplicationRole
            {
                Name = Input.Name,
                DisplayName = Input.DisplayName,
                Description = Input.Description,
                IsActive = Input.IsActive,
                Position = Input.Position,
                CreatedUtc = DateTime.UtcNow,
            };

            var createResult = await RoleManager.CreateAsync(role);
            if (!createResult.Succeeded)
            {
                StatusMessage = string.Join(" ", createResult.Errors.Select(e => e.Description));
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
        /// Must be unique across all roles.
        /// </summary>
        [Required]
        [StringLength(50, ErrorMessage = "Role name cannot exceed 50 characters.")]
        [Display(Name = "Role Name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// A human-readable label shown in the UI (e.g., "Administrator").
        /// Falls back to <see cref="Name"/> if not set.
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
        /// The authority position of the role. Higher values indicate higher authority.
        /// Must be zero or positive.
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Position must be zero or positive.")]
        [Display(Name = "Position")]
        public int Position { get; set; } = 0;
    }

    #endregion
}