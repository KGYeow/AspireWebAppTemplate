using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Domain.Constants;
using AspireWebAppTemplate.Application.Contracts.Users;
using AspireWebAppTemplate.Web.Abstractions;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Settings;

/// <summary>
/// Regional settings sub-page allowing authenticated users to configure their timezone
/// and date/time format preferences. Changes are saved instantly to the API.
/// </summary>
/// <remarks>
/// The timezone autocomplete handles legacy/alias timezone IDs that may not appear in
/// the standard list by building display entries from <see cref="TimeZoneInfo"/>.
/// Date/time format uses a select dropdown with predefined format options.
/// Both fields use a property setter pattern that captures the previous value before saving,
/// enabling automatic rollback on API failure.
/// </remarks>
[Authorize]
public partial class Regional : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for auth operations including preference updates.
    /// </summary>
    [Inject] private ApiAuthService AuthService { get; set; } = default!;

    /// <summary>
    /// Provides timezone list and conversion utilities for the timezone autocomplete.
    /// </summary>
    [Inject] private ITimeZoneHelper TimeZoneService { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions (e.g., redirecting to InvalidUser on load failure).
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording warnings and errors during preference saves.
    /// </summary>
    [Inject] private ILogger<Regional> Logger { get; set; } = default!;

    /// <summary>
    /// Circuit-scoped datetime context that caches the user's timezone and format preferences.
    /// Re-initialized after save so other pages pick up the new values immediately.
    /// </summary>
    [Inject] private IUserTimeZoneContext UserTimeZone { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// Whether the page is loading initial data.
    /// </summary>
    private bool _isLoading = true;

    /// <summary>
    /// The current timezone ID value bound to the autocomplete component.
    /// </summary>
    private string? _timeZoneValue;

    /// <summary>
    /// The previous timezone value before the latest change, used for rollback on save failure.
    /// </summary>
    private string? _previousTimeZoneValue;

    /// <summary>
    /// Timezone preference property with instant-save on change.
    /// Captures the previous value before firing the async save operation.
    /// </summary>
    private string? TimeZoneValue
    {
        get => _timeZoneValue;
        set
        {
            if (_timeZoneValue == value) return;
            _previousTimeZoneValue = _timeZoneValue;
            _timeZoneValue = value;
            _ = SaveTimeZoneAsync(value);
        }
    }

    /// <summary>
    /// The current date/time format string value bound to the select component.
    /// </summary>
    private string? _dateTimeFormatValue;

    /// <summary>
    /// The previous date/time format value before the latest change, used for rollback on save failure.
    /// </summary>
    private string? _previousDateTimeFormatValue;

    /// <summary>
    /// Date/time format preference property with instant-save on change.
    /// Captures the previous value before firing the async save operation.
    /// </summary>
    private string? DateTimeFormatValue
    {
        get => _dateTimeFormatValue;
        set
        {
            if (_dateTimeFormatValue == value) return;
            _previousDateTimeFormatValue = _dateTimeFormatValue;
            _dateTimeFormatValue = value;
            _ = SaveDateTimeFormatAsync(value);
        }
    }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads the current user's regional preferences from the API on page initialization.
    /// Redirects to InvalidUser page if the user cannot be resolved.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        var result = await AuthService.GetCurrentUserAsync();

        if (!result.Succeeded || result.Data is null)
        {
            NavigationManager.NavigateTo("Account/InvalidUser", forceLoad: true);
            return;
        }

        _timeZoneValue = result.Data.TimeZoneId;
        _dateTimeFormatValue = result.Data.DateTimeFormat;

        _isLoading = false;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Persists the timezone preference to the API.
    /// Reverts to the previous value and shows an error message on failure.
    /// </summary>
    /// <param name="timeZoneId">The IANA timezone ID to save, or empty string to clear.</param>
    private async Task SaveTimeZoneAsync(string? timeZoneId)
    {
        try
        {
            var result = await AuthService.UpdatePreferencesAsync(new UpdatePreferencesRequest { TimeZoneId = timeZoneId ?? "" });
            if (!result.Succeeded)
            {
                _timeZoneValue = _previousTimeZoneValue;
                Snackbar.Add("Failed to save time zone. Please try again.", MudBlazor.Severity.Error);
            }
            else
            {
                // Refresh the circuit-scoped datetime context so other pages use the new timezone immediately
                await UserTimeZone.InitializeAsync(string.Empty);
            }
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error saving TimeZone preference.");
            _timeZoneValue = _previousTimeZoneValue;
            Snackbar.Add("Failed to save time zone. Please try again.", MudBlazor.Severity.Error);
            StateHasChanged();
        }
    }

    /// <summary>
    /// Persists the date/time format preference to the API.
    /// Reverts to the previous value and shows an error message on failure.
    /// </summary>
    /// <param name="format">The date/time format string to save, or empty string to clear.</param>
    private async Task SaveDateTimeFormatAsync(string? format)
    {
        try
        {
            var result = await AuthService.UpdatePreferencesAsync(new UpdatePreferencesRequest { DateTimeFormat = format ?? "" });
            if (!result.Succeeded)
            {
                _dateTimeFormatValue = _previousDateTimeFormatValue;
                Snackbar.Add("Failed to save date/time format. Please try again.", MudBlazor.Severity.Error);
            }
            else
            {
                // Refresh the circuit-scoped datetime context so other pages use the new format immediately
                await UserTimeZone.InitializeAsync(string.Empty);
            }
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error saving DateTimeFormat preference.");
            _dateTimeFormatValue = _previousDateTimeFormatValue;
            Snackbar.Add("Failed to save date/time format. Please try again.", MudBlazor.Severity.Error);
            StateHasChanged();
        }
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Provides the timezone autocomplete search function.
    /// Filters the full timezone list by matching display name or ID against the search value.
    /// Also handles alias/legacy timezone IDs that may not appear in the standard list.
    /// </summary>
    /// <param name="value">The search text entered by the user.</param>
    /// <param name="token">Cancellation token (unused but required by MudAutocomplete).</param>
    /// <returns>Filtered enumerable of timezone IDs matching the search criteria.</returns>
    private Task<IEnumerable<string>> SearchTimeZones(string value, CancellationToken token)
    {
        var allTimeZones = TimeZoneService.GetAllTimeZones();
        IEnumerable<TimeZoneOption> source = allTimeZones;

        // If the user's saved timezone is a legacy/alias not in the standard list,
        // include it as an extra option so it appears in the dropdown
        if (!string.IsNullOrEmpty(_timeZoneValue)
            && !allTimeZones.Any(tz => tz.Id == _timeZoneValue))
        {
            var extra = BuildTimeZoneOptionForAlias(_timeZoneValue);
            if (extra is not null)
                source = allTimeZones.Append(extra);
        }

        if (string.IsNullOrWhiteSpace(value))
            return Task.FromResult(source.Select(tz => tz.Id));

        var filtered = source
            .Where(tz => tz.DisplayName.Contains(value, StringComparison.OrdinalIgnoreCase)
                      || tz.Id.Contains(value, StringComparison.OrdinalIgnoreCase))
            .Select(tz => tz.Id);

        return Task.FromResult(filtered);
    }

    /// <summary>
    /// Builds a <see cref="TimeZoneOption"/> for a timezone ID that may be a legacy alias
    /// not present in the standard list. Returns null if the ID is completely invalid.
    /// </summary>
    /// <param name="id">The timezone ID to resolve.</param>
    /// <returns>A constructed timezone option, or null if the ID cannot be resolved.</returns>
    private static TimeZoneOption? BuildTimeZoneOptionForAlias(string id)
    {
        try
        {
            var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(id);
            var offset = tzInfo.BaseUtcOffset;
            var sign = offset < TimeSpan.Zero ? "-" : "+";
            var abs = offset.Duration();
            var displayName = $"(UTC{sign}{abs.Hours:D2}:{abs.Minutes:D2}) {id}";
            return new TimeZoneOption(id, displayName, offset);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// Maps a date/time format string to its human-readable label for display purposes.
    /// Returns the raw format string if no matching label is defined.
    /// </summary>
    /// <param name="format">The format string to look up.</param>
    /// <returns>A human-readable label describing the format.</returns>
    private static string GetFormatLabel(string? format) => format switch
    {
        null or "" => DateTimeFormatDefaults.Label,
        "yyyy-MM-dd HH:mm" => "ISO (2026-11-03 14:30)",
        "yyyy-MM-dd HH:mm:ss" => "ISO with seconds (2026-11-03 14:30:00)",
        "dd/MM/yyyy HH:mm" => "Day first (03/11/2026 14:30)",
        "MM/dd/yyyy h:mm tt" => "US (11/03/2026 2:30 PM)",
        "dd-MM-yyyy HH:mm" => "Day first with dashes (03-11-2026 14:30)",
        "dd.MM.yyyy HH:mm" => "European with dots (03.11.2026 14:30)",
        "yyyy/MM/dd HH:mm" => "East Asian (2026/11/03 14:30)",
        "dd MMM yyyy HH:mm" => "Short month (03 Nov 2026 14:30)",
        "d MMMM yyyy HH:mm" => "Long month (3 November 2026 14:30)",
        "d MMM yyyy h:mm tt" => "Short month 12h (3 Nov 2026 2:30 PM)",
        "MMMM d, yyyy h:mm tt" => "US long (November 3, 2026 2:30 PM)",
        _ => format
    };

    /// <summary>
    /// Converts a timezone ID to its display name for the autocomplete's display function.
    /// Falls back to building a display name from <see cref="TimeZoneInfo"/> if not found
    /// in the standard list, or returns the raw ID if resolution fails entirely.
    /// </summary>
    /// <param name="id">The timezone ID to display.</param>
    /// <returns>A formatted display string like "(UTC+08:00) Asia/Singapore".</returns>
    private string TimeZoneToString(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return string.Empty;

        var allTimeZones = TimeZoneService.GetAllTimeZones();
        var match = allTimeZones.FirstOrDefault(tz => tz.Id == id);
        if (match is not null)
            return match.DisplayName;

        try
        {
            var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(id);
            var offset = tzInfo.BaseUtcOffset;
            var sign = offset < TimeSpan.Zero ? "-" : "+";
            var abs = offset.Duration();
            return $"(UTC{sign}{abs.Hours:D2}:{abs.Minutes:D2}) {id}";
        }
        catch (TimeZoneNotFoundException)
        {
            return id;
        }
    }

    #endregion
}
