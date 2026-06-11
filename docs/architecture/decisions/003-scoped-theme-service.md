# ADR-003: Scoped ThemeStateService for Real-Time Theme Switching

## Status
Accepted

## Context
Users need to switch between Light, Dark, and System themes on the Settings page and see the change reflected immediately across the entire application layout (MainLayout, MudThemeProvider) without a page reload.

## Decision
Use a scoped `ThemeStateService` registered per SignalR circuit as a pub/sub mechanism between the Settings page (publisher) and MainLayout (subscriber).

## Alternatives Considered
1. **Cascading parameter from MainLayout**: Would require passing theme state through the component tree — tight coupling and prop drilling.
2. **Static/singleton service**: Would leak state between users on Blazor Server.
3. **Browser localStorage + JS interop only**: Would require page reload to apply theme and wouldn't support server-side rendering.

## Consequences
- **Positive**: Instant theme switching without page reload
- **Positive**: Per-user session isolation (scoped lifetime = one per SignalR circuit)
- **Positive**: Decoupled components — Settings page and MainLayout don't reference each other
- **Positive**: Same pattern extensible for other real-time state (e.g., locale changes)
- **Negative**: Requires IDisposable on MainLayout to unsubscribe from events
- **Negative**: Theme flash on first render (resolved by loading from DB in OnAfterRenderAsync)
