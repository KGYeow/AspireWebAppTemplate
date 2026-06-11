# ADR-001: Blazor Server Rendering Mode

## Status
Accepted

## Context
The application needs interactive UI components (forms, dialogs, real-time theme switching) and integrates with internal LDAP services that require server-side network access.

## Decision
Use Blazor Server with Interactive Server rendering mode exclusively. No WebAssembly or static SSR.

## Consequences
- **Positive**: Direct server access to LDAP, database, and internal services without API layer
- **Positive**: Smaller client payload (no .NET runtime download)
- **Positive**: SignalR circuits enable scoped per-user services (ThemeStateService, UserTimeZoneContext)
- **Negative**: Requires persistent WebSocket connection (not suitable for offline scenarios)
- **Negative**: Server memory scales with connected users
- **Acceptable for**: Internal enterprise app with known user count on corporate network
