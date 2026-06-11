using Microsoft.AspNetCore.Components;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Auth;

/// <summary>
/// Displays the account lockout page when a user has been locked out
/// due to multiple failed login attempts.
/// </summary>
/// <remarks>
/// This page is navigated to from <c>Login.razor.cs</c> when
/// <c>LoginResult.IsLockedOut</c> is <c>true</c>.
/// </remarks>
public partial class Lockout : ComponentBase
{
}
