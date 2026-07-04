# Requirements Document

## Introduction

This feature enhances the real-time notification popup experience by replacing the plain-text snackbar with a rich custom component rendered inside MudBlazor's snackbar system. Notification popups display a category icon, bold title, and message body in the top-right corner, visually distinct from action feedback snackbars that remain at bottom-center. The enhancement uses MudBlazor's `ISnackbar.Add<TComponent>()` API with per-snackbar `PositionClass` configuration, requiring no changes to the global `MudSnackbarProvider` setup.

## Glossary

- **Notification_Snackbar**: A MudBlazor snackbar instance that renders a custom Blazor component (`NotificationSnackbarContent`) to display incoming real-time notifications with rich formatting.
- **Action_Feedback_Snackbar**: A standard plain-text MudBlazor snackbar used for transient user action confirmations (e.g., "Profile saved", "Settings updated") positioned at bottom-center.
- **NotificationBell**: The topbar Blazor component that subscribes to `INotificationContext.OnNotificationReceived` and triggers notification snackbar display.
- **NotificationSnackbarContent**: A custom Blazor component rendered inside the Notification_Snackbar, displaying category icon, title, and message.
- **NotificationContext**: A per-circuit scoped service managing notification state, hub connection, and the `NotificationPopupsEnabled` user preference.
- **Category**: A classification of notifications (Account, Activity, System) that determines which icon and color are displayed.
- **PositionClass**: A MudBlazor per-snackbar configuration property that controls where an individual snackbar appears on screen, independent of the global provider position.

## Requirements

### Requirement 1: Notification Snackbar Positioning

**User Story:** As a user, I want notification popups to appear in the top-right corner, so that I can distinguish them from action feedback messages that appear at bottom-center.

#### Acceptance Criteria

1. WHEN a real-time notification is received, THE NotificationBell SHALL display the Notification_Snackbar with `PositionClass` set to `Defaults.Classes.Position.TopRight`.
2. THE Action_Feedback_Snackbar SHALL continue to use the default MudBlazor position (bottom-center) without any configuration change.
3. THE Notification_Snackbar SHALL appear in a spatially distinct location from Action_Feedback_Snackbar, ensuring the user can differentiate notification popups from action confirmations.

### Requirement 2: Rich Content Custom Component

**User Story:** As a user, I want notification popups to show a category icon, title, and message, so that I can quickly understand the notification context without opening the notifications page.

#### Acceptance Criteria

1. WHEN a real-time notification is received, THE NotificationBell SHALL render the Notification_Snackbar using `ISnackbar.Add<NotificationSnackbarContent>()` with the notification title, message, and category passed as component parameters.
2. THE NotificationSnackbarContent SHALL display a `MudAvatar` containing the category icon with the corresponding category color.
3. THE NotificationSnackbarContent SHALL display the notification title using `Typo.body2` with bold font weight.
4. THE NotificationSnackbarContent SHALL display the notification message using `Typo.caption`.
5. THE NotificationSnackbarContent SHALL arrange the icon, title, and message in a horizontal layout with the avatar on the left and text stacked vertically on the right.

### Requirement 3: Category Icon and Color Mapping

**User Story:** As a user, I want notification popup icons to match the icons in the notification dropdown, so that the visual language is consistent across the application.

#### Acceptance Criteria

1. WHEN the Category is "Account", THE NotificationSnackbarContent SHALL display the `Icons.Material.Outlined.Security` icon with `Color.Error`.
2. WHEN the Category is "Activity", THE NotificationSnackbarContent SHALL display the `Icons.Material.Outlined.People` icon with `Color.Primary`.
3. WHEN the Category is "System", THE NotificationSnackbarContent SHALL display the `Icons.Material.Outlined.Info` icon with `Color.Info`.
4. IF the Category does not match any known value, THEN THE NotificationSnackbarContent SHALL display the `Icons.Material.Outlined.Notifications` icon with `Color.Default`.

### Requirement 4: Auto-Dismiss Timing

**User Story:** As a user, I want notification popups to disappear automatically after a reasonable time, so that they do not block my view indefinitely.

#### Acceptance Criteria

1. WHEN a Notification_Snackbar is displayed, THE NotificationBell SHALL configure `VisibleStateDuration` to 5000 milliseconds.
2. WHEN the VisibleStateDuration elapses, THE Notification_Snackbar SHALL dismiss automatically without user interaction.

### Requirement 5: Click-to-Navigate

**User Story:** As a user, I want to click a notification popup to navigate to the notifications page, so that I can view full notification details.

#### Acceptance Criteria

1. WHEN the user clicks the Notification_Snackbar, THE NotificationSnackbarContent SHALL navigate to `/account/notifications`.
2. WHEN the user clicks the Notification_Snackbar, THE Notification_Snackbar SHALL close after initiating navigation.

### Requirement 6: User Preference Suppression

**User Story:** As a user, I want to control whether notification popups appear, so that I can disable them if I find them distracting.

#### Acceptance Criteria

1. WHILE `NotificationContext.NotificationPopupsEnabled` is false, THE NotificationBell SHALL suppress all Notification_Snackbar display.
2. WHILE `NotificationContext.NotificationPopupsEnabled` is true, THE NotificationBell SHALL display the Notification_Snackbar for each received real-time notification.
3. THE NotificationBell SHALL evaluate the `NotificationPopupsEnabled` preference at the time each notification is received, reflecting any preference change made during the current session.

### Requirement 7: Text Truncation

**User Story:** As a user, I want notification popup text to be truncated when too long, so that the popup remains compact and readable.

#### Acceptance Criteria

1. WHEN the notification title exceeds 100 characters, THE NotificationSnackbarContent SHALL truncate the title to 100 characters and append an ellipsis character ("…").
2. WHEN the notification title is 100 characters or fewer, THE NotificationSnackbarContent SHALL display the full title without modification.
3. WHEN the notification message exceeds 200 characters, THE NotificationSnackbarContent SHALL truncate the message to 200 characters and append an ellipsis character ("…").
4. WHEN the notification message is 200 characters or fewer, THE NotificationSnackbarContent SHALL display the full message without modification.

### Requirement 8: Visual Distinction from Action Feedback

**User Story:** As a user, I want notification popups to look visually different from action feedback snackbars, so that I can immediately identify what type of message is being shown.

#### Acceptance Criteria

1. THE Notification_Snackbar SHALL render using a multi-element layout (avatar icon + title + message) that is visually distinct from the single-line text format of the Action_Feedback_Snackbar.
2. THE Notification_Snackbar SHALL use a card-like structure with the category-colored avatar, a bold title line, and a secondary caption line.
3. THE Action_Feedback_Snackbar SHALL remain unchanged as a single line of plain text with a severity icon.

### Requirement 9: No Global Snackbar Configuration Change

**User Story:** As a developer, I want notification popups to use per-snackbar configuration only, so that existing action feedback snackbars throughout the application remain unaffected.

#### Acceptance Criteria

1. THE MudSnackbarProvider configuration in `MainLayout.razor` SHALL remain unchanged with no additional attributes or global position overrides.
2. THE Notification_Snackbar SHALL achieve top-right positioning exclusively through the per-snackbar `SnackbarOptions.PositionClass` property set during the `ISnackbar.Add<TComponent>()` call.
3. IF a new Action_Feedback_Snackbar is added elsewhere in the application using `Snackbar.Add(message, severity)`, THEN THE Action_Feedback_Snackbar SHALL appear at the default MudBlazor position without requiring any additional configuration.
