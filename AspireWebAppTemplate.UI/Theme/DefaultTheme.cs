using MudBlazor;

namespace AspireWebAppTemplate.UI.Theme;

/// <summary>
/// The default/template MudBlazor theme configuration.
/// Provides a clean, neutral design suitable for personal projects or as a starting point
/// for custom branding. Defines both light and dark color palettes.
/// </summary>
/// <remarks>
/// <para>
/// This theme extends <see cref="MudTheme"/> and is consumed by <c>MudThemeProvider</c>
/// in the layout components. MudBlazor automatically switches between
/// <see cref="MudTheme.PaletteLight"/> and <see cref="MudTheme.PaletteDark"/>
/// based on the <c>IsDarkMode</c> property on the provider.
/// </para>
/// <para>
/// To create a branded variant, copy this file and adjust the palette values.
/// See <see cref="JabilTheme"/> for a corporate branding example.
/// </para>
/// </remarks>
public class DefaultTheme : MudTheme
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTheme"/> class
    /// with predefined light and dark palettes and shared layout properties.
    /// </summary>
    public DefaultTheme()
    {
        // -----------------------------------------------------------------
        // Light Palette
        // Clean, neutral appearance with a blue primary.
        // -----------------------------------------------------------------
        PaletteLight = new PaletteLight()
        {
            Primary = "#1976D2",
            Secondary = "#42A5F5",
            Tertiary = "#64B5F6",

            Background = "#F5F5F5",
            BackgroundGray = "#EEEEEE",
            Surface = "#FFFFFF",

            AppbarBackground = "#1976D2",
            DrawerBackground = "#1976D2",
            DrawerText = "#FFFFFF",
            DrawerIcon = "#FFFFFF",

            TextPrimary = "#212121",
            TextSecondary = "#757575",

            Info = "#2196F3",
            InfoLighten = "#64B5F6",
        };

        // -----------------------------------------------------------------
        // Dark Palette
        // Inverted for dark backgrounds with lighter blues.
        // -----------------------------------------------------------------
        PaletteDark = new PaletteDark()
        {
            Primary = "#64B5F6",
            Secondary = "#90CAF9",
            Tertiary = "#BBDEFB",

            Background = "#121212",
            BackgroundGray = "#1E1E1E",
            Surface = "#1E1E1E",

            AppbarBackground = "#1A1A1A",
            DrawerBackground = "#161616",
            DrawerText = "#E0E0E0",
            DrawerIcon = "#B0B0B0",

            TextPrimary = "#E8E8E8",
            TextSecondary = "#A0A0A0",

            Info = "#29B6F6",
            InfoLighten = "#4FC3F7",

            LinesDefault = "#2E2E2E",
            LinesInputs = "#3A3A3A",

            ActionDefault = "#9E9E9E",
            ActionDisabled = "#5A5A5A",
            ActionDisabledBackground = "#2A2A2A",
        };

        // -----------------------------------------------------------------
        // Layout Properties
        // -----------------------------------------------------------------
        LayoutProperties = new LayoutProperties()
        {
            DefaultBorderRadius = "8px",
        };
    }
}
