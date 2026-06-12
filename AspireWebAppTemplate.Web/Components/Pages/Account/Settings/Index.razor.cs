using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.Core.Application.Abstractions;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Common.Defaults;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Contracts.Auth;
using AspireWebAppTemplate.Core.Contracts.AuditLog;
using AspireWebAppTemplate.Core.Contracts.Roles;
using AspireWebAppTemplate.Core.Contracts.Users;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Settings;

/// <summary>
/// Settings page allowing authenticated users to view and edit their
/// preferences (Time Zone, Locale, Date/Time Format) and appearance (Theme).
/// All fields use instant-save on value change — no Save button or EditForm.
/// Delegates persistence to the API via <see cref="ApiAuthService"/>.
/// </summary>
[Authorize]
public partial class Index : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for auth operations including preference updates.
    /// </summary>
    [Inject] private ApiAuthService AuthService { get; set; } = default!;

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

    #region State

    /// <summary>
    /// Status message displayed after save.
    /// </summary>
    protected string? StatusMessage { get; set; }

    private ThemePreference _themeValue;
    private ThemePreference _previousThemeValue;

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

    private string? _timeZoneValue;
    private string? _previousTimeZoneValue;

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

    private string? _dateTimeFormatValue;
    private string? _previousDateTimeFormatValue;

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

    protected override async Task OnInitializedAsync()
    {
        var result = await AuthService.GetCurrentUserAsync();

        if (!result.Succeeded || result.Data is null)
        {
            NavigationManager.NavigateTo("Account/InvalidUser", forceLoad: true);
            return;
        }

        var user = result.Data;
        _timeZoneValue = user.TimeZoneId;
        _dateTimeFormatValue = user.DateTimeFormat;
        _themeValue = user.Theme;
    }

    #endregion

    #region Event Handlers

    private async Task SaveThemeAsync(ThemePreference theme)
    {
        try
        {
            var result = await AuthService.UpdatePreferencesAsync(new UpdatePreferencesRequest { Theme = theme });
            if (!result.Succeeded)
            {
                _themeValue = _previousThemeValue;
                StatusMessage = "Error: Theme change failed, please try again.";
                StateHasChanged();
                return;
            }

            var themeModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/theme.js");
            var systemPrefersDark = await themeModule.InvokeAsync<bool>("getSystemPrefersDark");
            ThemeState.SetThemePreference(theme, systemPrefersDark);
        }
        catch (Exception)
        {
            _themeValue = _previousThemeValue;
            StatusMessage = "Error: Theme change failed, please try again.";
            StateHasChanged();
        }
    }

    private async Task SaveTimeZoneAsync(string? timeZoneId)
    {
        try
        {
            var result = await AuthService.UpdatePreferencesAsync(new UpdatePreferencesRequest { TimeZoneId = timeZoneId ?? "" });
            if (!result.Succeeded)
            {
                _timeZoneValue = _previousTimeZoneValue;
                StatusMessage = "Error: Save failed, please try again.";
            }
            else
            {
                StatusMessage = "Time zone updated.";
            }
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error saving TimeZone preference.");
            _timeZoneValue = _previousTimeZoneValue;
            StatusMessage = "Error: Save failed, please try again.";
            StateHasChanged();
        }
    }

    private async Task SaveDateTimeFormatAsync(string? format)
    {
        try
        {
            var result = await AuthService.UpdatePreferencesAsync(new UpdatePreferencesRequest { DateTimeFormat = format ?? "" });
            if (!result.Succeeded)
            {
                _dateTimeFormatValue = _previousDateTimeFormatValue;
                StatusMessage = "Error: Save failed, please try again.";
            }
            else
            {
                StatusMessage = "Date/time format updated.";
            }
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error saving DateTimeFormat preference.");
            _dateTimeFormatValue = _previousDateTimeFormatValue;
            StatusMessage = "Error: Save failed, please try again.";
            StateHasChanged();
        }
    }

    #endregion

    #region Helpers

    private Task<IEnumerable<string>> SearchTimeZones(string value, CancellationToken token)
    {
        var allTimeZones = TimeZoneService.GetAllTimeZones();
        IEnumerable<TimeZoneOption> source = allTimeZones;

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
