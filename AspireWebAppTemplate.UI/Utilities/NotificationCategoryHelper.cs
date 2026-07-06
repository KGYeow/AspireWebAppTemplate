namespace AspireWebAppTemplate.UI.Utilities;

/// <summary>
/// Provides centralized icon and color CSS class mapping for notification categories.
/// Returns framework-agnostic strings (Material Symbols icon names, MudBlazor utility classes)
/// so consumers in any project layer can display category visuals consistently.
/// Accepts category as a string and performs case-insensitive matching.
/// </summary>
public static class NotificationCategoryHelper
{
    /// <summary>
    /// Returns the Material Symbols icon string for a notification category.
    /// Format: "material-symbols-rounded/{icon_name}" — compatible with MudBlazor's Icon attribute.
    /// </summary>
    /// <param name="category">The notification category string (case-insensitive).</param>
    /// <returns>A Material Symbols icon string.</returns>
    public static string GetIcon(string? category) => category?.ToLowerInvariant() switch
    {
        "account" => "material-symbols-rounded/security",
        "activity" => "material-symbols-rounded/people",
        "system" => "material-symbols-rounded/info",
        _ => "material-symbols-rounded/notifications"
    };

    /// <summary>
    /// Returns the MudBlazor CSS utility class for a notification category's avatar styling.
    /// Includes background color and contrasting white text for the icon.
    /// Compatible with the Class attribute on MudBlazor components (MudAvatar, etc.).
    /// </summary>
    /// <param name="category">The notification category string (case-insensitive).</param>
    /// <returns>A MudBlazor CSS class string (e.g., "mud-error mud-theme-dark").</returns>
    public static string GetColorClass(string? category) => category?.ToLowerInvariant() switch
    {
        "account" => "mud-error",
        "activity" => "mud-primary",
        "system" => "mud-info",
        _ => ""
    };
}
