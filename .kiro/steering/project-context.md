# Project Context — Architectural Patterns

## Key Architectural Patterns

- **Thin Controller / Full Service Layer**: Controllers handle ONLY HTTP concerns (request parsing, user identity extraction, status code mapping). All business logic, database access, audit logging, and entity mapping lives in service classes under `ApiService/Services/`. Reference: `NotificationController` → `INotificationService`.
- **Web ↔ ApiService communication**: HTTP calls via typed HttpClient services with Aspire service discovery (`https+http://apiservice`). Identity propagated via `UserIdentityDelegatingHandler`.
- **Per-circuit caching**: Scoped services in Blazor Server (e.g., `PagePermissionContext`, `NotificationContext`) load data once per SignalR circuit and provide synchronous in-memory lookups.
- **Whitelist authorization**: Page access controlled via database records (PagePermission entity). Record exists = access granted; absence = denied.
- **Audit logging**: A service-layer responsibility. Services call `IAuditLogService.LogAsync(AuditLogRequest)` with old/new value change tracking using `AuditChangeHelper`. Controllers pass `userId` and `ipAddress` to services; services construct the audit entry.

## Project Responsibilities (Summary)

- **Core** — domain enums, DTOs, shared interfaces, navigation models (no dependencies)
- **ApiService** — thin controllers (HTTP layer), service interfaces (`Abstractions/`), service implementations (`Services/`), EF Core, LDAP, seed data
- **Web** — Blazor pages, layout shell, API client services, authorization handlers
- **UI** — reusable MudBlazor components, grid utilities, theme config
- **Tests** — property-based + unit tests per feature
