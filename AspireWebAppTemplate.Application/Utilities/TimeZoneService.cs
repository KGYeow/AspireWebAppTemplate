using AspireWebAppTemplate.Application.Abstractions;

namespace AspireWebAppTemplate.Application.Utilities;

/// <summary>
/// Provides timezone conversion and display utilities using IANA identifiers.
/// </summary>
/// <remarks>
/// <para>
/// On Windows, <see cref="TimeZoneInfo.GetSystemTimeZones()"/> returns Windows-style IDs
/// (e.g., "Pacific Standard Time"). This service converts them to IANA identifiers
/// (e.g., "America/Los_Angeles") using <see cref="TimeZoneInfo.TryConvertWindowsIdToIanaId"/>
/// for consistent cross-platform display matching industry conventions (GitHub, GitLab, etc.).
/// </para>
/// <para>
/// The timezone list is lazily initialized and cached for the lifetime of the service.
/// Register as a singleton to avoid rebuilding the list on every request.
/// </para>
/// </remarks>
public sealed class TimeZoneService : ITimeZoneService
{
    /// <summary>
    /// Lazily-built, cached list of all available IANA time zones.
    /// Thread-safe by default via <see cref="Lazy{T}"/>.
    /// </summary>
    private readonly Lazy<IReadOnlyList<TimeZoneOption>> _allTimeZones = new(BuildTimeZoneList);

    /// <inheritdoc />
    public DateTime ConvertFromUtc(DateTime utcDateTime, string ianaTimeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZoneId);
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), timeZone);
    }

    /// <inheritdoc />
    public DateTime? ConvertFromUtc(DateTime? utcDateTime, string ianaTimeZoneId)
    {
        if (utcDateTime is null)
            return null;

        return ConvertFromUtc(utcDateTime.Value, ianaTimeZoneId);
    }

    /// <inheritdoc />
    public DateTime ConvertToUtc(DateTime localDateTime, string ianaTimeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZoneId);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), timeZone);
    }

    /// <inheritdoc />
    public DateTime? ConvertToUtc(DateTime? localDateTime, string ianaTimeZoneId)
    {
        if (localDateTime is null)
            return null;

        return ConvertToUtc(localDateTime.Value, ianaTimeZoneId);
    }

    /// <inheritdoc />
    public IReadOnlyList<TimeZoneOption> GetAllTimeZones() => _allTimeZones.Value;

    /// <summary>
    /// Builds the canonical list of IANA time zones from the system's available time zones.
    /// </summary>
    /// <returns>
    /// A read-only list of <see cref="TimeZoneOption"/> entries ordered by UTC offset ascending,
    /// then alphabetically by IANA identifier.
    /// </returns>
    /// <remarks>
    /// <para>
    /// On Windows, system time zones use Windows-style IDs. Each ID is converted to its
    /// IANA equivalent via <see cref="TimeZoneInfo.TryConvertWindowsIdToIanaId"/>.
    /// If conversion fails (unlikely), the original ID is preserved as a fallback.
    /// </para>
    /// <para>
    /// Multiple Windows IDs can map to the same IANA ID (e.g., "Singapore Standard Time"
    /// and "Malay Peninsula Standard Time" both map to "Asia/Singapore"), so duplicates
    /// are removed via <c>DistinctBy</c>.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<TimeZoneOption> BuildTimeZoneList()
    {
        return TimeZoneInfo.GetSystemTimeZones()
            .Select(tz =>
            {
                // Convert Windows ID to IANA ID for consistent cross-platform display.
                // On Linux/macOS, IDs are already IANA and TryConvert returns false,
                // so the original ID is used as-is.
                var ianaId = TimeZoneInfo.TryConvertWindowsIdToIanaId(tz.Id, out var converted)
                    ? converted
                    : tz.Id;

                return new TimeZoneOption(
                    Id: ianaId,
                    DisplayName: FormatDisplayName(ianaId, tz.BaseUtcOffset),
                    BaseUtcOffset: tz.BaseUtcOffset);
            })
            .DistinctBy(tz => tz.Id)
            .OrderBy(tz => tz.BaseUtcOffset)
            .ThenBy(tz => tz.Id, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Formats a time zone display name in the pattern "(UTC±HH:mm) IANA_Identifier".
    /// </summary>
    /// <param name="ianaId">The IANA time zone identifier (e.g., "America/New_York").</param>
    /// <param name="offset">The base UTC offset for the time zone.</param>
    /// <returns>A formatted string like "(UTC-05:00) America/New_York".</returns>
    private static string FormatDisplayName(string ianaId, TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var absOffset = offset.Duration();
        return $"(UTC{sign}{absOffset.Hours:D2}:{absOffset.Minutes:D2}) {ianaId}";
    }
}
