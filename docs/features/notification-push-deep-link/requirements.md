# Requirements Document

## Introduction

Extend the real-time notification push pipeline to include the notification ID (Guid) in all layers—from API creation through SignalR delivery to the browser—so that the snackbar popup's click handler can navigate the user directly to `/account/notifications?id={notificationId}` for inline expansion of the specific notification.

## Glossary

- **Push_Pipeline**: The end-to-end flow that delivers a newly created notification from the API service to the user's browser in real time. Comprises NotificationPushRequest, WebCallbackClient, NotificationCallbackEndpoint, NotificationHub, and NotificationContext.
- **NotificationPushRequest**: The DTO sent from the API service to the Web project's internal callback endpoint, carrying the data needed for real-time delivery.
- **NotificationCallbackEndpoint**: The internal minimal API endpoint in the Web project that receives push requests and forwards them to SignalR.
- **NotificationHub**: The SignalR hub that delivers notification events to connected user circuits.
- **NotificationContext**: The per-circuit scoped service that manages the SignalR hub connection and raises events for UI components.
- **NotificationReceivedEventArgs**: A class in the Web project that bundles notification event data (title, message, category, notificationId) for the OnNotificationReceived event.
- **NotificationBell**: The topbar Blazor component that displays the notification badge, dropdown, and snackbar toasts.
- **Snackbar_Toast**: The MudBlazor snackbar popup shown when a new notification arrives in real time.
- **Deep_Link_URL**: The navigation URL `/account/notifications?id={notificationId}` that identifies a specific notification for inline expansion on the notifications page.

## Requirements

### Requirement 1

**User Story:** As a developer extending the push pipeline, I want the NotificationPushRequest to carry the notification ID so that downstream components can associate the real-time event with a specific persisted notification.

#### Acceptance Criteria

1. THE NotificationPushRequest SHALL contain a NotificationId property of type Guid.
2. WHEN the API service creates a notification and builds the push request, THE NotificationService SHALL set the NotificationId property to the persisted notification entity's Id value.
3. IF the NotificationId in an incoming push request is Guid.Empty, THEN THE NotificationCallbackEndpoint SHALL return a 400 Bad Request response with the message "NotificationId is required."

### Requirement 2

**User Story:** As a developer extending the push pipeline, I want the SignalR hub invocation to include the notification ID so that client-side handlers can use it for navigation.

#### Acceptance Criteria

1. WHEN the NotificationCallbackEndpoint receives a valid push request, THE NotificationCallbackEndpoint SHALL invoke the SignalR "ReceiveNotification" method with five parameters: title, message, category, unreadCount, and notificationId (Guid).
2. THE NotificationContext SHALL register a handler for the "ReceiveNotification" hub method that accepts five parameters: title (string), message (string), category (string), unreadCount (int), and notificationId (Guid).

### Requirement 3

**User Story:** As a developer, I want a strongly-typed event args class for notification-received events so that the event signature is extensible and type-safe.

#### Acceptance Criteria

1. THE NotificationReceivedEventArgs SHALL expose four properties: Title (string), Message (string), Category (string), and NotificationId (Guid).
2. THE INotificationContext interface SHALL declare the OnNotificationReceived event with the signature Action<NotificationReceivedEventArgs>.
3. WHEN the NotificationContext receives a "ReceiveNotification" hub event, THE NotificationContext SHALL raise the OnNotificationReceived event with a NotificationReceivedEventArgs instance populated from the hub parameters.

### Requirement 4

**User Story:** As a user, I want to click the snackbar toast popup and navigate directly to the specific notification so that I can read its details without searching for it.

#### Acceptance Criteria

1. WHEN a new notification arrives and the snackbar toast is displayed, THE NotificationBell SHALL configure the snackbar's Onclick handler to navigate to `/account/notifications?id={notificationId}` where notificationId is the Guid received in the event args.
2. THE NotificationBell SHALL update the HandleNotificationReceived method signature to accept NotificationReceivedEventArgs and extract all fields from the event args object.
3. WHEN the snackbar Onclick handler triggers navigation, THE NotificationBell SHALL use the NavigationManager to perform the navigation to the Deep_Link_URL.
