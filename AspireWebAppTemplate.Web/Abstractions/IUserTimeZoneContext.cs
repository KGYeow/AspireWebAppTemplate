namespace AspireWebAppTemplate.Web.Abstractions;

/// <summary>
/// Provides user-aware datetime formatting using the current user's configured time zone.
/// </summary>
/// <remarks>
/// <para>
/// Registered as <b>scoped</b> (one instance per Blazor Server circuit). The user's time zone
/// is loaded once via <see cref="InitializeAsync"/> at circuit startup and cached for the session.
/// </para>
/// <para>
/// Pages inject this service and call <see cref="FormatDateTime(DateTime, string?)"/>
/// without needing to resolve the user's time zone themselves.
/// </para>
/// </remarks>
public interface IUserTimeZoneContext
{
    /// <summary>
    /// The current user's IANA time zone ID. Null if not configured or not yet initialized.
    /// </summary>
    string? TimeZoneId { get; }

    /// <summary>
    /// The current user's preferred date/time format string.
    /// Null means use the default "yyyy-MM-dd HH:mm".
    /// </summary>
    string? DateTimeFormat { get; }

    /// <summary>
    /// Raised when the timezone context is initialized or updated.
    /// Pages can subscribe to trigger data reloads that depend on timezone conversion.
    /// </summary>
    event Action? OnInitialized;

    /// <summary>
    /// Initializes the context by loading the current user's time zone preference.
    /// Called once per circuit (typically from the root layout or auth state handler).
    /// </summary>
    /// <param name="userId">The authenticated user's ID.</param>
    Task InitializeAsync(string userId);

    /// <summary>
    /// Formats a UTC <see cref="DateTime"/> in the user's configured time zone.
    /// Falls back to UTC display with "UTC" suffix if no time zone is configured.
    /// </summary>
    /// <param name="utcDateTime">The UTC datetime to format.</param>
    /// <param name="format">The datetime format string. When null, uses the stored <see cref="DateTimeFormat"/> or "yyyy-MM-dd HH:mm".</param>
    /// <returns>The formatted datetime string.</returns>
    string FormatDateTime(DateTime utcDateTime, string? format = null);

    /// <summary>
    /// Formats a nullable UTC <see cref="DateTime"/> in the user's configured time zone.
    /// Returns the fallback string if the value is null.
    /// </summary>
    /// <param name="utcDateTime">The nullable UTC datetime to format.</param>
    /// <param name="format">The datetime format string. When null, uses the stored <see cref="DateTimeFormat"/> or "yyyy-MM-dd HH:mm".</param>
    /// <param name="fallback">The string to return when the value is null. Defaults to "-".</param>
    /// <returns>The formatted datetime string or the fallback.</returns>
    string FormatDateTime(DateTime? utcDateTime, string? format = null, string fallback = "-");

    /// <summary>
    /// Formats a nullable UTC <see cref="DateTimeOffset"/> in the user's configured time zone.
    /// Returns the fallback string if the value is null.
    /// </summary>
    /// <param name="utcDateTimeOffset">The nullable UTC DateTimeOffset to format.</param>
    /// <param name="format">The datetime format string. When null, uses the stored <see cref="DateTimeFormat"/> or "yyyy-MM-dd HH:mm".</param>
    /// <param name="fallback">The string to return when the value is null. Defaults to "-".</param>
    /// <returns>The formatted datetime string or the fallback.</returns>
    string FormatDateTime(DateTimeOffset? utcDateTimeOffset, string? format = null, string fallback = "-");
}
