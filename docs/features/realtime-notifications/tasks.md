# Implementation Plan: Real-Time Notifications

## Overview

This plan implements a real-time push notification pipeline using SignalR. The API project calls back to the Web project after persisting a notification, and the Web project's NotificationHub pushes the event to the user's connected Blazor circuits. The NotificationBell badge updates instantly and a snackbar toast is shown.

## Tasks

- [x] 1. Set up Aspire orchestration and shared contracts
  - [x] 1.1 Update AppHost to add API→Web service discovery and internal API key parameter
    - Modify `AspireWebAppTemplate.AppHost/AppHost.cs` to add `WithReference(webfrontend)` on the apiservice and configure a shared `InternalApiKey` secret parameter passed to both projects as an environment variable
    - _Requirements: 5.1, 5.2, 5.4_

  - [x] 1.2 Create the NotificationPushRequest DTO in the Core project
    - Create `AspireWebAppTemplate.Core/Contracts/Notifications/NotificationPushRequest.cs` with UserId, Title, Category (string), and UnreadCount properties
    - _Requirements: 2.2_

- [x] 2. Implement internal authentication for service-to-service callbacks
  - [x] 2.1 Create InternalApiKeyAuthenticationHandler in the Web project
    - Create `AspireWebAppTemplate.Web/Authentication/InternalApiKeyAuthenticationHandler.cs` that validates the `X-Internal-Api-Key` header against the configured `INTERNAL_API_KEY` environment variable
    - Register the authentication scheme and `InternalApiPolicy` authorization policy in the Web project's DI setup
    - _Requirements: 5.4, 5.5, 7.1_

  - [x] 2.2 Create InternalApiKeyDelegatingHandler in the API project
    - Create `AspireWebAppTemplate.ApiService/Services/Handlers/InternalApiKeyDelegatingHandler.cs` that attaches the `X-Internal-Api-Key` header to all outbound requests using the value from `INTERNAL_API_KEY` configuration
    - Register as a transient service in the API project DI
    - _Requirements: 5.6_

  - [x] 2.3 Write property test for internal API key authentication
    - **Property 8: API key authentication validates correctly**
    - **Validates: Requirements 5.4, 5.5**

- [x] 3. Implement NotificationHub in the Web project
  - [x] 3.1 Create the NotificationHub class
    - Create `AspireWebAppTemplate.Web/Hubs/NotificationHub.cs` with `[Authorize]` attribute
    - Implement `OnConnectedAsync` to extract the user's NameIdentifier claim and add the connection to a SignalR group keyed by user ID; abort connection if unauthenticated
    - Implement `OnDisconnectedAsync` to remove the connection from the user's group
    - No client-callable methods exposed (server-to-client only)
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 7.1, 7.2, 7.3_

  - [x] 3.2 Register SignalR services and map the hub endpoint
    - Add `builder.Services.AddSignalR()` in Web project's `Program.cs`
    - Map the hub at `/hubs/notifications` with `app.MapHub<NotificationHub>("/hubs/notifications")`
    - _Requirements: 1.2, 1.5_

- [x] 4. Implement the notification callback endpoint in the Web project
  - [x] 4.1 Create the NotificationCallbackEndpoint minimal API
    - Create `AspireWebAppTemplate.Web/Endpoints/NotificationCallbackEndpoint.cs` with a `MapNotificationCallback` extension method
    - Route: `POST /internal/notifications/push` with `RequireAuthorization("InternalApiPolicy")`
    - Validate the NotificationPushRequest (UserId non-empty, Title non-empty and max 200 chars, Category is valid NotificationCategory string, UnreadCount >= 0)
    - On valid request: use `IHubContext<NotificationHub>` to send "ReceiveNotification" to the user's group, return 200 OK
    - On invalid request: return 400 Bad Request
    - On missing/invalid auth: framework returns 401 Unauthorized
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.8_

  - [x] 4.2 Register the callback endpoint in Program.cs
    - Call `app.MapNotificationCallback()` in the Web project's startup pipeline
    - _Requirements: 2.1_

  - [x] 4.3 Write property tests for callback validation
    - **Property 1: Valid callback requests are accepted**
    - **Property 2: Invalid callback requests are rejected**
    - **Validates: Requirements 2.2, 2.3, 2.8**

- [x] 5. Implement WebCallbackClient in the API project
  - [x] 5.1 Create the WebCallbackClient typed HttpClient
    - Create `AspireWebAppTemplate.ApiService/Services/WebCallbackClient.cs` with a `NotifyAsync` method that POSTs to `/internal/notifications/push`
    - Handle `TaskCanceledException` (timeout), `HttpRequestException` (network), and non-success status codes by logging at Warning level without throwing
    - _Requirements: 2.6, 2.7, 5.3, 5.7_

  - [x] 5.2 Register WebCallbackClient with Aspire service discovery
    - In `AspireWebAppTemplate.ApiService/Extensions/ApplicationServiceExtensions.cs`, register the typed HttpClient with base address `https+http://webfrontend`, 5-second timeout, and the `InternalApiKeyDelegatingHandler`
    - _Requirements: 5.1, 5.3, 5.6, 5.7_

  - [x] 5.3 Integrate WebCallbackClient into NotificationService.CreateNotificationAsync
    - Inject `WebCallbackClient` into `NotificationService`
    - After successfully persisting the notification, compute the user's unread count and call `WebCallbackClient.NotifyAsync` with userId, title, category string, and unread count
    - Wrap the callback call in try/catch so failures never disrupt notification creation
    - _Requirements: 2.6, 2.7_

  - [x] 5.4 Write property test for callback failure resilience
    - **Property 3: Callback failure does not disrupt notification creation**
    - **Validates: Requirements 2.7**

