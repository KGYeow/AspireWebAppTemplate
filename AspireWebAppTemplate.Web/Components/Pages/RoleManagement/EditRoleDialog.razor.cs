using System.ComponentModel.DataAnnotations;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace BlazorWebAppTemplate.Components.Pages.RoleManagement;

/// <summary>
/// Dialog for editing an existing application role's name, display name,
/// description, and active status.
/// Loads the current role data on initialization and updates it on submission.
/// </summary>
public partial class EditRoleDialog : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages roles. Used to load and update the target role.
    /// </summary>
    [Inject] private RoleManager<ApplicationRole> RoleManager { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording errors during role load and update.
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
    /// Used to load the existing role data on initialization.
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
    /// Initialized after the role is loaded in <see cref="OnInitializedAsync"/>.
    /// </summary>
    private EditContext editContext = default!;

    /// <summary>
    /// The original role name loaded from Identity.
    /// Used to detect name changes and check for conflicts on save.
    /// </summary>
    private string originalName = "";

    /// <summary>
    /// Whether the role data is currently being loaded.
    /// Hides the form and disables the save button while true.
    /// </summary>
    protected bool IsLoading { get; private set; } = true;

    /// <summary>
    /// Controls the save button's disabled state and loading spinner
    /// to prevent duplicate submissions.
    /// </summary>
    protected bool IsBusy { get; private set; }

    /// <summary>
    /// Status message displayed in the error alert on validation
    /// or persistence failure.
    /// </summary>
    protected string? StatusMessage { get; private set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads the existing role data by <see cref="RoleId"/> and
    /// pre-populates the <see cref="Input"/> model.
    /// Closes the dialog immediately if the role is not found.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var role = await RoleManager.FindByIdAsync(RoleId);
            if (role is null)
            {
                Logger.LogWarning("EditRoleDialog: role '{RoleId}' not found.", RoleId);
                MudDialog.Cancel();
                return;
            }

            // Pre-populate form with existing values
            originalName = role.Name ?? "";
            Input = new InputModel
            {
                Name = role.Name ?? "",
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
            // Initialize edit context after Input is populated
            editContext = new EditContext(Input);
            IsLoading = false;
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Saves the updated role data on valid form submission.
    /// Checks for name conflicts if the role name has changed.
    /// Stamps <see cref="ApplicationRole.UpdatedUtc"/> on success.
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
            var role = await RoleManager.FindByIdAsync(RoleId);
            if (role is null)
            {
                StatusMessage = "Role no longer exists. It may have been deleted.";
                return;
            }

            // Guard: check for name conflict only if the name has changed
            var nameChanged = !string.Equals(
                Input.Name, originalName,
                StringComparison.OrdinalIgnoreCase);

            if (nameChanged)
            {
                var existing = await RoleManager.FindByNameAsync(Input.Name);
                if (existing is not null)
                {
                    StatusMessage = $"A role with the name '{Input.Name}' already exists.";
                    return;
                }
            }

            // Apply changes
            role.Name = Input.Name;
            role.DisplayName = Input.DisplayName;
            role.Description = Input.Description;
            role.IsActive = Input.IsActive;
            role.Position = Input.Position;
            role.UpdatedUtc = DateTime.UtcNow;

            var updateResult = await RoleManager.UpdateAsync(role);
            if (!updateResult.Succeeded)
            {
                StatusMessage = string.Join(" ", updateResult.Errors.Select(e => e.Description));
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
    /// Pre-populated from the existing <see cref="ApplicationRole"/> on load.
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
        /// Whether the role is currently active.
        /// </summary>
        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// The authority hierarchy position of this role.
        /// Higher values indicate higher authority.
        /// </summary>
        [Display(Name = "Position")]
        public int Position { get; set; }
    }

    #endregion
}