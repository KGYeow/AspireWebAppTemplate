using AspireWebAppTemplate.Core.Domain.Enums;

namespace AspireWebAppTemplate.Core.Contracts;

/// <summary>
/// Request body for updating the current user's display preferences (theme, timezone, date format).
/// </summary>
public sealed class UpdatePreferencesRequest
{
    public ThemePreference? Theme { get; set; }
    public string? TimeZoneId { get; set; }
    public string? DateTimeFormat { get; set; }
}
