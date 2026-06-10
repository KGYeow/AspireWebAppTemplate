using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BlazorWebAppTemplate.Components.Account.Pages.Manage;

/// <summary>
/// Enable authenticator app page using <c>InteractiveServer</c> render mode.
/// Guides the user through setting up a TOTP authenticator app and verifying it.
/// </summary>
public partial class EnableAuthenticator : ComponentBase
{
    #region Constants

    /// <summary>
    /// The TOTP URI format used by authenticator apps.
    /// </summary>
    private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

    #endregion

    #region Injected Services

    /// <summary>
    /// Manages user accounts. Used for authenticator key and 2FA operations.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// URL encoder for building the authenticator URI.
    /// </summary>
    [Inject] private UrlEncoder UrlEncoder { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording 2FA enablement events.
    /// </summary>
    [Inject] private ILogger<EnableAuthenticator> Logger { get; set; } = default!;

    #endregion

    #region Cascading Parameters

    /// <summary>
    /// Provides the current authentication state to resolve the user.
    /// </summary>
    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// The current user.
    /// </summary>
    private ApplicationUser? user;

    /// <summary>
    /// The form input model.
    /// </summary>
    private InputModel Input { get; set; } = new();

    /// <summary>
    /// Drives the <c>EditForm</c> validation context.
    /// </summary>
    private EditContext editContext = default!;

    /// <summary>
    /// The formatted shared key displayed to the user.
    /// </summary>
    protected string? SharedKey { get; private set; }

    /// <summary>
    /// The TOTP URI for QR code generation.
    /// </summary>
    protected string? AuthenticatorUri { get; private set; }

    /// <summary>
    /// Recovery codes generated after successful 2FA enablement.
    /// </summary>
    protected string[]? RecoveryCodes { get; private set; }

    /// <summary>
    /// Status message displayed after verification.
    /// </summary>
    protected string? StatusMessage { get; set; }

    /// <summary>
    /// Controls the submit button's disabled state and loading spinner.
    /// </summary>
    protected bool IsBusy { get; set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the edit context synchronously.
    /// </summary>
    protected override void OnInitialized()
    {
        editContext = new EditContext(Input);
    }

    /// <summary>
    /// Loads the current user and generates the shared key and QR code URI.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateTask;
        user = await UserManager.GetUserAsync(authState.User);
        if (user is null)
        {
            NavigationManager.NavigateTo("Account/InvalidUser", forceLoad: true);
            return;
        }

        await LoadSharedKeyAndQrCodeUriAsync(user);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Verifies the authenticator code and enables 2FA on valid submission.
    /// </summary>
    protected async Task OnValidSubmitAsync()
    {
        if (IsBusy || user is null) return;
        IsBusy = true;
        StatusMessage = null;

        try
        {
            var verificationCode = Input.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
            var is2faTokenValid = await UserManager.VerifyTwoFactorTokenAsync(
                user, UserManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);

            if (!is2faTokenValid)
            {
                StatusMessage = "Error: Verification code is invalid.";
                return;
            }

            await UserManager.SetTwoFactorEnabledAsync(user, true);
            var userId = await UserManager.GetUserIdAsync(user);
            Logger.LogInformation("User with ID '{UserId}' has enabled 2FA with an authenticator app.", userId);

            if (await UserManager.CountRecoveryCodesAsync(user) == 0)
            {
                var codes = await UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
                RecoveryCodes = codes?.ToArray();
            }
            else
            {
                NavigationManager.NavigateTo("Account/Manage/TwoFactorAuthentication", forceLoad: true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Loads the authenticator shared key and generates the QR code URI.
    /// </summary>
    /// <param name="user">The current user.</param>
    private async ValueTask LoadSharedKeyAndQrCodeUriAsync(ApplicationUser user)
    {
        var unformattedKey = await UserManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await UserManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await UserManager.GetAuthenticatorKeyAsync(user);
        }

        SharedKey = FormatKey(unformattedKey!);
        var email = await UserManager.GetEmailAsync(user);
        AuthenticatorUri = string.Format(
            CultureInfo.InvariantCulture,
            AuthenticatorUriFormat,
            UrlEncoder.Encode("Microsoft.AspNetCore.Identity.UI"),
            UrlEncoder.Encode(email!),
            unformattedKey);
    }

    /// <summary>
    /// Formats the authenticator key with spaces every 4 characters for readability.
    /// </summary>
    /// <param name="unformattedKey">The raw authenticator key.</param>
    /// <returns>The formatted key in lowercase with spaces.</returns>
    private static string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        int currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }
        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }
        return result.ToString().ToLowerInvariant();
    }

    #endregion

    #region Input Model

    /// <summary>
    /// Form model for the verification code input.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>
        /// The TOTP verification code from the authenticator app.
        /// </summary>
        [Required]
        [StringLength(7, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Text)]
        [Display(Name = "Verification Code")]
        public string Code { get; set; } = "";
    }

    #endregion
}
