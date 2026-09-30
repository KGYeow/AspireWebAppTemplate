# Requirements Document

## Introduction

An in-app notification system for the AspireWebAppTemplate enterprise application. The feature provides a notification bell in the topbar with unread count badge, a full notification management page at `/notifications` using a list/feed layout, and notification preferences integrated into the existing Settings page. Users can mark notifications as read individually or dismiss them in bulk. The system follows existing patterns: API controllers extending BaseController, scoped services, per-circuit caching for real-time state, typed HttpClient services with Aspire service discovery, and MudBlazor components for the UI.

## Glossary

- **Notification_Service**: The backend service responsible for creating, retrieving, updating, and deleting notification records in the database.
- **Notification_Controller**: The REST API controller that exposes notification endpoints, extending BaseController.
- **Notification_Bell**: The MudIconButton with MudBadge component in the topbar that displays the unread notification count and opens a dropdown preview.
- **Notification_Page**: The Blazor Server page at `/notifications` that displays all notifications in a list/feed layout with filtering and bulk actions.
- **Notification_Context**: A per-circuit scoped service that caches the current user's unread notification count for synchronous O(1) access from layout components.
- **Notification_Entity**: The EF Core entity representing a single notification record stored in the database.
- **Notification_Preference_Entity**: The EF Core entity representing a user's delivery preference for a specific notification category.
- **Notification_Category**: A classification of notifications (e.g., Security, UserManagement, System) used to group preferences and filter the notification list.
- **Api_Notification_Service**: The typed HttpClient service in the Web project that communicates with the Notification_Controller via Aspire service discovery.

## Requirements

### Requirement 1: Create Notifications

**User Story:** As a system component, I want to create notifications for users when significant events occur, so that users are informed about actions relevant to them.

#### Acceptance Criteria

1. WHEN a significant event occurs (user role changed, account activated/deactivated, password reset, permissions updated), THE Notification_Service SHALL create a Notification_Entity with the target user ID, category, title, message, and UTC timestamp.
2. THE Notification_Entity SHALL store the following fields: Id (Guid), UserId (string), Category (Notification_Category enum), Title (string, max 256 characters), Message (string, max 1024 characters), IsRead (bool, default false), CreatedAt (DateTime UTC), and ReadAt (nullable DateTime UTC).
3. IF the target user ID does not correspond to an existing user, THEN THE Notification_Service SHALL discard the notification without throwing an exception.

### Requirement 2: Retrieve Notifications

**User Story:** As an authenticated user, I want to retrieve my notifications with filtering and pagination, so that I can review past and current notifications efficiently.

#### Acceptance Criteria

1. WHEN an authenticated user requests their notifications, THE Notification_Controller SHALL return a paginated list of Notification_Entity records belonging to that user, ordered by CreatedAt descending.
2. WHERE the user provides a category filter, THE Notification_Controller SHALL return only notifications matching the specified Notification_Category.
3. WHERE the user provides a read status filter, THE Notification_Controller SHALL return only notifications matching the specified IsRead value.
4. THE Notification_Controller SHALL support page-based pagination with configurable page size (default 20, maximum 100).

### Requirement 3: Unread Notification Count

**User Story:** As an authenticated user, I want to see how many unread notifications I have at a glance, so that I know when new notifications arrive.

#### Acceptance Criteria

1. WHEN an authenticated user requests their unread count, THE Notification_Controller SHALL return the total number of Notification_Entity records where IsRead is false for that user.
2. THE Notification_Context SHALL cache the unread count per circuit and provide a synchronous accessor for layout components.
3. WHEN the Notification_Context is initialized for a new circuit, THE Notification_Context SHALL load the unread count from the Api_Notification_Service.
4. WHEN a user marks notifications as read or dismisses notifications on the Notification_Page, THE Notification_Context SHALL update its cached count to reflect the change.

### Requirement 4: Mark Notifications as Read

**User Story:** As an authenticated user, I want to mark individual notifications as read, so that I can track which notifications I have already reviewed.

#### Acceptance Criteria

1. WHEN an authenticated user marks a notification as read, THE Notification_Service SHALL set the IsRead field to true and the ReadAt field to the current UTC timestamp on the specified Notification_Entity.
2. IF the specified notification does not belong to the authenticated user, THEN THE Notification_Controller SHALL return a Not Found response.
3. IF the specified notification is already marked as read, THEN THE Notification_Service SHALL complete successfully without modifying the record.

### Requirement 5: Bulk Dismiss Notifications

**User Story:** As an authenticated user, I want to dismiss multiple notifications at once, so that I can efficiently clear my notification list.

#### Acceptance Criteria

