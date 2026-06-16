using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Core.Contracts.Users;
using AspireWebAppTemplate.Core.Utilities;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Admin.UserManagement;

/// <summary>
/// Dialog for editing a user's profile information.
/// Delegates all persistence to the API via <see cref="ApiUserService"/>.
/// </summary>
public partial class EditUserDialog : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for user operations.
    /// </summary>
    [Inject] private ApiUserService UserService { get; set; } = default!;

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
    /// The user's Identity ID to edit.
    /// </summary>
    [Parameter]
    public string UserId { get; set; } = "";

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
    /// Whether the user is being loaded.
    /// </summary>
    protected bool IsLoadingUser { get; private set; } = true;

    /// <summary>
    /// Whether a save operation is in progress.
    /// </summary>
    protected bool IsBusy { get; set; }

    /// <summary>
    /// Error message displayed on failure.
    /// </summary>
    protected string? StatusMessage { get; set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the edit context and loads the user data.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        editContext = new EditContext(Input);

        var userResult = await UserService.GetUserAsync(UserId);
        if (userResult.Succeeded && userResult.Data is not null)
        {
            var user = userResult.Data;
            Input.DisplayName = user.DisplayName ?? "";
            Input.FirstName = user.FirstName;
            Input.LastName = user.LastName;
            Input.Email = user.Email ?? "";
            Input.Phone = user.PhoneNumber;
            Input.EmployeeNumber = user.EmployeeNumber;
            Input.JobTitle = user.JobTitle;
            Input.Department = user.Department;
        }

        IsLoadingUser = false;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Saves the updated user profile via the API.
    /// </summary>
    protected async Task OnSubmitAsync()
    {
        if (IsBusy) return;
        if (!editContext.Validate()) return;

        IsBusy = true;
        StatusMessage = null;

        try
        {
            var request = new UpdateUserRequest
            {
                DisplayName = Input.DisplayName,
                FirstName = Input.FirstName,
                LastName = Input.LastName,
                Email = Input.Email,
                PhoneNumber = Input.Phone,
                EmployeeNumber = Input.EmployeeNumber,
                JobTitle = Input.JobTitle,
                Department = Input.Department
            };

            var result = await UserService.UpdateUserAsync(UserId, request);
            if (!result.Succeeded)
            {
                StatusMessage = result.Error ?? "Failed to update user.";
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
    /// Form model for the edit user dialog.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>The display name.</summary>
        [Required]
        [Display(Name = "Display Name")]
        public string DisplayName { get; set; } = "";

        /// <summary>The first name.</summary>
        [Display(Name = "First Name")]
        public string? FirstName { get; set; }

        /// <summary>The last name.</summary>
        [Display(Name = "Last Name")]
        public string? LastName { get; set; }

        /// <summary>The email address.</summary>
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = "";

        /// <summary>The phone number.</summary>
        [Display(Name = "Phone")]
        public string? Phone { get; set; }

        /// <summary>The employee number.</summary>
        [OptionalPhone]
        [Display(Name = "Employee Number")]
        public string? EmployeeNumber { get; set; }

        /// <summary>The job title.</summary>
        [Display(Name = "Job Title")]
        public string? JobTitle { get; set; }

        /// <summary>The department.</summary>
        [Display(Name = "Department")]
        public string? Department { get; set; }
    }

    #endregion
}
