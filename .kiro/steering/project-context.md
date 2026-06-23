# Project Context — Architectural Patterns

## Key Architectural Patterns

- **Web ↔ ApiService communication**: HTTP calls via typed HttpClient services with Aspire service discovery (`https+http://apiservice`). Identity propagated via `UserIdentityDelegatingHandler`.
- **Per-circuit caching**: Scoped services in Blazor Server (e.g., `PagePermissionContext`) load data once per SignalR circuit and provide synchronous in-memory lookups.
- **Whitelist authorization**: Page access controlled via database records (PagePermission entity). Record exists = access granted; absence = denied.
- **Audit logging**: All significant actions logged via `IAuditLogService.LogAsync(AuditLogRequest)` with old/new value change tracking using `AuditChangeHelper`.

## Project Responsibilities (Summary)

- **Core** — domain enums, DTOs, shared interfaces, navigation models (no dependencies)
- **ApiService** — controllers, EF Core, business services, LDAP, seed data
- **Web** — Blazor pages, layout shell, API client services, authorization handlers
- **UI** — reusable MudBlazor components, grid utilities, theme config
- **Tests** — property-based + unit tests per feature
