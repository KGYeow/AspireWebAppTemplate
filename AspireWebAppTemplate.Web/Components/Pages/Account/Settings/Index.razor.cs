using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.Core.Application.Abstractions;
using AspireWebAppTemplate.Core.Common.Defaults;
using AspireWebAppTemplate.Core.Contracts.Notifications;
using AspireWebAppTemplate.Core.Contracts.Users;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Settings;

/// <summary>
/// Settings page allowing authenticated users to view and edit their
/// preferences (Time Zone, Date/Time Format) and appearance (Theme).
/// All fields use instant-save on value change — no Save button or EditForm.
/// Delegates persistence to the API via <see cref="ApiAuthService"/>.
/// </summary>
/// <remarks>
/// Each preference field uses a property setter pattern that captures the previous
/// value before saving, enabling automatic rollback on API failure.
/// </remarks>
[Authorize]
public partial class Index : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for auth operations including preference updates.
    /// </summary>
    [Inject] private ApiAuthService AuthService { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions (e.g., redirecting to InvalidUser on load failure).
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording warnings and errors during preference saves.
    /// </summary>
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    /// <summary>
    /// Provides timezone list and conversion utilities for the timezone autocomplete.
    /// </summary>
    [Inject] private ITimeZoneService TimeZoneService { get; set; } = default!;

    /// <summary>
    /// Scoped theme context for notifying the layout of theme changes in real time.
    /// </summary>
    [Inject] private IThemeContext ThemeState { get; set; } = default!;

    /// <summary>
    /// JavaScript runtime for detecting OS dark mode preference when applying theme changes.
    /// </summary>
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>
    /// Typed HttpClient service for notification preference operations (get/update).
    /// </summary>
    [Inject] private ApiNotificationService NotificationService { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// Status message displayed after a save operation (success or error).
    /// </summary>
    protected string? StatusMessage { get; set; }

    /// <summary>
    /// Whether the page is loading initial data. Controls the <see cref="UI.Components.Shared.PageContent"/> wrapper.
    /// </summary>
    private bool _isLoading = true;

    /// <summary>
    /// The current theme preference value bound to the PillToggle component.
    /// </summary>
    private ThemePreference _themeValue;

    /// <summary>
    /// The previous theme value before the latest change, used for rollback on save failure.
    /// </summary>
    private ThemePreference _previousThemeValue;

    /// <summary>
    /// Theme preference property with instant-save on change.
    /// Captures the previous value before firing the async save operation.
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

    /// <summary>
    /// The list of notification preferences for all categories, loaded from the API.
    /// </summary>
    private List<NotificationPreferenceDto> _notificationPreferences = [];

    /// <summary>
    /// Whether the notification preferences section is loading its data.
    /// </summary>
    private bool _isLoadingPreferences = true;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads the current user's preferences from the API on page initialization.
    /// Redirects to InvalidUser page if the user cannot be resolved.
    /// Also loads notification delivery preferences for the Notifications section.
    /// </summary>
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
        _isLoading = false;

        // Load notification preferences in parallel (non-blocking for the main page content)
        await LoadNotificationPreferencesAsync();
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Persists the theme preference to the API and updates the layout's theme context.
    /// Reverts to the previous value and shows an error message on failure.
    /// </summary>
    /// <param name="theme">The new theme preference to save.</param>
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

            // Apply the theme change immediately by detecting OS preference and notifying the layout
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

    /// <summary>
    /// Handles the In-App notification toggle change for a specific category.
    /// Immediately persists the new value via the API. On failure, reverts the toggle
    /// to its previous state and displays a Snackbar error.
    /// </summary>
    /// <param name="pref">The preference DTO being toggled.</param>
    /// <param name="newValue">The new In-App enabled value.</param>
    private async Task HandleInAppToggle(NotificationPreferenceDto pref, bool newValue)
    {
        var previousValue = pref.InAppEnabled;
        pref.InAppEnabled = newValue;

        var request = new UpdateNotificationPreferenceRequest
        {
            Category = pref.Category,
            InAppEnabled = newValue,
            EmailEnabled = pref.EmailEnabled
        };

        try
        {
            var result = await NotificationService.UpdatePreferenceAsync(request);
            if (!result.Succeeded)
            {
                pref.InAppEnabled = previousValue;
                Snackbar.Add("Failed to save notification preference. Please try again.", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error saving In-App notification preference for {Category}.", pref.Category);
            pref.InAppEnabled = previousValue;
            Snackbar.Add("Failed to save notification preference. Please try again.", Severity.Error);
        }
    }

    /// <summary>
    /// Handles the Email notification toggle change for a specific category.
    /// Immediately persists the new value via the API. On failure, reverts the toggle
    /// to its previous state and displays a Snackbar error.
    /// </summary>
    /// <param name="pref">The preference DTO being toggled.</param>
    /// <param name="newValue">The new Email enabled value.</param>
    private async Task HandleEmailToggle(NotificationPreferenceDto pref, bool newValue)
    {
        var previousValue = pref.EmailEnabled;
        pref.EmailEnabled = newValue;

        var request = new UpdateNotificationPreferenceRequest
        {
            Category = pref.Category,
            InAppEnabled = pref.InAppEnabled,
            EmailEnabled = newValue
        };

        try
        {
            var result = await NotificationService.UpdatePreferenceAsync(request);
            if (!result.Succeeded)
            {
                pref.EmailEnabled = previousValue;
                Snackbar.Add("Failed to save notification preference. Please try again.", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error saving Email notification preference for {Category}.", pref.Category);
            pref.EmailEnabled = previousValue;
            Snackbar.Add("Failed to save notification preference. Please try again.", Severity.Error);
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Loads notification delivery preferences for all categories from the API.
    /// On failure, logs a warning and leaves the list empty (non-blocking).
    /// </summary>
    private async Task LoadNotificationPreferencesAsync()
    {
        try
        {
            var result = await NotificationService.GetPreferencesAsync();
            if (result.Succeeded && result.Data is not null)
            {
                _notificationPreferences = result.Data;
            }
            else
            {
                Logger.LogWarning("Failed to load notification preferences.");
                Snackbar.Add("Failed to load notification preferences.", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error loading notification preferences.");
            Snackbar.Add("Failed to load notification preferences.", Severity.Error);
        }
        finally
        {
            _isLoadingPreferences = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Converts a <see cref="NotificationCategory"/> enum value to a human-readable display name.
    /// Inserts spaces before uppercase letters in multi-word enum names (e.g., "UserManagement" → "User Management").
    /// </summary>
    /// <param name="category">The notification category to format.</param>
    /// <returns>A human-readable category name.</returns>
    private static string FormatCategoryName(NotificationCategory category)
    {
        return category switch
        {
            NotificationCategory.UserManagement => "User Management",
            _ => category.ToString()
        };
    }

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