- [x] 6. Checkpoint - Ensure backend pipeline compiles and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Implement client-side real-time integration
  - [x] 7.1 Create ExponentialBackoffRetryPolicy
    - Create `AspireWebAppTemplate.Web/Services/ExponentialBackoffRetryPolicy.cs` implementing `IRetryPolicy`
    - Delays: 1s, 2s, 4s, 8s, 16s (capped at 30s), returns null after 5 attempts
    - _Requirements: 6.1_

  - [x] 7.2 Add UpdateFromHub method to NotificationContext
    - Add `UpdateFromHub(int unreadCount)` method to `INotificationContext` interface and `NotificationContext` implementation
    - Set `_unreadCount` to `Math.Max(0, unreadCount)` and invoke `OnChange`
    - _Requirements: 3.2_

  - [x] 7.3 Add PrependNotification method to NotificationContext
    - Add a method that accepts a `NotificationDto` and prepends it to a cached recent notifications list, raising `OnChange`
    - Used when the dropdown is open and a new notification arrives via SignalR
    - _Requirements: 3.5_

  - [x] 7.4 Write property tests for NotificationContext hub update
    - **Property 4: UpdateFromHub replaces cached unread count**
    - **Validates: Requirements 3.2**

  - [x] 7.5 Write property test for exponential backoff retry delays
    - **Property 7: Exponential backoff retry delays**
    - **Validates: Requirements 6.1**

- [x] 8. Integrate SignalR hub connection into NotificationBell component
  - [x] 8.1 Add hub connection lifecycle to NotificationBell
    - In `NotificationBell.razor.cs` `OnInitializedAsync`, create a `HubConnection` to `/hubs/notifications` with `WithAutomaticReconnect(new ExponentialBackoffRetryPolicy())`
    - Register handler for "ReceiveNotification" event that calls `NotificationContext.UpdateFromHub` and `InvokeAsync(StateHasChanged)` for thread-safe UI updates
    - Handle `Reconnected` event by calling `NotificationContext.RefreshAsync()` to reconcile missed notifications
    - Handle `Closed` event to abandon hub connection after all retries are exhausted (fall back to navigation-based refresh)
    - Dispose the hub connection in `IAsyncDisposable.DisposeAsync`
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 6.1, 6.2, 6.3, 6.4_

  - [x] 8.2 Add dropdown live update on incoming notification
    - When "ReceiveNotification" fires and the dropdown is open, prepend a new `NotificationDto` to the cached recent list so the dropdown reflects the latest state without manual refresh
    - _Requirements: 3.5_

- [x] 9. Implement snackbar toast for new notifications
  - [x] 9.1 Add snackbar toast display on ReceiveNotification event
    - In the "ReceiveNotification" handler, use `ISnackbar.Add(...)` to show a toast with the notification title (truncated to 100 chars with ellipsis if longer)
    - Configure: auto-dismiss after 5 seconds, category icon matching NotificationBell dropdown styling, click action navigates to notifications page
    - Check user's notification preference (`InAppEnabled` for the category) before showing; suppress toast if disabled
    - Follow MudBlazor's default stacking behavior for multiple toasts
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7_

  - [x] 9.2 Write property test for toast suppression logic
    - **Property 5: Toast suppression respects InAppEnabled preference**
    - **Validates: Requirements 4.5**

  - [x] 9.3 Write property test for title truncation
    - **Property 6: Title truncation preserves content within limit**
    - **Validates: Requirements 4.7**

- [x] 10. Implement security and authentication state handling
  - [x] 10.1 Handle authentication state changes in hub connection
    - When a user logs out or session expires, remove the connection from the User_Group and terminate the hub connection
    - Ensure no unhandled exceptions surface to the user when the hub connection is terminated due to auth changes
    - _Requirements: 7.4, 7.5, 6.4_

- [x] 11. Final checkpoint - Ensure all tests pass and integration is complete
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- The design uses C# throughout — all implementations target .NET 10 with Blazor Server
- The existing `NotificationContext` already supports per-circuit caching and `RefreshAsync` — the new `UpdateFromHub` method extends this pattern
- The AppHost orchestration change is foundational and must be completed first

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "2.2", "3.1"] },
    { "id": 2, "tasks": ["2.3", "3.2", "4.1", "7.1", "7.2", "7.3"] },
    { "id": 3, "tasks": ["4.2", "4.3", "5.1", "7.4", "7.5"] },
    { "id": 4, "tasks": ["5.2"] },
    { "id": 5, "tasks": ["5.3", "5.4"] },
    { "id": 6, "tasks": ["8.1", "8.2"] },
    { "id": 7, "tasks": ["9.1", "10.1"] },
    { "id": 8, "tasks": ["9.2", "9.3"] }
  ]
}
```
