# ADR-002: MudBlazor UI Component Library

## Status
Accepted

## Context
The application needs a comprehensive Material Design component library for Blazor that provides data grids, form controls, dialogs, navigation, and theming.

## Decision
Use MudBlazor as the primary UI component library.

## Consequences
- **Positive**: Rich component set (MudDataGrid, MudAutocomplete, MudDialog, MudToggleGroup, etc.)
- **Positive**: Built-in dark/light theme support via MudThemeProvider with dual palettes
- **Positive**: Active community and regular updates
- **Positive**: Consistent Material Design language across the application
- **Negative**: Tight coupling to MudBlazor API (difficult to swap later)
- **Acceptable because**: This is an internal template, not a reusable library — consistency matters more than portability
