namespace BlazorWebAppTemplate.Core.Common.Defaults;

/// <summary>
/// System-wide default values for date/time formatting.
/// Used as the fallback when a user has not explicitly chosen a format.
/// </summary>
public static class DateTimeFormatDefaults
{
    /// <summary>
    /// The default date/time format string used when the user's preference is null.
    /// ISO 8601-inspired format: "2026-11-03 14:30".
    /// </summary>
    public const string Format = "yyyy-MM-dd HH:mm";

    /// <summary>
    /// The display label shown in the UI when the default format is active.
    /// Includes a sample formatted date so users understand what the format looks like.
    /// </summary>
    public const string Label = "Default (2026-11-03 14:30)";
}
