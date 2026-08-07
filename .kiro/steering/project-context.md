# Project Context — Architectural Patterns

## Key Architectural Patterns

- **Clean Architecture (4-layer)**: Domain → Application → Infrastructure → Host projects. Dependencies flow inward only. Domain has zero dependencies; Application depends on Domain; Infrastructure depends on Application; host projects (ApiService) depend on Application + Infrastructure; Web depends on Application + UI + ServiceDefaults.
- **Thin Controller / Full Service Layer**: Controllers handle ONLY HTTP concerns (request parsing, status code mapping). All business logic, database access, audit logging, and entity mapping lives in service classes under `Infrastructure/Services/`.
- **ICurrentUserAccessor**: A scoped service that provides the authenticated user's `UserId`, `UserName`, and `IpAddress` to service-layer components. Services inject this directly — no need to pass identity through method parameters.
- **Web ↔ ApiService communication**: HTTP calls via typed HttpClient services with Aspire service discovery (`https+http://apiservice`). Identity propagated via `UserIdentityDelegatingHandler` which forwards user claims and client IP (`X-Client-Ip` header).
- **Per-circuit caching**: Scoped services in Blazor Server (e.g., `PagePermissionContext`, `NotificationContext`) load data once per SignalR circuit and provide synchronous in-memory lookups.
- **Real-time notifications (API→Web→Client)**: When the API creates a notification, it calls a Web project internal endpoint (`/internal/notifications/push`) via `WebCallbackClient`. The push request carries the `NotificationId` (Guid) end-to-end. The Web project delivers the event to the user's browser via `NotificationHub` (SignalR) with 5 parameters: title, message, category, unreadCount, notificationId. The `NotificationContext` manages the hub connection lifecycle and raises `OnNotificationReceived` with a strongly-typed `NotificationReceivedEventArgs` (Title, Message, Category, NotificationId). UI components use the notification ID to construct deep-link URLs (`/account/notifications?id={notificationId}`) for snackbar click navigation.
- **Internal service-to-service auth**: API→Web callbacks use a shared `INTERNAL_API_KEY` environment variable. The API attaches it via `InternalApiKeyDelegatingHandler`; the Web validates it via `InternalApiKeyAuthenticationHandler` + `InternalApiPolicy`.
- **Server-side hub connection with cookie forwarding**: In Blazor Server, hub connections from server-side code back to the same host require manually forwarding the user's auth cookie (captured from `IHttpContextAccessor` during SSR). `UserIdentityDelegatingHandler` is NOT involved — it only handles Web→API HttpClient calls.
- **Whitelist authorization**: Page access controlled via database records (PagePermission entity). Record exists = access granted; absence = denied.
- **Audit logging**: A service-layer responsibility. Services call `IAuditLogService.LogAsync(AuditLogRequest)` with old/new value change tracking using `AuditChangeHelper`. Only security-sensitive operations are audited (admin actions, password changes, 2FA changes, account deletion). Personal profile edits and preference changes are not audited.
- **Email service**: `IEmailService` / `EmailService` implements both a custom interface and `IEmailSender<ApplicationUser>` for Identity integration. All templates stored in database, resolved by `EmailType` enum. `EmailTemplateCategory` determines editability (System=read-only, Business=admin-editable). Admin page at `/admin/email-templates`.

## Project Responsibilities (Summary)

- **Domain** — domain enums, constants, custom attributes, pure entities (zero dependencies)
- **Application** — service interfaces, DTOs/contracts, shared models, extension methods, pure-logic utilities (depends on Domain only)
- **Infrastructure** — EF Core DbContext, Identity entities, service implementations, data access, typed HttpClients, delegating handlers, utilities (depends on Application)
- **ApiService** — thin REST controllers (HTTP layer), authentication handler, composition root / Program.cs (depends on Application + Infrastructure + ServiceDefaults)
- **Web** — Blazor pages, layout shell, API client services, authorization handlers (depends on Application + UI + ServiceDefaults)
- **UI** — reusable MudBlazor components, grid utilities, theme config
- **ServiceDefaults** — Aspire shared configuration (telemetry, health, resilience)
- **AppHost** — Aspire orchestrator (dev entry point, service discovery wiring)
- **Tests** — property-based + unit tests per feature

## Key Design Decisions

- **Clean Architecture layers** — Domain/Application/Infrastructure separation enforces dependency inversion. Infrastructure implements Application interfaces; host projects compose the full dependency graph.
- **No domain events / MediatR** — direct service calls are sufficient at current scale. Introduce when >15 cross-cutting triggers exist.
- **No repository pattern** — services access `ApplicationDbContext` directly. EF Core IS the repository/unit-of-work.
- **No FluentValidation** — DataAnnotations on DTOs + service-layer throws for business rules. Simpler for a template.
- **All email templates in database** — both system and business templates. No file-based templates. `EmailTemplateCategory` determines editability, not storage.
- **Edit-only template model** — admins customize content, cannot create/delete template types. Set defined by `EmailType` enum + seed data.
- **Wrapper ViewModel pattern** — all DataGrid pages use a ViewModel wrapping the DTO reference (not flat property copy). `vm.Dto` gives instant access for dialog parameters.
- **Best-effort email delivery** — `TrySendEmailAsync` checks user preferences, never throws. Primary operations are never blocked by email failures.
- **`ServerData` for all admin grids** — consistent filtering/sorting/pagination via `DataGridUtils<T>`, even for small datasets. Simplifies maintenance and makes the pattern uniform.
