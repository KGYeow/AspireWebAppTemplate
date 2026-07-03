# Requirements Document

## Introduction

This feature adds real-time push notifications to the Blazor Server enterprise web app template using SignalR. Currently, the notification badge updates only on page navigation or when the bell dropdown is opened. With this feature, when any service in the API project creates a notification, the target user's NotificationBell badge updates instantly without requiring page navigation. The architecture leverages the existing Blazor Server SignalR circuit and introduces an HTTP callback mechanism from the API project to the Web project to bridge the two services.

## Glossary

- **Notification_Hub**: A SignalR hub hosted in the Web project that manages real-time notification delivery to connected Blazor Server circuits.
- **Notification_Callback_Endpoint**: An internal HTTP endpoint on the Web project that the API project calls after creating a notification, triggering real-time delivery to the target user's circuit.
- **Circuit**: A Blazor Server SignalR connection representing a single user session. Each browser tab maintains one circuit.
- **User_Group**: A SignalR group identified by the user's ID. All circuits belonging to the same authenticated user are members of the same User_Group.
- **NotificationContext**: A per-circuit scoped service in the Web project that caches the unread notification count and raises change events for UI components.
- **NotificationBell**: The topbar component displaying the unread badge count and recent notifications dropdown.
- **Snackbar_Toast**: A transient MudBlazor notification popup that auto-dismisses after a configurable duration.
- **API_Service**: The AspireWebAppTemplate.ApiService project responsible for notification persistence and business logic.
- **Web_Service**: The AspireWebAppTemplate.Web project hosting the Blazor Server frontend and the Notification_Hub.
- **Internal_Auth**: An authentication scheme used for service-to-service communication between the API_Service and Web_Service, preventing external access to internal endpoints.

## Requirements

### Requirement 1: Real-Time Notification Delivery Hub

**User Story:** As a developer using this template, I want a SignalR hub in the Web project that delivers notification events to connected user circuits, so that users receive instant notification updates without page navigation.

#### Acceptance Criteria

1. IF an unauthenticated user attempts to connect to the Notification_Hub, THEN THE Notification_Hub SHALL reject the connection and return an authentication error.
2. WHEN a user establishes a Blazor Server circuit, THE Notification_Hub SHALL add the connection to a User_Group identified by the user's unique ID (the `NameIdentifier` claim value).
3. WHEN a user disconnects from a circuit, THE Notification_Hub SHALL remove the connection from the User_Group.
4. WHEN a user has multiple active circuits (browser tabs), THE Notification_Hub SHALL deliver notification events to all circuits in the User_Group simultaneously.
5. WHEN a new notification is persisted for a user by the NotificationService, THE Notification_Hub SHALL invoke the "ReceiveNotification" method on all clients in the target user's User_Group, transmitting the notification title, category (as a NotificationCategory string value), and the user's updated unread count (integer).
6. IF the target user has zero active circuits at the time a notification is created, THEN THE Notification_Hub SHALL skip real-time delivery without error, relying on the persisted notification for retrieval on next connection.

### Requirement 2: API-to-Web Notification Callback

**User Story:** As a developer using this template, I want the API project to signal the Web project when a notification is created, so that the real-time delivery pipeline is triggered from any notification source.

#### Acceptance Criteria

1. THE Web_Service SHALL expose a Notification_Callback_Endpoint that accepts notification-created events from the API_Service via HTTP POST.
2. THE Notification_Callback_Endpoint SHALL accept the target user ID (non-empty string), notification title (non-empty string, maximum 200 characters), notification category (valid NotificationCategory enum value), and updated unread count (integer >= 0) as parameters in the request body.
3. WHEN the Notification_Callback_Endpoint receives a request where all required parameters are present and valid, THE Web_Service SHALL deliver the notification event to the target user's User_Group via the Notification_Hub and return a 200 OK response.
4. THE Notification_Callback_Endpoint SHALL require Internal_Auth to prevent unauthorized external access.
5. IF the Notification_Callback_Endpoint receives a request for a user with no active circuits, THEN THE Web_Service SHALL return a 200 OK response without delivering any event.
6. WHEN the API_Service successfully creates a notification via INotificationService.CreateNotificationAsync, THE API_Service SHALL call the Notification_Callback_Endpoint with the created notification's target user ID, title, category, and the user's current unread count.
7. IF the callback to the Web_Service fails due to network error, timeout exceeding 5 seconds, or a non-success HTTP response, THEN THE API_Service SHALL log the failure at Warning level and continue without disrupting the primary notification creation operation.
8. IF the Notification_Callback_Endpoint receives a request with missing or malformed parameters, THEN THE Web_Service SHALL return a 400 Bad Request response without delivering any event.

### Requirement 3: Client-Side Real-Time Badge Update

**User Story:** As an application user, I want my notification bell badge to update instantly when a new notification arrives, so that I am always aware of new notifications without refreshing the page.

#### Acceptance Criteria

