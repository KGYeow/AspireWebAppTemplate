using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Domain.Enums;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// Scoped service that holds the current dark mode state for a Blazor Server circuit.
/// Acts as a lightweight pub/sub mechanism between the Settings page (publisher)
/// and the MainLayout (subscriber) so theme changes apply instantly without a full page reload.
/// </summary>
/// <remarks>
/// Because Blazor Server uses scoped DI per SignalR circuit, each user session
/// gets its own instance — no cross-user interference.
/// </remarks>
public sealed class ThemeContext : IThemeContext
{
    /// <inheritdoc />
    public bool IsDarkMode { get; private set; }

    /// <inheritdoc />
    public event Action? OnChange;

    /// <inheritdoc />
    public void SetDarkMode(bool isDark)
    {
        if (IsDarkMode == isDark) return;
        IsDarkMode = isDark;
        OnChange?.Invoke();
    }

    /// <inheritdoc />
    public void SetThemePreference(ThemePreference preference, bool systemPrefersDark)
    {
        var isDark = preference switch
        {
            ThemePreference.Dark => true,
            ThemePreference.Light => false,
            ThemePreference.System => systemPrefersDark,
            _ => false
        };

        SetDarkMode(isDark);
    }
}
