using MudBlazor;

namespace AspireWebAppTemplate.UI.Theme;

/// <summary>
/// Jabil corporate MudBlazor theme configuration.
/// Uses Jabil's brand color palette (dark navy primary) for professional, corporate appearance.
/// Defines both light and dark color palettes.
/// </summary>
/// <remarks>
/// <para>
/// This theme extends <see cref="MudTheme"/> and is consumed by <c>MudThemeProvider</c>
/// in the layout components. MudBlazor automatically switches between
/// <see cref="MudTheme.PaletteLight"/> and <see cref="MudTheme.PaletteDark"/>
/// based on the <c>IsDarkMode</c> property on the provider.
/// </para>
/// <para>
/// The light palette uses Jabil's dark navy primary (#003865) for a professional, corporate feel.
/// The dark palette inverts this — using lighter blues on dark surfaces for readability
/// while maintaining the same brand identity.
/// </para>
/// </remarks>
public class JabilTheme : MudTheme
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JabilTheme"/> class
    /// with Jabil corporate brand palettes and shared layout properties.
    /// </summary>
    public JabilTheme()
    {
        // -----------------------------------------------------------------
        // Light Palette
        // Based on Jabil Brand Guidelines v24 color palette.
        // Primary: Jabil Blue (#003B6B), Accent: Sky Blue (#15BEF0)
        // -----------------------------------------------------------------
        PaletteLight = new PaletteLight()
        {
            // Brand colors — Jabil Blue hierarchy
            Primary = "#003B6B",          // Jabil Blue (PMS 2955 C)
            Secondary = "#005288",        // Old Jabil Blue
            Tertiary = "#0764A1",         // Medium Blue
            Info = "#09A4CF",             // Light Blue
            InfoLighten = "#15BEF0",      // Sky Blue (PMS 298 C)

            // Backgrounds
            Background = "#F1F2F2",       // Background Grey (PMS 427 C)
            BackgroundGray = "#F1F2F2",   // Background Grey
            Surface = "#FFFFFF",          // cards, papers, dialogs

            // App shell — Jabil Blue
            AppbarBackground = "#003B6B",
            DrawerBackground = "#002B49", // Navy (PMS 7463 C) — darker for sidebar
            DrawerText = "#FFFFFF",
            DrawerIcon = "#FFFFFF",

            // Text — brand text colors
            TextPrimary = "#414042",      // Almost Black / Text Grey (PMS 426 C)
            TextSecondary = "#60605B",    // Dark Grey (PMS 425 C)
        };

        // -----------------------------------------------------------------
        // Dark Palette
        // Inverted for dark backgrounds — lighter blues for primary actions,
        // dark gray surfaces, and light text for readability.
        // Sky Blue (#15BEF0) becomes the primary on dark surfaces.
        // -----------------------------------------------------------------
        PaletteDark = new PaletteDark()
        {
            // Brand colors — lighter blues that pop on dark surfaces
            Primary = "#15BEF0",          // Sky Blue (PMS 298 C)
            Secondary = "#09A4CF",        // Light Blue
            Tertiary = "#7BC0F0",

            // Backgrounds — dark gray with subtle layering
            Background = "#121212",       // Material Design dark surface
            BackgroundGray = "#1A1A1A",
            Surface = "#1E1E1E",          // cards, papers, dialogs

            // App shell — darker than surface for visual hierarchy
            AppbarBackground = "#002B49", // Navy
            DrawerBackground = "#001F36", // Darker navy
            DrawerText = "#E0E0E0",
            DrawerIcon = "#B0B0B0",

            // Text — high contrast on dark backgrounds
            TextPrimary = "#E8E8E8",
            TextSecondary = "#A0A0A0",

            // Semantic colors
            Info = "#15BEF0",             // Sky Blue
            InfoLighten = "#4FC3F7",

            // Lines and dividers
            LinesDefault = "#2E2E2E",
            LinesInputs = "#3A3A3A",

            // Action colors
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
