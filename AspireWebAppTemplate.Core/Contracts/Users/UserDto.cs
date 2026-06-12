using AspireWebAppTemplate.Core.Domain.Enums;

namespace AspireWebAppTemplate.Core.Contracts.Users;

public sealed class UserDto
{
    public string Id { get; set; } = "";
    public string UserName { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? JobTitle { get; set; }
    public string? Department { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public string AuthSource { get; set; } = "Local";
    public List<string> Roles { get; set; } = [];
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }

    // Account security
    public bool EmailConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool LockoutEnabled { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public DateTime? LastPasswordChangeUtc { get; set; }

    // Profile extras
    public string? AvatarUrl { get; set; }
    public string? Locale { get; set; }
    public Guid? TenantId { get; set; }

    // Preferences
    public ThemePreference Theme { get; set; } = ThemePreference.System;
    public string? TimeZoneId { get; set; }
    public string? DateTimeFormat { get; set; }
}
