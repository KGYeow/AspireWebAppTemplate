using Microsoft.AspNetCore.Components;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Auth;

/// <summary>
/// Displays a confirmation message after a password reset request, prompting
/// the user to check their email for the reset link.
/// </summary>
/// <remarks>
/// The message is deliberately vague ("If an account exists...") to avoid
/// revealing whether the email address is registered in the system.
/// </remarks>
public partial class ForgotPasswordConfirmation : ComponentBase
{
}
