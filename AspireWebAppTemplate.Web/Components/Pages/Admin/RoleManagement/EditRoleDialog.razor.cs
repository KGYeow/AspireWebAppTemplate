using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Core.Contracts.Roles;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Admin.RoleManagement;

/// <summary>
/// Dialog for editing an existing application role's name, display name,
/// description, and active status.
/// Delegates all persistence to the API via <see cref="ApiRoleService"/>.
/// </summary>
public partial class EditRoleDialog : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for role operations.
    /// </summary>
    [Inject] private ApiRoleService RoleService { get; set; } = default!;

    /// <summary>
    /// Structured logger.
    /// </summary>
    [Inject] private ILogger<EditRoleDialog> Logger { get; set; } = default!;

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
    /// The Identity ID of the role to edit.
    /// </summary>
    [Parameter]
    public string RoleId { get; set; } = "";

    /// <summary>
    /// Indicates whether the role is a system-protected role.
    /// When true, the Name field is disabled to prevent renaming.
    /// </summary>
    [Parameter]
    public bool IsSystem { get; set; }

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
    /// Whether the role data is currently being loaded.
    /// </summary>
    protected bool IsLoading { get; private set; } = true;

    /// <summary>
    /// Controls the save button's disabled state.
    /// </summary>
    protected bool IsBusy { get; private set; }

    /// <summary>
    /// Status message displayed on error.
    /// </summary>
    protected string? StatusMessage { get; private set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads the existing role data from the API and pre-populates the form.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var roleResult = await RoleService.GetRoleAsync(RoleId);
            if (!roleResult.Succeeded || roleResult.Data is null)
            {
                Logger.LogWarning("EditRoleDialog: role '{RoleId}' not found.", RoleId);
                MudDialog.Cancel();
                return;
            }

            var role = roleResult.Data;
            Input = new InputModel
            {
                Name = role.Name,
                DisplayName = role.DisplayName ?? "",
                Description = role.Description ?? "",
                IsActive = role.IsActive,
                Position = role.Position,
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading role '{RoleId}' for editing.", RoleId);
            StatusMessage = "Failed to load role data. Please try again.";
        }
        finally
        {
            editContext = new EditContext(Input);
            IsLoading = false;
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Saves the updated role data via the API.
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
                Position = Input.Position,
                IsActive = Input.IsActive
            };

            var result = await RoleService.UpdateRoleAsync(RoleId, request);
            if (!result.Succeeded)
            {
                StatusMessage = result.Error ?? "Failed to update role.";
                return;
            }

            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating role '{RoleId}'.", RoleId);
            StatusMessage = "An unexpected error occurred. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Input Model

    /// <summary>
    /// Form model for the edit role dialog.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>
        /// The technical role name.
        /// </summary>
        [Required]
        [StringLength(50, ErrorMessage = "Role name cannot exceed 50 characters.")]
        [Display(Name = "Role Name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// A human-readable label shown in the UI.
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
        /// Whether the role is currently active.
        /// </summary>
        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// The authority hierarchy position.
        /// </summary>
        [Display(Name = "Position")]
        public int Position { get; set; }
    }

    #endregion
}
