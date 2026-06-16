# ADR-001: Blazor Server Rendering Mode

## Status
Accepted

## Context
The application needs interactive UI components (forms, dialogs, real-time theme switching) and integrates with internal LDAP services that require server-side network access.

## Decision
Use Blazor Server with Interactive Server rendering mode exclusively. No WebAssembly or static SSR.

## Consequences
- **Positive**: SignalR circuits enable scoped per-user services (ThemeStateService, UserTimeZoneContext)
- **Positive**: Smaller client payload (no .NET runtime download)
- **Positive**: Server-side rendering allows secure HTTP calls to the API backend without exposing tokens to the browser
- **Negative**: Requires persistent WebSocket connection (not suitable for offline scenarios)
- **Negative**: Server memory scales with connected users
- **Acceptable for**: Internal enterprise app with known user count on corporate network
