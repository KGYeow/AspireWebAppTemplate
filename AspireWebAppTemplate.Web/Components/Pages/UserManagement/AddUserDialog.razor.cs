using System.ComponentModel.DataAnnotations;
using BlazorWebAppTemplate.Abstractions;
using BlazorWebAppTemplate.Core.Domain.Enums;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using MudBlazor;

namespace BlazorWebAppTemplate.Components.Pages.UserManagement;

/// <summary>
/// Dialog for adding a new local user with email, display name, password, and role.
/// </summary>
public partial class AddUserDialog : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages user accounts.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Manages roles for querying the default role.
    /// </summary>
    [Inject] private RoleManager<ApplicationRole> RoleManager { get; set; } = default!;

    /// <summary>
    /// Audit log service for recording user creation events.
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
    /// All available role names for the role selector.
    /// </summary>
    [Parameter]
    public List<string> AllRoleNames { get; set; } = [];

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
    /// Initializes the edit context and sets the default role.
    /// </summary>
    protected override void OnInitialized()
    {
        var defaultRoleName = RoleManager.Roles.FirstOrDefault(r => r.IsDefault)?.Name ?? "User";
        Input.Role = AllRoleNames.Contains(defaultRoleName) ? defaultRoleName : AllRoleNames.FirstOrDefault() ?? "";
        editContext = new EditContext(Input);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Creates the user on valid form submission.
    /// </summary>
    protected async Task OnSubmitAsync()
    {
        if (IsBusy) return;
        if (!editContext.Validate()) return;

        IsBusy = true;
        StatusMessage = null;

        try
        {
            var existing = await UserManager.FindByEmailAsync(Input.Email);
            if (existing is not null)
            {
                StatusMessage = $"A user with email '{Input.Email}' already exists.";
                return;
            }

            var user = new ApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                EmailConfirmed = true,
                DisplayName = Input.DisplayName,
                IsActive = true,
                AuthSource = AuthSource.Local,
                CreatedUtc = DateTime.UtcNow
            };

            var createResult = await UserManager.CreateAsync(user, Input.Password);
            if (!createResult.Succeeded)
            {
                StatusMessage = string.Join(" ", createResult.Errors.Select(e => e.Description));
                return;
            }

            if (!string.IsNullOrEmpty(Input.Role))
            {
                var roleResult = await UserManager.AddToRoleAsync(user, Input.Role);
                if (!roleResult.Succeeded)
                {
                    StatusMessage = $"User created but failed to assign role: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}";
                    return;
                }
            }

            // Audit: log user creation event (fire-and-forget safe — failures won't interrupt)
            try
            {
                var authState = await AuthStateTask;
                var actingUserName = authState.User.Identity?.Name;
                string? actingUserId = null;
                if (actingUserName is not null)
                {
                    var actingUser = await UserManager.FindByNameAsync(actingUserName);
                    actingUserId = actingUser?.Id;
                }

                await AuditLogService.LogAsync(
                    actingUserId,
                    AuditActionType.UserCreated,
                    AuditEntityType.User,
                    user.Id,
                    user.DisplayName ?? user.UserName ?? "",
                    $"User '{user.DisplayName ?? user.UserName}' created.");
            }
            catch { /* audit failures must not interrupt the primary operation */ }

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
