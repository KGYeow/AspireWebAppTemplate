using AspireWebAppTemplate.Core.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace AspireWebAppTemplate.ApiService.Data.Entities;

/// <summary>
/// Represents an application user record stored by ASP.NET Core Identity.
/// Inherits all standard Identity fields (UserName, Email, Lockout, etc.) and
/// adds profile, organization, and auditing properties used by the application.
/// </summary>
/// <remarks>
/// After adding or changing properties here, run an EF Core migration to update
/// the underlying AspNetUsers table so the schema remains in sync with the model.
/// See: Microsoft docs on customizing the Identity model and EF Core migrations.
/// </remarks>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Gets or sets the user's given name.
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Gets or sets the user's family/surname.
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Gets or sets the preferred display name used in UI (e.g., dashboards, comments).
    /// </summary>
    /// <remarks>
    /// If not set, UI can fall back to FirstName + LastName or UserName.
    /// </remarks>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the URL pointing to the user's avatar/profile image.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Gets or sets the user�s locale/culture (e.g., "en-US" or "ms-MY") for formatting.
    /// </summary>
    public string? Locale { get; set; }

    /// <summary>
    /// Gets or sets the IANA or Windows time zone identifier (e.g., "Asia/Kuala_Lumpur" or "Singapore Standard Time").
    /// </summary>
    public string? TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the user's preferred date/time display format string
    /// (e.g., "yyyy-MM-dd HH:mm", "dd/MM/yyyy HH:mm").
    /// When null, the system uses the default format "yyyy-MM-dd HH:mm".
    /// </summary>
    public string? DateTimeFormat { get; set; }

    /// <summary>
    /// Gets or sets the user's preferred UI theme.
    /// Defaults to <see cref="ThemePreference.System"/> (follow OS preference).
    /// </summary>
    public ThemePreference Theme { get; set; } = ThemePreference.System;

    /// <summary>
    /// Gets or sets an employee or staff number used by the organization.
    /// </summary>
    public string? EmployeeNumber { get; set; }

    /// <summary>
    /// Gets or sets the organizational department (e.g., "IT", "Finance").
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// Gets or sets the job title (e.g., "Contract IT Programmer").
    /// </summary>
    public string? JobTitle { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier for multi-tenant scenarios.
    /// </summary>
    /// <remarks>
    /// Null indicates single-tenant or non-partitioned contexts.
    /// </remarks>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Gets or sets an application-level active flag.
    /// When false, the account should be treated as deactivated, even if not locked out.
    /// </summary>
    /// <remarks>
    /// This complements Identity's built-in lockout features; your authorization logic
    /// can check this flag to short-circuit sign-in or resource access.
    /// </remarks>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the authentication source that created this user account.
    /// Defaults to <see cref="AuthSource.Local"/> for locally registered users.
    /// </summary>
    /// <remarks>
    /// [LDAP] Users provisioned from Active Directory have <see cref="AuthSource.LDAP"/>.
    /// Used to filter LDAP sync operations to only LDAP-sourced users.
    /// </remarks>
    public AuthSource AuthSource { get; set; } = AuthSource.Local;

    /// <summary>
    /// Gets or sets the timestamp (UTC) of the last successful sign-in for this user.
    /// </summary>
    public DateTime? LastLoginUtc { get; set; }

    /// <summary>
    /// Gets or sets the timestamp (UTC) of the last password change for this user.
    /// </summary>
    public DateTime? LastPasswordChangeUtc { get; set; }

    /// <summary>
    /// Gets or sets the timestamp (UTC) when the user record was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp (UTC) when the user record was last updated.
    /// </summary>
    public DateTime? UpdatedUtc { get; set; }

    /// <summary>
    /// Gets or sets whether real-time pop-up notifications are shown to the user.
    /// When false, notifications still appear in the notification bell/list but no
    /// in-page pop-up is displayed on arrival. Defaults to true.
    /// </summary>
    public bool NotificationPopupsEnabled { get; set; } = true;
}