using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Application.Contracts.Users;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Admin.UserManagement;

/// <summary>
/// Dialog for adding a new local user with email, display name, password, and role.
/// Delegates all persistence to the API via <see cref="ApiUserService"/>.
/// </summary>
public partial class AddUserDialog : ComponentBase
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
    /// All available role names for the role selector.
    /// </summary>
    [Parameter]
    public List<string> AllRoleNames { get; set; } = [];

    /// <summary>
    /// The default role name to pre-select (the role marked as IsDefault).
    /// </summary>
    [Parameter]
    public string DefaultRoleName { get; set; } = "";

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
    /// Status message on error.
    /// </summary>
    protected string? StatusMessage { get; set; }

    /// <summary>
    /// Controls the button disabled state.
    /// </summary>
    protected bool IsBusy { get; set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the edit context and sets the default role based on the IsDefault parameter.
    /// </summary>
    protected override void OnInitialized()
    {
        Input.Role = AllRoleNames.Contains(DefaultRoleName) ? DefaultRoleName : AllRoleNames.FirstOrDefault() ?? "";
        editContext = new EditContext(Input);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Creates the user on valid form submission via the API.
    /// </summary>
    protected async Task OnSubmitAsync()
    {
        if (IsBusy) return;
        if (!editContext.Validate()) return;

        IsBusy = true;
        StatusMessage = null;

        try
        {
            var request = new CreateUserRequest
            {
                Email = Input.Email,
                DisplayName = Input.DisplayName,
                Password = Input.Password,
                Role = Input.Role
            };

            var result = await UserService.CreateUserAsync(request);
            if (!result.Succeeded)
            {
                StatusMessage = result.Error ?? "Failed to create user.";
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
    /// Form model for the add user dialog.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>The user's email address.</summary>
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = "";

        /// <summary>The user's display name.</summary>
        [Required]
        [Display(Name = "Display Name")]
        public string DisplayName { get; set; } = "";

        /// <summary>The user's password.</summary>
        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = "";

        /// <summary>Confirmation of the password.</summary>
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = "";

        /// <summary>The role to assign.</summary>
        [Display(Name = "Role")]
        public string Role { get; set; } = "";
    }

    #endregion
}
