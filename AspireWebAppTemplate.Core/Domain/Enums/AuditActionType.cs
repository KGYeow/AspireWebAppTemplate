namespace AspireWebAppTemplate.Core.Domain.Enums;

/// <summary>
/// Defines the categories of auditable actions within the application.
/// Stored as a PascalCase string in the database via <c>HasConversion&lt;string&gt;()</c>.
/// </summary>
public enum AuditActionType
{
    /// <summary>
    /// A new user account was created in the system.
    /// </summary>
    UserCreated,

    /// <summary>
    /// An existing user's profile or account details were modified.
    /// </summary>
    UserUpdated,

    /// <summary>
    /// A user account was permanently deleted from the system.
    /// </summary>
    UserDeleted,

    /// <summary>
    /// A previously deactivated user account was re-enabled.
    /// </summary>
    UserActivated,

    /// <summary>
    /// A user account was disabled, preventing the user from logging in.
    /// </summary>
    UserDeactivated,

    /// <summary>
    /// A new application role was created.
    /// </summary>
    RoleCreated,

    /// <summary>
    /// An existing role's properties (e.g., name, permissions) were modified.
    /// </summary>
    RoleUpdated,

    /// <summary>
    /// A role was permanently deleted from the system.
    /// </summary>
    RoleDeleted,

    /// <summary>
    /// A role was assigned to a user, granting them additional permissions.
    /// </summary>
    RoleAssigned,

    /// <summary>
    /// A role was removed from a user, revoking the associated permissions.
    /// </summary>
    RoleUnassigned,

    /// <summary>
    /// A user successfully authenticated and established a session.
    /// </summary>
    LoginSuccess,

    /// <summary>
    /// An authentication attempt failed due to invalid credentials or account lockout.
    /// </summary>
    LoginFailed,

    /// <summary>
    /// A user explicitly ended their session by logging out.
    /// </summary>
    LogoutSuccess,

    /// <summary>
    /// Application-level settings were modified by an administrator.
    /// </summary>
    SettingsChanged,

    /// <summary>
    /// A user changed their account password.
    /// </summary>
    PasswordChanged,

    /// <summary>
    /// A user updated their own profile information (e.g., display name, preferences).
    /// </summary>
    ProfileUpdated
}
