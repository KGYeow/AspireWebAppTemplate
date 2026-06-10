using Microsoft.AspNetCore.Components;

namespace BlazorWebAppTemplate.Components.Account.Pages;

/// <summary>
/// Displays a context-specific access denied message based on the <c>reason</c> query parameter.
/// </summary>
/// <remarks>
/// <para>
/// This page is used across the application for various access-denied scenarios:
/// </para>
/// <list type="bullet">
///   <item><description><c>?reason=inactive</c> — deactivated account attempted login</description></item>
///   <item><description><c>?reason=missingrole</c> — authenticated user lacks required role</description></item>
///   <item><description><c>?reason=policy</c> — organization policy restriction</description></item>
///   <item><description><c>?reason=expired</c> — session expired or changed</description></item>
///   <item><description><c>?reason=notauthorized</c> — general authorization failure</description></item>
///   <item><description>No reason — default access denied message</description></item>
/// </list>
/// </remarks>
public partial class AccessDenied : ComponentBase
{
    /// <summary>
    /// The reason code supplied via the <c>reason</c> query parameter.
    /// Drives the context-specific message displayed to the user.
    /// </summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "reason")]
    public string? Reason { get; set; }

    /// <summary>
    /// Returns a user-friendly message based on the <see cref="Reason"/> value.
    /// </summary>
    protected string Message => (Reason?.Trim().ToLowerInvariant()) switch
    {
        "inactive"      => "Your account is inactive. Please contact your administrator.",
        "missingrole"   => "Your account is signed in but lacks the required permissions to access this resource.",
        "policy"        => "Your access is restricted due to organization policies.",
        "expired"       => "Your session may have expired or changed. Please try signing in again.",
        "notauthorized" => "This resource is restricted and your account isn't authorized to access it.",
        null            => "You do not have access to this resource.",
        _               => "You don't have permission to view this page."
    };
}
