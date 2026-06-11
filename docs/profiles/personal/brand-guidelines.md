# Personal Brand Guidelines

## Overview

When deploying this template for personal projects or freelance work, customize the ApplicationTheme to match your own branding or your client's brand identity.

## Default Personal Theme

If no specific branding is needed, the template defaults to a clean neutral palette:

| Element | Light Mode | Dark Mode |
|---------|-----------|-----------|
| Primary | `#1976D2` (Material Blue) | `#90CAF9` (Light Blue) |
| Background | `#FFFFFF` | `#121212` |
| Surface | `#FFFFFF` | `#1E1E1E` |
| AppBar | `#1976D2` | `#1E1E1E` |
| Drawer | `#F5F5F5` | `#121212` |

## Customization

Modify `BlazorWebAppTemplate.UI/Theme/ApplicationTheme.cs`:

```csharp
// Replace Jabil colors with your own
PaletteLight = new PaletteLight
{
    Primary = "#YOUR_PRIMARY_COLOR",
    AppbarBackground = "#YOUR_APPBAR_COLOR",
    DrawerBackground = "#YOUR_DRAWER_COLOR",
    // ...
};
```

## Typography

The template uses Material Design defaults (Roboto) which work universally. For client projects, you can override via MudTheme's Typography property.

## Client Projects

For freelance/client work, create a profile at `docs/profiles/client-name/brand-guidelines.md` with the client's specific brand colors, fonts, and logo requirements.
