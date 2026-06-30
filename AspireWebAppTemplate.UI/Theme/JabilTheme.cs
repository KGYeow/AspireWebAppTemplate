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
        // Clean, professional appearance with Jabil dark navy primary on light backgrounds.
        // -----------------------------------------------------------------
        PaletteLight = new PaletteLight()
        {
            // Brand colors — Jabil dark navy hierarchy
            Primary = "#003865",
            Secondary = "#005288",
            Tertiary = "#0164A1",

            // Backgrounds
            Background = "#F1F2F2",       // page background
            BackgroundGray = "#F1F2F2",   // alternate background (e.g., striped rows)
            Surface = "#FFFFFF",          // cards, papers, dialogs

            // App shell
            AppbarBackground = "#003865",
            DrawerBackground = "#003865",
            DrawerText = "#FFFFFF",
            DrawerIcon = "#FFFFFF",

            // Text
            TextPrimary = "#414042",      // main body text
            TextSecondary = "#60605B",    // muted/secondary text

            // Semantic colors
            Info = "#0990CF",
            InfoLighten = "#15BEF0",
        };

        // -----------------------------------------------------------------
        // Dark Palette
        // Inverted for dark backgrounds — lighter blues for primary actions,
        // dark gray surfaces, and light text for readability.
        // -----------------------------------------------------------------
        PaletteDark = new PaletteDark()
        {
            // Brand colors — lighter blues that pop on dark surfaces
            Primary = "#4A9BD9",
            Secondary = "#5AABE8",
            Tertiary = "#7BC0F0",

            // Backgrounds — dark gray with subtle layering
            Background = "#121212",       // page background (Material Design dark surface)
            BackgroundGray = "#212529",   // alternate background
            Surface = "#212529",          // cards, papers, dialogs

            // App shell — darker than surface for visual hierarchy
            AppbarBackground = "#1A1A1A",
            DrawerBackground = "#161616",
            DrawerText = "#E0E0E0",
            DrawerIcon = "#B0B0B0",

            // Text — high contrast on dark backgrounds
            TextPrimary = "#E8E8E8",      // main body text
            TextSecondary = "#A0A0A0",    // muted/secondary text

            // Semantic colors — slightly brighter for dark mode visibility
            Info = "#29B6F6",
            InfoLighten = "#4FC3F7",

            // Lines and dividers — subtle on dark surfaces
            LinesDefault = "#2E2E2E",
            LinesInputs = "#3A3A3A",

            // Action colors — hover/active states
            ActionDefault = "#9E9E9E",
            ActionDisabled = "#5A5A5A",
            ActionDisabledBackground = "#2A2A2A",
        };

        // -----------------------------------------------------------------
        // Layout Properties
        // Shared across both light and dark modes.
        // -----------------------------------------------------------------
        LayoutProperties = new LayoutProperties()
        {
            DefaultBorderRadius = "8px",
        };
    }
}
