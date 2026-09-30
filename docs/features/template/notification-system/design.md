# Notification System — Design

## Overview

The notification system provides in-app notifications with real-time delivery. Users receive
notifications for significant events (account changes, role assignments, admin actions), see an
unread badge that updates live, get a rich toast popup for new notifications, and manage per-category
delivery preferences.

It spans all layers of the template and is built from four cooperating parts, documented together here:

1. **Service layer** — `NotificationController` → `INotificationService` (create, query, read-status, bulk dismiss, preferences), with `ApiNotificationService` as the Web-side typed HttpClient.
2. **Real-time pipeline** — after the API persists a notification it calls back to the Web project, which pushes the event to the user's browser over SignalR.
3. **Deep-link toast** — the push carries the notification's `Guid` end-to-end so the toast can navigate straight to that notification.
4. **UI** — the topbar bell (badge + dropdown), the notifications page (master-detail), the snackbar toast, and the per-category settings section.

## Architecture

### Layering

Following the template's thin-controller / full-service convention:

- **Domain** (`Domain/Enums/NotificationCategory.cs`) — the category enum.
- **Application** (`Application/Features/Notifications/`) — `INotificationService` at the feature root; DTOs in `Contracts/` (`NotificationDto`, `CreateNotificationRequest`, `NotificationQueryParams`, `BulkDismissRequest`, `NotificationPreferenceDto`, `UpdateNotificationPreferenceRequest`, `NotificationPushRequest`).
- **Infrastructure** (`Infrastructure/Services/Notifications/NotificationService.cs`) — all business logic and EF Core access; `Infrastructure/Clients/WebCallbackClient.cs` — the API→Web push caller. Entities `Notification` and `NotificationPreference` live under `Infrastructure/Data/Entities/`.
- **ApiService** — `NotificationController` (thin; extracts `CurrentUserId` from `BaseController`, delegates to `INotificationService`, lets exceptions propagate to the central handler).
- **Web** — `ApiNotificationService` (typed client), `NotificationContext` (per-circuit unread-count cache + hub connection), `NotificationHub` (SignalR), `NotificationCallbackEndpoint` (internal push receiver), and the UI components.

`NotificationService` is registered **scoped** (aligns with per-request `DbContext` lifetime). `CreateNotificationAsync` is also called cross-cutting by other services (e.g. `UserService`, `RoleService`) to notify users of events; it is best-effort — failures are logged, never propagated, so a notification failure never disrupts the primary operation.

### Query flow (browser → API)

```
NotificationBell / Notifications page
        v
NotificationContext (synchronous cached unread count)  ── or ──  ApiNotificationService
        v
GET /api/notifications  → NotificationController → INotificationService → ApplicationDbContext (SQL)
        v
PagedResult<NotificationDto>  → ApiResult<PagedResult<NotificationDto>>  → UI
```

### Real-time delivery flow (API → browser)

Every connected Blazor Server user already holds a persistent SignalR circuit. The pipeline reuses it:

```
Service creates notification (NotificationService.CreateNotificationAsync)
        v
WebCallbackClient.NotifyAsync(NotificationPushRequest)   // Aspire service discovery "https+http://webfrontend"
   → POST /internal/notifications/push  (InternalApiKey-authenticated)
        v
NotificationCallbackEndpoint.HandlePush  → IHubContext<NotificationHub>.SendAsync("ReceiveNotification", ...)
        v
NotificationContext hub handler  → raises OnNotificationReceived(NotificationReceivedEventArgs)
        v
NotificationBell: badge count updates live + snackbar toast shown
```

`NotificationPushRequest` carries `UserId`, `Title`, `Message`, `Category`, `UnreadCount`, and
`NotificationId` (Guid). The `NotificationId` flows end-to-end so the toast's click handler can deep-link
to `/account/notifications?id={notificationId}` and expand that specific notification inline. Events are
delivered to the user's SignalR group; `NotificationReceivedEventArgs` (`Web/Common/`) bundles the event
data into a single strongly-typed argument.

**Internal callback auth:** the API attaches a shared `INTERNAL_API_KEY` via `InternalApiKeyDelegatingHandler`; the Web validates it via `InternalApiKeyAuthenticationHandler` + the `InternalApiPolicy`. Callback failures are logged at Warning level and never disrupt the primary operation — the notification is already persisted, so real-time delivery is best-effort and the user still sees it on next page load.

**Server-side hub connection:** `NotificationContext` opens a `HubConnection` from server-side code back to the same host. Because there is no browser in that path, it manually forwards the user's auth cookie (captured from `IHttpContextAccessor` during SSR) when building the connection, and uses `ExponentialBackoffRetryPolicy` for automatic reconnect.

## Components and Interfaces

### INotificationService (`Application/Features/Notifications/`)

Owns all notification business logic and database access. Methods:

