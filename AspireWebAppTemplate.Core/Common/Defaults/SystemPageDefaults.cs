namespace AspireWebAppTemplate.Core.Common.Defaults;

/// <summary>
/// Defines the set of system page paths that are always accessible regardless of
/// role-based permissions. These pages are essential for authentication flow and
/// error handling — they must never be blocked by the permission system.
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
    /// Includes authentication flow pages (Login, Register, ForgotPassword, ResetPassword,
    /// PerformLogin) and error/access-denied pages.
    /// </summary>
    public static readonly IReadOnlySet<string> Paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "/Account/Login",
        "/Account/Register",
        "/Account/AccessDenied",
        "/Error",
        "/Account/ForgotPassword",
        "/Account/ResetPassword",
        "/Account/PerformLogin"
    };
}
