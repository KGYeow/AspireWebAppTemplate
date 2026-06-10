using BlazorWebAppTemplate.Core.Domain.Enums;

namespace BlazorWebAppTemplate.Abstractions;

/// <summary>
/// Provides per-circuit theme state management for Blazor Server.
/// Holds the current <c>IsDarkMode</c> value and notifies subscribers (e.g., the layout)
/// when the theme changes so the UI can re-render with the correct palette.
/// </summary>
/// <remarks>
/// Registered as <b>scoped</b> — one instance per SignalR circuit (user session).
/// The Settings page calls <see cref="SetThemePreference"/> after persisting the user's choice,
/// and the layout subscribes to <see cref="OnChange"/> to pick up the new value.
/// </remarks>
public interface IThemeStateService
{
    /// <summary>
    /// Gets the current dark mode state. When <c>true</c>, the UI renders with the dark palette.
    /// </summary>
    bool IsDarkMode { get; }

    /// <summary>
    /// Raised when <see cref="IsDarkMode"/> changes. Subscribers should call <c>StateHasChanged</c>.
    /// </summary>
    event Action? OnChange;

    /// <summary>
    /// Directly sets the dark mode flag and notifies subscribers.
    /// </summary>
    /// <param name="isDark">Whether dark mode should be active.</param>
    void SetDarkMode(bool isDark);

    /// <summary>
    /// Resolves the effective dark mode state from a <see cref="ThemePreference"/> value
    /// and the OS-level preference, then notifies subscribers.
    /// </summary>
    /// <param name="preference">The user's stored theme preference.</param>
    /// <param name="systemPrefersDark">
    /// Whether the operating system / browser currently prefers dark mode.
    /// Only used when <paramref name="preference"/> is <see cref="ThemePreference.System"/>.
    /// </param>
    void SetThemePreference(ThemePreference preference, bool systemPrefersDark);
}