| Method | Purpose |
|---|---|
| `CreateNotificationAsync(CreateNotificationRequest)` | Create a notification, respecting the user's `InAppEnabled` preference for the category. Best-effort; never throws. |
| `GetNotificationsAsync(userId, NotificationQueryParams)` | Paged list, newest first, optional category/read-status filters. PageSize clamped to 100. |
| `GetUnreadCountAsync(userId)` | Count of `IsRead=false`. |
| `GetRecentAsync(userId, count = 5)` | Most recent notifications for the bell dropdown. |
| `MarkAsReadAsync(userId, id)` / `MarkAsUnreadAsync(userId, id)` | Toggle read state (idempotent). Returns false if not found or not owned by the user. |
| `MarkAllAsReadAsync(userId)` | Mark all unread as read; returns count updated. |
| `BulkDismissAsync(userId, List<Guid>)` | Delete owned notifications; foreign/unknown IDs silently ignored. Max 100 IDs (enforced in the controller). |
| `GetPreferencesAsync(userId)` | One `NotificationPreferenceDto` per category; categories without a stored record default to both channels enabled. |
| `UpdatePreferenceAsync(userId, UpdateNotificationPreferenceRequest)` | Upsert the per-category `InAppEnabled`/`EmailEnabled` toggles. |

### NotificationController (`ApiService/Controllers/`)

Thin REST controller. Reads `CurrentUserId` from `BaseController`, performs only input-format checks
(e.g. rejects a bulk-dismiss request with more than 100 IDs), delegates to `INotificationService`, and
returns the result. Service exceptions propagate to the central `ExceptionMappingHandler`.

### ApiNotificationService (`Web/Services/ApiClients/`)

Typed HttpClient wrapping the controller endpoints; returns `ApiResult`/`ApiResult<T>`.

### NotificationContext (`Web/Services/Contexts/`, per-circuit scoped)

Caches the unread count for synchronous UI reads, owns the `NotificationHub` connection lifecycle, and
raises `OnNotificationReceived` (with `NotificationReceivedEventArgs`) when a push arrives. UI components
subscribe for live badge updates and toast display.

### Real-time components (Web)

- `NotificationHub` (`Web/Hubs/`) — `[Authorize]` SignalR hub; groups connections by user.
- `NotificationCallbackEndpoint` (`Web/Endpoints/`) — minimal API `/internal/notifications/push`; validates the `NotificationPushRequest` (400 on invalid) and forwards to the hub.
- `WebCallbackClient` (`Infrastructure/Clients/`) — typed client the API uses to POST the push.

### UI components

- **NotificationBell** (topbar) — `MudMenu` dropdown with a badge for the unread count, category-colored icons, skeleton loading, and a snackbar toast on new pushes. Toast click navigates to `/account/notifications?id={notificationId}`.
- **NotificationSnackbarContent** (`UI/Components/Shared/`) — the rich toast body: category-colored avatar icon, bold title, caption message, top-left-aligned `MudStack` to support multi-line, ellipsis truncation, cursor-pointer for click-to-navigate. Rendered via `ISnackbar.Add<NotificationSnackbarContent>()` with a per-snackbar `PositionClass` (top-right) so the global `MudSnackbarProvider` (bottom-center action feedback) is untouched.
- **Notifications page** (`Web/Components/Pages/Account/Notifications`) — master-detail: scrollable list with infinite scroll on the left, selected-notification detail on the right, per-item action menu (mark read/unread, delete).
- **Settings — Notifications section** — `MudSimpleTable` with per-category rows and per-channel (In-App / Email) checkboxes bound to `UpdatePreferenceAsync`.

## Data Models

- **Notification** entity — `Id` (Guid), `UserId`, `Category`, `Title`, `Message`, `IsRead`, `CreatedAtUtc`, `ReadAtUtc` (nullable; set on first mark-as-read, not modified afterward).
- **NotificationPreference** entity — per user-category `InAppEnabled` / `EmailEnabled` toggles.
- **NotificationCategory** enum (`Domain/Enums/`) — the category set (e.g. Account, Activity, System).
- DTOs in `Application/Features/Notifications/Contracts/` as listed under Architecture.

## Key Design Decisions

- **Thin controller / full service** — mirrors `PagePermissionsController` → `IPagePermissionService`; the controller never touches `DbContext`.
- **Best-effort creation** — `CreateNotificationAsync` swallows and logs failures so notifying a user can never break the operation that triggered it.
- **Per-circuit unread cache** — `NotificationContext` gives the bell a synchronous count without a round-trip on every render.
- **Reuse the existing SignalR circuit** — no separate WebSocket; the push rides the connection every Blazor Server user already has.
- **Notification ID carried end-to-end** — one `Guid` field on the push DTO enables deep-linking without a second contract; a strongly-typed `NotificationReceivedEventArgs` avoids parameter-count growth as the event evolves.
- **Per-snackbar position override** — the toast appears top-right via `SnackbarOptions.PositionClass`, leaving the global provider's bottom-center action feedback intact.
- **Snackbar body as a UI-project component** — `NotificationSnackbarContent` lives in the shared UI RCL for reuse and testability; its truncation rule is a static helper so it can be unit-tested without rendering.