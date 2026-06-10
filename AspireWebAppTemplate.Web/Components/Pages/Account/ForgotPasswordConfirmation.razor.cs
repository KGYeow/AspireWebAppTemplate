using Microsoft.AspNetCore.Components;

namespace BlazorWebAppTemplate.Components.Account.Pages;

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
