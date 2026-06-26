using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.Core.Contracts.Users;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Settings;

/// <summary>
/// Appearance settings sub-page allowing authenticated users to choose their preferred theme.
/// Provides a PillToggle with Light, Dark, and System options. Changes are saved instantly
/// to the API and applied in real time via <see cref="IThemeContext"/>.
/// </summary>
[Authorize]
public partial class Appearance : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for auth operations including preference updates.
    /// </summary>
    [Inject] private ApiAuthService AuthService { get; set; } = default!;

    /// <summary>
    /// Scoped theme context for notifying the layout of theme changes in real time.
    /// </summary>
    [Inject] private IThemeContext ThemeState { get; set; } = default!;

    /// <summary>
    /// JavaScript runtime for detecting OS dark mode preference when applying theme changes.
    /// </summary>
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions (e.g., redirecting to InvalidUser on load failure).
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording warnings and errors during theme saves.
    /// </summary>
    [Inject] private ILogger<Appearance> Logger { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// Whether the page is loading initial data.
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

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads the current user's theme preference from the API on page initialization.
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

        _themeValue = result.Data.Theme;
        _isLoading = false;
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
                Snackbar.Add("Failed to change theme. Please try again.", MudBlazor.Severity.Error);
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
            Snackbar.Add("Failed to change theme. Please try again.", MudBlazor.Severity.Error);
            StateHasChanged();
        }
    }

    #endregion
}
