namespace AspireWebAppTemplate.Application.Abstractions;

/// <summary>
/// Provides timezone conversion utilities for displaying UTC dates
/// in the user's configured timezone.
/// </summary>
public interface ITimeZoneHelper
{
    /// <summary>
    /// Converts a UTC DateTime to the specified IANA timezone.
    /// </summary>
    DateTime ConvertFromUtc(DateTime utcDateTime, string ianaTimeZoneId);

    /// <summary>
    /// Converts a UTC DateTime to the specified IANA timezone,
    /// returning null if the input is null.
    /// </summary>
    DateTime? ConvertFromUtc(DateTime? utcDateTime, string ianaTimeZoneId);

    /// <summary>
    /// Converts a local DateTime in the specified IANA timezone to UTC.
    /// </summary>
    DateTime ConvertToUtc(DateTime localDateTime, string ianaTimeZoneId);

    /// <summary>
    /// Converts a local DateTime in the specified IANA timezone to UTC,
    /// returning null if the input is null.
    /// </summary>
    DateTime? ConvertToUtc(DateTime? localDateTime, string ianaTimeZoneId);

    /// <summary>
    /// Gets all available IANA timezone identifiers with their UTC offsets.
    /// </summary>
    IReadOnlyList<TimeZoneOption> GetAllTimeZones();
}

/// <summary>
/// Represents a timezone option for display in dropdowns.
/// </summary>
public record TimeZoneOption(string Id, string DisplayName, TimeSpan BaseUtcOffset);
