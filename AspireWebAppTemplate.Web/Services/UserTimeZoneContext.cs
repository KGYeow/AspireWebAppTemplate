using BlazorWebAppTemplate.Abstractions;
using BlazorWebAppTemplate.Core.Application.Abstractions;
using BlazorWebAppTemplate.Core.Common.Defaults;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace BlazorWebAppTemplate.Services;

/// <summary>
/// Scoped service that holds the current user's time zone preference and provides
/// datetime formatting for the duration of a Blazor Server circuit.
/// </summary>
/// <remarks>
/// <para>
/// This service is registered as <b>scoped</b> — one instance per SignalR circuit.
/// It is initialized once by <see cref="Components.Layout.MainLayout"/> on first render
/// via <see cref="InitializeAsync"/>, which loads the user's <c>TimeZoneId</c> from the database.
/// </para>
/// <para>
/// After initialization, all formatting calls use the cached time zone ID without
/// additional database lookups, making them safe to call frequently in Razor markup.
/// </para>
/// <para>
/// If the user has no time zone configured (null or empty <c>TimeZoneId</c>),
/// formatting falls back to displaying the raw UTC value with a "UTC" suffix.
/// </para>
/// </remarks>
public sealed class UserTimeZoneContext : IUserTimeZoneContext
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITimeZoneService _timeZoneService;

    /// <summary>
    /// Initializes a new instance of <see cref="UserTimeZoneContext"/>.
    /// </summary>
    /// <param name="userManager">The Identity user manager for loading user preferences.</param>
    /// <param name="timeZoneService">The singleton time zone service for UTC-to-local conversion.</param>
    public UserTimeZoneContext(UserManager<ApplicationUser> userManager, ITimeZoneService timeZoneService)
    {
        _userManager = userManager;
        _timeZoneService = timeZoneService;
    }

    /// <inheritdoc />
    public string? TimeZoneId { get; private set; }

    /// <inheritdoc />
    public string? DateTimeFormat { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Called once per circuit from <see cref="Components.Layout.MainLayout.OnAfterRenderAsync"/>.
    /// Subsequent calls overwrite the cached <see cref="TimeZoneId"/> and <see cref="DateTimeFormat"/>
    /// (e.g., after the user changes their preferences in Settings and the layout re-initializes).
    /// </remarks>
    public async Task InitializeAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        TimeZoneId = user?.TimeZoneId;
        DateTimeFormat = user?.DateTimeFormat;
    }

    /// <inheritdoc />
    public string FormatDateTime(DateTime utcDateTime, string? format = null)
    {
        var effectiveFormat = format ?? DateTimeFormat ?? DateTimeFormatDefaults.Format;

        // No time zone configured — display raw UTC with suffix for clarity
        if (string.IsNullOrEmpty(TimeZoneId))
            return $"{utcDateTime.ToString(effectiveFormat)} UTC";

        // Convert UTC to the user's local time zone and format
        var local = _timeZoneService.ConvertFromUtc(utcDateTime, TimeZoneId);
        return local.ToString(effectiveFormat);
    }

    /// <inheritdoc />
    public string FormatDateTime(DateTime? utcDateTime, string? format = null, string fallback = "-")
    {
        if (utcDateTime is null) return fallback;
        return FormatDateTime(utcDateTime.Value, format);
    }

    /// <inheritdoc />
    public string FormatDateTime(DateTimeOffset? utcDateTimeOffset, string? format = null, string fallback = "-")
    {
        if (utcDateTimeOffset is null) return fallback;
        return FormatDateTime(utcDateTimeOffset.Value.UtcDateTime, format);
    }
}
