namespace AspireWebAppTemplate.Domain.Constants;

/// <summary>
/// Defines the set of system page paths that are always accessible regardless of
/// role-based permissions. These pages are essential for authentication flow,
/// error handling, and user self-service — they must never be blocked by the permission system.
/// </summary>
/// <remarks>
/// <para>
/// Used by: <c>PagePermissionContext</c>, <c>PagePermissionHandler</c>, <c>NavMenu</c>,
/// <c>PagePermissions</c> admin page, and <c>SeedData</c>.
/// </para>
/// <para>
/// A <c>HashSet&lt;string&gt;</c> with <see cref="StringComparer.OrdinalIgnoreCase"/> is used
/// for O(1) case-insensitive membership checks. This cannot be declared as <c>const</c> because
/// C# only supports compile-time constants for primitive types; <c>static readonly</c> is the
/// idiomatic equivalent for reference-type constants.
/// </para>
/// </remarks>
public static class SystemPageDefaults
{
    /// <summary>
    /// The set of page paths that bypass all permission checks.
    /// Includes authentication flow pages, error pages, user self-service pages
    /// (profile, settings, account management, notifications), and the 404 page.
    /// </summary>
    public static readonly IReadOnlySet<string> Paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Authentication flow
        "/Account/Login",
        "/Account/Register",
        "/Account/ForgotPassword",
        "/Account/ForgotPasswordConfirmation",
        "/Account/ResetPassword",
        "/Account/ResetPasswordConfirmation",
        "/Account/InvalidPasswordReset",
        "/Account/ConfirmEmail",
        "/Account/ConfirmEmailChange",
        "/Account/ResendEmailConfirmation",
        "/Account/RegisterConfirmation",
        "/Account/LoginWith2fa",
        "/Account/LoginWithRecoveryCode",
        "/Account/ExternalLogin",
        "/Account/Lockout",
        "/Account/InvalidUser",
        "/Account/PerformLogin",

        // Error and access pages
        "/Error",
        "/Account/AccessDenied",
        "/not-found",

        // User self-service: profile and settings
        "/account/profile",
        "/account/settings",
        "/account/settings/profile",
        "/account/settings/appearance",
        "/account/settings/regional",
        "/account/settings/notifications",

        // User self-service: notifications
        "/account/notifications",

        // User self-service: account management
        "/Account/Manage",
        "/Account/Manage/Email",
        "/Account/Manage/ChangePassword",
        "/Account/Manage/SetPassword",
        "/Account/Manage/TwoFactorAuthentication",
        "/Account/Manage/EnableAuthenticator",
        "/Account/Manage/ResetAuthenticator",
        "/Account/Manage/GenerateRecoveryCodes",
        "/Account/Manage/Disable2fa",
        "/Account/Manage/Passkeys",
        "/Account/Manage/RenamePasskey",
        "/Account/Manage/PersonalData",
        "/Account/Manage/DeletePersonalData",
        "/Account/Manage/ExternalLogins"
    };
}
