using AspireWebAppTemplate.Core.Domain.Enums;

namespace AspireWebAppTemplate.Core.Contracts.Users;

/// <summary>
/// Data transfer object representing a user account with all profile, security, and preference fields.
/// Returned by user management and profile API endpoints.
/// </summary>
public sealed class UserDto
{
    /// <summary>
    /// The unique identifier of the user.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// The user's login username.
    /// </summary>
    public string UserName { get; set; } = "";

    /// <summary>
    /// The user's display name shown throughout the application.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// The user's email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// The user's first/given name.
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// The user's last/family name.
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// The user's job title.
    /// </summary>
    public string? JobTitle { get; set; }

    /// <summary>
    /// The user's department.
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// The user's employee number.
    /// </summary>
    public string? EmployeeNumber { get; set; }

    /// <summary>
    /// The user's phone number.
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Whether the user account is active and can sign in.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// The authentication source for this account (e.g., "Local", "LDAP").
    /// </summary>
    public string AuthSource { get; set; } = "Local";

    /// <summary>
    /// The roles currently assigned to the user.
    /// </summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>
    /// The UTC timestamp when the account was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// The UTC timestamp when the account was last updated.
    /// </summary>
    public DateTime? UpdatedUtc { get; set; }

    // Account security

    /// <summary>
    /// Whether the user's email address has been confirmed.
    /// </summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>
    /// Whether two-factor authentication is enabled for the account.
    /// </summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>
    /// Whether the account can be locked out after failed login attempts.
    /// </summary>
    public bool LockoutEnabled { get; set; }

    /// <summary>
    /// The UTC timestamp when the current lockout period expires, if locked.
    /// </summary>
    public DateTimeOffset? LockoutEnd { get; set; }

    /// <summary>
    /// The number of consecutive failed login attempts since last successful login.
    /// </summary>
    public int AccessFailedCount { get; set; }

    /// <summary>
    /// The UTC timestamp of the user's most recent successful login.
    /// </summary>
    public DateTime? LastLoginUtc { get; set; }

    /// <summary>
    /// The UTC timestamp of the user's last password change.
    /// </summary>
    public DateTime? LastPasswordChangeUtc { get; set; }

    // Profile extras

    /// <summary>
    /// The URL of the user's avatar image.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// The user's preferred locale/language code.
    /// </summary>
    public string? Locale { get; set; }

    /// <summary>
    /// The tenant identifier for multi-tenant deployments.
    /// </summary>
    public Guid? TenantId { get; set; }

    // Preferences

    /// <summary>
    /// The user's preferred UI theme (Light, Dark, or System).
    /// </summary>
    public ThemePreference Theme { get; set; } = ThemePreference.System;

    /// <summary>
    /// The IANA time zone identifier for the user's preferred timezone.
    /// </summary>
    public string? TimeZoneId { get; set; }

    /// <summary>
    /// The user's preferred date/time format string.
    /// </summary>
    public string? DateTimeFormat { get; set; }

    /// <summary>
    /// Whether real-time pop-up notifications are enabled for this user.
    /// When false, notifications still appear in the bell/list but no pop-up is shown.
    /// </summary>
    public bool NotificationPopupsEnabled { get; set; } = true;
}
