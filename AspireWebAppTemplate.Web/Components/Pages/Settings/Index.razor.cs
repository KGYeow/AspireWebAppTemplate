using BlazorWebAppTemplate.Abstractions;
using BlazorWebAppTemplate.Core.Application.Abstractions;
using BlazorWebAppTemplate.Core.Common.Defaults;
using BlazorWebAppTemplate.Core.Domain.Enums;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace BlazorWebAppTemplate.Components.Pages.Settings;

/// <summary>
/// Settings page allowing authenticated users to view and edit their
/// preferences (Time Zone, Locale, Date/Time Format) and appearance (Theme).
/// All fields use instant-save on value change — no Save button or EditForm.
/// Uses <see cref="AuthenticationStateProvider"/> to resolve the current user
/// since <c>HttpContext</c> is not available on a SignalR circuit.
/// </summary>
[Authorize]
public partial class Index : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages user accounts.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording events.
    /// </summary>
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    /// <summary>
    /// Provides timezone conversion and display utilities.
    /// </summary>
    [Inject] private ITimeZoneService TimeZoneService { get; set; } = default!;

    /// <summary>
    /// Scoped theme state service for notifying the layout of theme changes.
    /// </summary>
    [Inject] private IThemeStateService ThemeState { get; set; } = default!;

    /// <summary>
    /// JavaScript runtime for detecting OS dark mode preference.
    /// </summary>
    [Inject] private IJSRuntime JS { get; set; } = default!;

    #endregion

    #region Cascading Parameters

    /// <summary>
    /// Provides the current authentication state to resolve the user
    /// without depending on <c>HttpContext</c>.
    /// </summary>
    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// The current user entity.
    /// </summary>
    private ApplicationUser? User { get; set; }

    /// <summary>
    /// Status message displayed after save.
    /// </summary>
    protected string? StatusMessage { get; set; }

    /// <summary>
    /// Backing field for the theme preference instant-save toggle.
    /// </summary>
    private ThemePreference _themeValue;
    /// <summary>
    /// Stores the previous theme value for revert-on-failure logic.
    /// </summary>
    private ThemePreference _previousThemeValue;

    /// <summary>
    /// Gets or sets the user's theme preference.
    /// The setter triggers an immediate async save when the value changes.
    /// Stores the previous value for revert-on-failure logic.
    /// </summary>
    private ThemePreference ThemeValue
    {
        get => _themeValue;
        set
        {
            if (_themeValue == value) return;
            _previousThemeValue = _themeValue;
            _themeValue = value;
            _ = SaveThemeAsync(value);
        }
    }

    // Time Zone state
    private string? _timeZoneValue;
    private string? _previousTimeZoneValue;

    /// <summary>
    /// Gets or sets the user's time zone preference.
    /// The setter triggers an immediate async save when the value changes.
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

    // Date/Time Format state
    private string? _dateTimeFormatValue;
    private string? _previousDateTimeFormatValue;

    /// <summary>
    /// Gets or sets the user's date/time format preference.
    /// The setter triggers an immediate async save when the value changes.
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
    /// Loads the current user's preferences data using <see cref="AuthenticationState"/>.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateTask;
        User = await UserManager.GetUserAsync(authState.User);

        if (User is null)
        {
            NavigationManager.NavigateTo("Account/InvalidUser", forceLoad: true);
            return;
        }

        _timeZoneValue = User.TimeZoneId;
        _dateTimeFormatValue = User.DateTimeFormat;
        _themeValue = User.Theme;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Persists the selected theme preference immediately (instant-save pattern).
    /// Called by the <see cref="ThemeValue"/> property setter.
    /// Implements optimistic UI: updates User.Theme immediately, and on failure
    /// reverts both _themeValue and User.Theme to the previous value and shows an error alert.
    /// On success, notifies the <see cref="IThemeStateService"/> so the layout updates immediately.
    /// </summary>
    private async Task SaveThemeAsync(ThemePreference theme)
    {
        if (User is null) return;
        User.Theme = theme;
        try
        {
            await UserManager.UpdateAsync(User);

            // Detect system preference for the "System" option
            var themeModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/theme.js");
            var systemPrefersDark = await themeModule.InvokeAsync<bool>("getSystemPrefersDark");

            // Notify the layout to apply the new theme immediately
            ThemeState.SetThemePreference(theme, systemPrefersDark);
        }
        catch (Exception)
        {
            // Revert on failure
            _themeValue = _previousThemeValue;
            User.Theme = _previousThemeValue;
            StatusMessage = "Error: Theme change failed, please try again.";
            StateHasChanged();
        }
    }

    /// <summary>
    /// Persists the selected time zone preference immediately (instant-save pattern).
    /// Called by the <see cref="TimeZoneValue"/> property setter.
    /// Implements optimistic UI: updates User.TimeZoneId immediately, and on failure
    /// reverts both _timeZoneValue and User.TimeZoneId to the previous value and shows an error alert.
    /// </summary>
    private async Task SaveTimeZoneAsync(string? timeZoneId)
    {
        if (User is null) return;
        User.TimeZoneId = timeZoneId;
        try
        {
            var result = await UserManager.UpdateAsync(User);
            if (!result.Succeeded)
            {
                _timeZoneValue = _previousTimeZoneValue;
                User.TimeZoneId = _previousTimeZoneValue;
                StatusMessage = "Error: Save failed, please try again.";
            }
            else
            {
                StatusMessage = "Time zone updated.";
            }
            StateHasChanged();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Logger.LogWarning(ex, "Concurrency conflict saving TimeZone for user {UserId}.", User.Id);
            _timeZoneValue = _previousTimeZoneValue;
            User.TimeZoneId = _previousTimeZoneValue;
            StatusMessage = "Error: Profile was modified elsewhere, please reload.";
            StateHasChanged();
        }
        catch (DbUpdateException ex)
        {
            Logger.LogError(ex, "Database error saving TimeZone for user {UserId}.", User.Id);
            _timeZoneValue = _previousTimeZoneValue;
            User.TimeZoneId = _previousTimeZoneValue;
            StatusMessage = "Error: Save failed, please try again.";
            StateHasChanged();
        }
        catch (TaskCanceledException ex)
        {
            Logger.LogWarning(ex, "Save timed out for TimeZone for user {UserId}.", User.Id);
            _timeZoneValue = _previousTimeZoneValue;
            User.TimeZoneId = _previousTimeZoneValue;
            StatusMessage = "Error: Save failed, please try again.";
            StateHasChanged();
        }
    }

    /// <summary>
    /// Persists the selected date/time format preference immediately (instant-save pattern).
    /// Called by the <see cref="DateTimeFormatValue"/> property setter.
    /// Implements optimistic UI: updates User.DateTimeFormat immediately, and on failure
    /// reverts both _dateTimeFormatValue and User.DateTimeFormat to the previous value and shows an error alert.
    /// </summary>
    private async Task SaveDateTimeFormatAsync(string? format)
    {
        if (User is null) return;
        User.DateTimeFormat = format;
        try
        {
            var result = await UserManager.UpdateAsync(User);
            if (!result.Succeeded)
            {
                _dateTimeFormatValue = _previousDateTimeFormatValue;
                User.DateTimeFormat = _previousDateTimeFormatValue;
                StatusMessage = "Error: Save failed, please try again.";
            }
            else
            {
                StatusMessage = "Date/time format updated.";
            }
            StateHasChanged();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Logger.LogWarning(ex, "Concurrency conflict saving DateTimeFormat for user {UserId}.", User.Id);
            _dateTimeFormatValue = _previousDateTimeFormatValue;
            User.DateTimeFormat = _previousDateTimeFormatValue;
            StatusMessage = "Error: Profile was modified elsewhere, please reload.";
            StateHasChanged();
        }
        catch (DbUpdateException ex)
        {
            Logger.LogError(ex, "Database error saving DateTimeFormat for user {UserId}.", User.Id);
            _dateTimeFormatValue = _previousDateTimeFormatValue;
            User.DateTimeFormat = _previousDateTimeFormatValue;
            StatusMessage = "Error: Save failed, please try again.";
            StateHasChanged();
        }
        catch (TaskCanceledException ex)
        {
            Logger.LogWarning(ex, "Save timed out for DateTimeFormat for user {UserId}.", User.Id);
            _dateTimeFormatValue = _previousDateTimeFormatValue;
            User.DateTimeFormat = _previousDateTimeFormatValue;
            StatusMessage = "Error: Save failed, please try again.";
            StateHasChanged();
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Searches timezones for the MudAutocomplete component.
    /// Returns timezone IDs filtered by the search text (case-insensitive match on DisplayName or Id).
    /// Injects the user's saved TimeZoneId into results if it's not in the canonical list
    /// (handles IANA aliases like "Asia/Kuala_Lumpur").
    /// </summary>
    private Task<IEnumerable<string>> SearchTimeZones(string value, CancellationToken token)
    {
        var allTimeZones = TimeZoneService.GetAllTimeZones();
        IEnumerable<TimeZoneOption> source = allTimeZones;

        // Inject the user's saved TimeZoneId if it's not in the canonical list
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
    /// Attempts to build a TimeZoneOption for an alias ID by resolving it via
    /// TimeZoneInfo.FindSystemTimeZoneById(). Returns null if the ID is unresolvable.
    /// </summary>
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
    /// Maps a date/time format string to a user-friendly display label.
    /// Returns "Default (2026-05-28 14:30)" for null/empty values.
    /// Returns the raw format string if no predefined label exists.
    /// </summary>
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
    /// Converts a timezone IANA identifier to its display name for the autocomplete's ToStringFunc.
    /// Handles canonical IDs, resolvable aliases, and unresolvable IDs gracefully.
    /// </summary>
    private string TimeZoneToString(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return string.Empty;

        // First: check the canonical list
        var allTimeZones = TimeZoneService.GetAllTimeZones();
        var match = allTimeZones.FirstOrDefault(tz => tz.Id == id);
        if (match is not null)
            return match.DisplayName;

        // Second: attempt alias resolution via FindSystemTimeZoneById
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
            // Graceful fallback: return raw ID
            return id;
        }
    }

    #endregion
}