1. WHEN an authenticated user submits a bulk dismiss request with a list of notification IDs, THE Notification_Service SHALL delete the specified Notification_Entity records belonging to that user.
2. THE Notification_Controller SHALL ignore any IDs in the bulk dismiss request that do not belong to the authenticated user or do not exist.
3. THE Notification_Controller SHALL accept a maximum of 100 notification IDs per bulk dismiss request.
4. IF the bulk dismiss request contains more than 100 IDs, THEN THE Notification_Controller SHALL return a Bad Request response with a descriptive error message.

### Requirement 6: Mark All Notifications as Read

**User Story:** As an authenticated user, I want to mark all my unread notifications as read with a single action, so that I can quickly clear my unread count.

#### Acceptance Criteria

1. WHEN an authenticated user requests to mark all notifications as read, THE Notification_Service SHALL set IsRead to true and ReadAt to the current UTC timestamp on all Notification_Entity records where IsRead is false for that user.
2. THE Notification_Controller SHALL return the count of notifications that were updated.

### Requirement 7: Notification Bell in Topbar

**User Story:** As an authenticated user, I want to see a notification bell icon with an unread count badge in the topbar, so that I am always aware of pending notifications.

#### Acceptance Criteria

1. THE Notification_Bell SHALL display a bell icon (MudBlazor Icons.Material.Outlined.Notifications) in the topbar RightContent area, positioned before the profile dropdown.
2. WHILE the user has one or more unread notifications, THE Notification_Bell SHALL display a MudBadge with the unread count overlaid on the bell icon.
3. WHILE the user has zero unread notifications, THE Notification_Bell SHALL display the bell icon without a badge.
4. WHEN the user clicks the Notification_Bell, THE Notification_Bell SHALL open a dropdown (MudPopover or MudMenu) showing the five most recent notifications with title, category icon, and relative timestamp (e.g., "2 hours ago").
5. WHEN the user clicks "View All" in the dropdown, THE Notification_Bell SHALL navigate the user to the `/notifications` page.
6. WHEN the user clicks an individual notification in the dropdown, THE Notification_Bell SHALL mark that notification as read and navigate to the `/notifications` page.

### Requirement 8: Notification Page

**User Story:** As an authenticated user, I want a dedicated notifications page where I can view, filter, and manage all my notifications, so that I have full control over my notification history.

#### Acceptance Criteria

1. THE Notification_Page SHALL be accessible at the route `/notifications` and require authentication.
2. THE Notification_Page SHALL display notifications in a list/feed layout (MudList or custom card list), with each item showing: category icon, title (bold if unread), message preview, and relative timestamp.
3. THE Notification_Page SHALL visually distinguish unread notifications from read ones using bold text and a subtle background highlight on unread items.
4. THE Notification_Page SHALL provide toolbar filter chips for filtering by category and by read status (All, Unread, Read).
5. THE Notification_Page SHALL support infinite scroll or "Load More" pagination to load additional notifications as the user scrolls down.
6. THE Notification_Page SHALL provide a "Mark as Read" action on individual unread notification items (via click or action button).
7. THE Notification_Page SHALL provide a "Dismiss" action on individual notification items (via swipe gesture or action button).
8. THE Notification_Page SHALL provide a bulk dismiss action via selection checkboxes and a "Dismiss Selected" toolbar button.
9. THE Notification_Page SHALL provide a "Mark All as Read" button in the toolbar that marks all unread notifications as read.
10. THE Notification_Page SHALL use the PageContent loading wrapper during initial data fetch.

### Requirement 9: Notification Preferences in Settings Page

**User Story:** As an authenticated user, I want to configure which notification categories I receive and through which channels, so that I only get notifications relevant to me.

#### Acceptance Criteria

1. THE Settings page SHALL include a "Notifications" section (below the existing Appearance section) displaying all Notification_Category values with toggle switches for each delivery channel (In-App, Email).
2. WHEN a user toggles a delivery channel for a category, THE Settings page SHALL persist the change immediately using the instant-save pattern consistent with the existing theme and timezone preferences.
3. THE Notification_Preference_Entity SHALL store: Id (Guid), UserId (string), Category (Notification_Category enum), InAppEnabled (bool, default true), and EmailEnabled (bool, default true).
4. WHEN no preference record exists for a user-category pair, THE Notification_Service SHALL treat the defaults as both InAppEnabled and EmailEnabled set to true.
5. WHEN a notification is created and the target user has InAppEnabled set to false for that category, THE Notification_Service SHALL skip creating the in-app Notification_Entity for that user.
6. IF the instant-save fails, THE Settings page SHALL revert the toggle to its previous state and display a Snackbar error message (consistent with existing preference save failure behavior).