1. WHEN the NotificationBell component initializes, THE NotificationBell SHALL establish a connection to the Notification_Hub and register a handler for the "ReceiveNotification" event.
2. WHEN the Notification_Hub delivers a "ReceiveNotification" event, THE NotificationContext SHALL replace the cached unread count with the updated unread count value provided in the event payload and raise its OnChange event.
3. WHEN the NotificationContext unread count changes due to a real-time event, THE NotificationBell SHALL re-render the badge using InvokeAsync for thread-safe UI updates, displaying the new numeric count or hiding the badge when the count is zero.
4. WHEN the NotificationBell component is disposed (circuit termination or layout teardown), THE NotificationBell SHALL unsubscribe from the "ReceiveNotification" handler and dispose the Notification_Hub connection.
5. WHEN the Notification_Hub delivers a "ReceiveNotification" event while the notification dropdown is open, THE NotificationBell SHALL prepend the new notification to the cached recent notifications list so the dropdown reflects the latest state without requiring a manual refresh.

### Requirement 4: Snackbar Toast for New Notifications

**User Story:** As an application user, I want to see a brief toast popup when a new notification arrives, so that I notice important events even when not looking at the notification bell.

#### Acceptance Criteria

1. WHEN the Notification_Hub delivers a "ReceiveNotification" event, THE Web_Service SHALL display a Snackbar_Toast containing the notification title.
2. THE Snackbar_Toast SHALL auto-dismiss after 5 seconds.
3. THE Snackbar_Toast SHALL display the notification category icon consistent with the NotificationBell dropdown styling.
4. WHEN the user clicks the Snackbar_Toast, THE Web_Service SHALL navigate the user to the notifications page.
5. WHILE the user's notification preference has InAppEnabled set to false for a category, THE Web_Service SHALL suppress the Snackbar_Toast for notifications of that category.
6. WHEN multiple notifications arrive in quick succession, THE Snackbar_Toast messages SHALL stack following MudBlazor's default snackbar stacking behavior.
7. THE Snackbar_Toast notification title SHALL be truncated to a maximum of 100 characters with ellipsis when the title exceeds this length.

### Requirement 5: Service-to-Service Communication Setup

**User Story:** As a developer using this template, I want the API-to-Web callback to use Aspire service discovery and internal authentication, so that the real-time pipeline integrates cleanly with the existing orchestration infrastructure.

#### Acceptance Criteria

1. THE API_Service SHALL resolve the Web_Service base URL using Aspire service discovery (the "webfrontend" resource reference).
2. THE AppHost SHALL configure the API_Service with a reference to the Web_Service to enable service discovery in the API-to-Web direction.
3. THE API_Service SHALL use a typed HttpClient registered with the Aspire service discovery base address for calling the Notification_Callback_Endpoint.
4. THE Internal_Auth scheme SHALL validate requests using a shared API key transmitted via an HTTP header (e.g., `X-Internal-Api-Key`), configured via Aspire parameters or environment variables.
5. THE Web_Service SHALL reject requests to the Notification_Callback_Endpoint that do not include a valid Internal_Auth credential with a 401 Unauthorized response.
6. THE API_Service typed HttpClient SHALL include the Internal_Auth API key header on all outbound requests to the Web_Service via a delegating handler.
7. THE API_Service callback request SHALL have a timeout of 5 seconds to prevent blocking the notification creation operation.

### Requirement 6: Connection Resilience

**User Story:** As an application user, I want the real-time notification connection to recover gracefully from temporary interruptions, so that I continue receiving updates after network blips.

#### Acceptance Criteria

1. IF the Notification_Hub connection is lost, THEN THE NotificationBell SHALL attempt to reconnect using automatic reconnection with exponential backoff starting at a 1-second delay, doubling on each attempt, up to a maximum of 5 reconnection attempts and a maximum delay of 30 seconds between attempts.
2. WHEN the Notification_Hub connection is re-established after a disconnection, THE NotificationContext SHALL refresh the unread count from the API to reconcile any notifications missed during the disconnection.
3. IF the Notification_Hub connection cannot be re-established after all reconnection attempts are exhausted, THEN THE NotificationBell SHALL abandon the hub connection and fall back to the existing navigation-based badge refresh behavior for the remainder of the page lifecycle.
4. THE Notification_Hub connection failure SHALL NOT disrupt other page functionality or cause unhandled exceptions, error messages, or error UI elements visible to the user.

### Requirement 7: Security and User Isolation

**User Story:** As a developer using this template, I want real-time notification delivery to be strictly scoped to the correct user, so that no user can receive another user's notifications.

#### Acceptance Criteria

1. THE Notification_Hub SHALL associate each connection with the authenticated user's identity from the circuit's authentication state.
2. THE Notification_Hub SHALL deliver notification events only to connections in the User_Group matching the target user ID from the callback payload.
3. THE Notification_Hub SHALL NOT expose any method that allows a client to subscribe to another user's notification events or specify a target user ID.
4. WHEN a user's authentication state changes (logout), THE Notification_Hub SHALL remove the connection from the User_Group.
5. WHEN a user's authentication session expires while connected, THE Notification_Hub SHALL remove the connection from the User_Group and terminate the hub connection.
