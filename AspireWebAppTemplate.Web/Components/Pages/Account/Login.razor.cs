using System.ComponentModel.DataAnnotations;
using BlazorWebAppTemplate.Abstractions;
using BlazorWebAppTemplate.Contracts;
using BlazorWebAppTemplate.Core.Domain.Enums;
using BlazorWebAppTemplate.Options;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorWebAppTemplate.Components.Account.Pages;

/// <summary>
/// Handles the login page using <c>InteractiveServer</c> render mode,
/// enabling full Blazor interactivity (password toggle, loading state, inline errors).
/// Delegates credential validation to <see cref="ILoginService"/> (local) or
/// <see cref="ILdapLoginService"/> (LDAP) based on configuration.
/// </summary>
public partial class Login : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// The local login service responsible for credential validation and token generation.
    /// </summary>
    [Inject] private ILoginService LoginService { get; set; } = default!;

    // [LDAP] LDAP login service — remove if LDAP is not needed
    /// <summary>
    /// The LDAP login service for corporate directory authentication.
    /// </summary>
    [Inject] private ILdapLoginService LdapLoginService { get; set; } = default!;

    // [LDAP] LDAP settings — remove if LDAP is not needed
    /// <summary>
    /// LDAP configuration options to determine if LDAP is enabled.
    /// </summary>
    [Inject] private IOptions<LdapSettings> LdapOptions { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording sign-in events and warnings.
    /// </summary>
    [Inject] private ILogger<Login> Logger { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions used to redirect after successful sign-in.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Audit log service for recording login success and failure events.
    /// </summary>
    [Inject] private IAuditLogService AuditLogService { get; set; } = default!;

    /// <summary>
    /// HTTP context accessor used to retrieve the client's IP address for audit logging.
    /// In InteractiveServer mode, the HttpContext may be null after the initial connection;
    /// IP address is captured on a best-effort basis.
    /// </summary>
    [Inject] private IHttpContextAccessor HttpContextAccessor { get; set; } = default!;

    #endregion

    #region Query Parameters

    /// <summary>
    /// Optional return URL supplied via query string.
    /// The user is redirected here after a successful sign-in.
    /// Defaults to the app root if not provided.
    /// </summary>
    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    #endregion

    #region State

    /// <summary>
    /// The form input model bound to the login form fields.
    /// </summary>
    private InputModel Input { get; set; } = new();

    /// <summary>
    /// Drives the <c>EditForm</c> validation context.
    /// Initialized from <see cref="Input"/> in <see cref="OnInitialized"/>.
    /// </summary>
    private EditContext editContext = default!;

    /// <summary>
    /// Error message displayed in the alert banner when sign-in fails.
    /// </summary>
    protected string? ErrorMessage { get; private set; }

    /// <summary>
    /// Controls the submit button's disabled state and loading spinner
    /// to prevent duplicate submissions during async sign-in.
    /// </summary>
    protected bool IsBusy { get; private set; }

    /// <summary>
    /// Toggles the password field between masked and plain-text display.
    /// </summary>
    protected bool IsPasswordVisible { get; private set; }

    // [LDAP] Placeholder text changes when LDAP is enabled — remove if LDAP is not needed
    /// <summary>
    /// The placeholder text for the identifier field. Shows "Email or NTID" when LDAP
    /// is enabled, "Email" otherwise.
    /// </summary>
    protected string IdentifierPlaceholder => LdapOptions.Value.Enabled ? "Email or NTID" : "Email";

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
    /// Processes the login form on valid submission.
    /// When LDAP is enabled, tries LDAP first, then falls back to local Identity.
    /// When LDAP is disabled, uses local Identity only.
    /// Records audit log entries for successful and failed login attempts.
    /// </summary>
    protected async Task LoginUser()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            LoginResult result;

            // Capture the client IP address for audit logging.
            // In InteractiveServer mode, HttpContext may be null after the initial render;
            // IP is recorded on a best-effort basis.
            var ipAddress = HttpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

            // [LDAP] Try LDAP first when enabled, fall back to local — remove this block if LDAP is not needed
            if (LdapOptions.Value.Enabled)
            {
                result = await LdapLoginService.ValidateAndGenerateTokenAsync(
                    Input.Email, Input.Password, Input.RememberMe, ReturnUrl ?? "/");

                // If LDAP fails (user not in directory), fall back to local Identity
                if (!result.Succeeded && !result.IsDeactivated && !result.IsLockedOut)
                {
                    result = await LoginService.ValidateAndGenerateTokenAsync(
                        Input.Email, Input.Password, Input.RememberMe, ReturnUrl ?? "/");
                }
            }
            else
            {
                // Local Identity login only (default template behavior)
                result = await LoginService.ValidateAndGenerateTokenAsync(
                    Input.Email, Input.Password, Input.RememberMe, ReturnUrl ?? "/");
            }

            if (result.Succeeded)
            {
                // Audit: record successful login with user ID and source IP address
                await AuditLogService.LogAsync(
                    userId: result.UserId,
                    actionType: AuditActionType.LoginSuccess,
                    entityType: AuditEntityType.User,
                    entityId: result.UserId ?? Input.Email,
                    entityName: Input.Email,
                    description: $"User '{Input.Email}' logged in successfully.",
                    ipAddress: ipAddress);

                NavigationManager.NavigateTo($"Account/PerformLogin?token={result.Token}", forceLoad: true);
            }
            else if (result.RequiresTwoFactor)
            {
                NavigationManager.NavigateTo(
                    $"Account/LoginWith2fa?returnUrl={ReturnUrl}&rememberMe={Input.RememberMe}",
                    forceLoad: true);
            }
            else if (result.IsLockedOut)
            {
                // Audit: record failed login due to lockout
                await AuditLogService.LogAsync(
                    userId: null,
                    actionType: AuditActionType.LoginFailed,
                    entityType: AuditEntityType.System,
                    entityId: Input.Email,
                    entityName: Input.Email,
                    description: $"Login failed for '{Input.Email}': account is locked out.",
                    ipAddress: ipAddress);

                NavigationManager.NavigateTo("Account/Lockout");
            }
            else if (result.IsDeactivated)
            {
                // Audit: record failed login due to deactivated account
                await AuditLogService.LogAsync(
                    userId: null,
                    actionType: AuditActionType.LoginFailed,
                    entityType: AuditEntityType.System,
                    entityId: Input.Email,
                    entityName: Input.Email,
                    description: $"Login failed for '{Input.Email}': account is deactivated.",
                    ipAddress: ipAddress);

                NavigationManager.NavigateTo("Account/AccessDenied?reason=inactive", forceLoad: true);
            }
            else
            {
                // Audit: record failed login (invalid credentials or other failure)
                await AuditLogService.LogAsync(
                    userId: null,
                    actionType: AuditActionType.LoginFailed,
                    entityType: AuditEntityType.System,
                    entityId: Input.Email,
                    entityName: Input.Email,
                    description: $"Login failed for '{Input.Email}': invalid credentials.",
                    ipAddress: ipAddress);

                ErrorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during login for {Identifier}.", Input.Email);
            ErrorMessage = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Toggles the password field visibility between masked and plain text.
    /// </summary>
    protected void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;

    /// <summary>
    /// Clears the current error message (e.g., when the user dismisses the alert).
    /// </summary>
    protected void ClearError() => ErrorMessage = null;

    #endregion

    #region Input Model

    /// <summary>
    /// Form model bound to the login form fields.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>
        /// The user's email address or NTID used as the login identifier.
        /// </summary>
        [Required]
        //[EmailAddress]
        [Display(Name = "Email or NTID")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// The user's account password.
        /// </summary>
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Whether the authentication cookie should persist beyond the current browser session.
        /// </summary>
        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    #endregion
}
