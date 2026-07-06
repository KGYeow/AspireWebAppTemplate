# Requirements Document

## Introduction

An announcement and banner system for the AspireWebAppTemplate enterprise application. The system enables administrators to create and manage announcements that are displayed to users through multiple surfaces: a persistent top-of-layout banner for urgent announcements, a dashboard card summarizing all active announcements, a full admin CRUD management page, and a read-only browsable list page for regular users. Announcements support scheduling (start/expiry dates), manual activation, severity levels (Info, Warning, Critical), and two display types (Banner for top-layout display, Standard for card/list only). Announcement content is authored via a TinyMCE WYSIWYG rich text editor and stored as sanitized HTML (using Ganss.Xss.HtmlSanitizer server-side), while titles remain plain text. Banner dismissals are per-user and stored in the database via a join table, ensuring one user's dismissal does not affect other users. The system follows existing patterns: thin controllers with full service layer, per-circuit caching via a scoped AnnouncementContext, typed HttpClient services with Aspire service discovery, and MudBlazor components for the UI.

## Glossary

- **Announcement_Service**: The backend service responsible for all announcement business logic including CRUD operations, activation queries, dismissal management, scheduling evaluation, HTML content sanitization, and notification delivery to users when announcements become active.
- **Announcement_Controller**: The REST API controller that exposes announcement endpoints, extending BaseController. Delegates all logic to Announcement_Service.
- **Announcement_Entity**: The EF Core entity representing a single announcement record stored in the database. Contains Id, Title, Content (sanitized HTML), DisplayType, Severity, StartsAtUtc, ExpiresAtUtc, IsActive, NotifyUsers (bool, default false), CreatedByUserId, CreatedAtUtc, and UpdatedAtUtc.
- **Announcement_Dismissal_Entity**: The EF Core entity representing a per-user dismissal of a specific announcement. Uses a composite key of UserId and AnnouncementId with a DismissedAtUtc timestamp.
- **Announcement_Context**: A per-circuit scoped service in the Web project that loads and caches the current user's active, non-dismissed announcements on circuit initialization. Provides synchronous access for layout components (banner) and page components (dashboard card).
- **Api_Announcement_Service**: The typed HttpClient service in the Web project that communicates with the Announcement_Controller via Aspire service discovery.
- **Top_Banner**: A persistent, dismissible banner component rendered at the top of the MainLayout for displaying the highest-priority active, non-dismissed Banner-type announcement.
- **Dashboard_Card**: A card component on the home/dashboard page that displays a compact list of all active announcements regardless of type or severity.
- **Admin_Management_Page**: The Blazor Server page at `/admin/announcements` providing full CRUD for announcements with status filtering, restricted to Admin role.
- **Announcement_List_Page**: The Blazor Server page at `/announcements` providing a read-only browsable list of active and recently expired announcements for all authenticated users.
- **Topbar_Announcement_Icon**: A persistent icon button in the application topbar (adjacent to the notification bell) that navigates to the Announcement_List_Page. Does not display badges or unread counts — announcements are broadcast content without per-user read tracking.
- **Display_Type**: An enum classifying how an announcement is surfaced. Banner type appears in the top-of-layout banner plus dashboard card and list page. Standard type appears only in the dashboard card and list page.
- **Severity**: An enum indicating announcement urgency: Info, Warning, or Critical. Affects banner styling and priority ordering.
- **TinyMCE**: A WYSIWYG rich text editor used via the TinyMCE.Blazor NuGet package to provide administrators with a rich editing experience for announcement content. Outputs HTML.
- **HtmlSanitizer**: The Ganss.Xss.HtmlSanitizer library used server-side to sanitize HTML content produced by the WYSIWYG editor before persistence, protecting against XSS attacks.

## Requirements

### Requirement 1: Announcement Data Model

**User Story:** As a developer, I want a well-defined data model for announcements and dismissals, so that the system can store and query announcement state efficiently.

#### Acceptance Criteria

1. THE Announcement_Entity SHALL store the following fields: Id (Guid, primary key), Title (string, plain text, max 200 characters), Content (string, sanitized HTML from TinyMCE editor, max 10000 characters), DisplayType (Display_Type enum), Severity (Severity enum), StartsAtUtc (nullable DateTime), ExpiresAtUtc (nullable DateTime), IsActive (bool, default false), NotifyUsers (bool, default false), CreatedByUserId (string, foreign key to ApplicationUser), CreatedAtUtc (DateTime), and UpdatedAtUtc (DateTime).
2. THE Announcement_Dismissal_Entity SHALL store the following fields: UserId (string), AnnouncementId (Guid), and DismissedAtUtc (DateTime), with a composite primary key of UserId and AnnouncementId.
3. THE Announcement_Entity SHALL have a navigation property to the creating ApplicationUser and a collection navigation to Announcement_Dismissal_Entity records.

### Requirement 2: Announcement Activation and Scheduling

**User Story:** As an administrator, I want announcements to support both manual activation and time-based scheduling, so that I can control precisely when announcements become visible to users.

#### Acceptance Criteria

1. THE Announcement_Service SHALL consider an announcement as "active" WHEN IsActive is true AND the current UTC time is on or after StartsAtUtc (or StartsAtUtc is null) AND the current UTC time is before ExpiresAtUtc (or ExpiresAtUtc is null).
2. WHEN StartsAtUtc is null and IsActive is true, THE Announcement_Service SHALL treat the announcement as immediately active with no start constraint.
3. WHEN ExpiresAtUtc is null and IsActive is true, THE Announcement_Service SHALL treat the announcement as active indefinitely with no expiry constraint.
4. WHEN the current UTC time is before StartsAtUtc, THE Announcement_Service SHALL treat the announcement as "scheduled" regardless of the IsActive flag.
5. WHEN the current UTC time is on or after ExpiresAtUtc, THE Announcement_Service SHALL treat the announcement as "expired" regardless of the IsActive flag.

### Requirement 3: Create Announcements (Admin)

**User Story:** As an administrator, I want to create new announcements with title, rich text content, display type, severity, and scheduling options, so that I can communicate important information to all users.

#### Acceptance Criteria

1. WHEN an administrator submits a valid create announcement request, THE Announcement_Service SHALL create an Announcement_Entity with the specified fields and set CreatedAtUtc and UpdatedAtUtc to the current UTC time.
2. THE Announcement_Service SHALL set CreatedByUserId to the authenticated administrator's user ID via ICurrentUserAccessor.
3. IF the Title exceeds 200 characters or the Content exceeds 10000 characters, THEN THE Announcement_Service SHALL reject the request with a descriptive validation error.
4. IF StartsAtUtc is provided and ExpiresAtUtc is provided and StartsAtUtc is on or after ExpiresAtUtc, THEN THE Announcement_Service SHALL reject the request with a descriptive validation error.
5. WHEN persisting a new announcement, THE Announcement_Service SHALL sanitize the Content field using an allowlist-based HTML sanitizer before storage. Allowed tags: p, strong, em, ul, ol, li, a (with href attribute only), h3, h4, br, blockquote. All other tags, attributes, and event handlers SHALL be stripped. The javascript: URI scheme SHALL be removed from href attributes.
6. WHEN NotifyUsers is true and the announcement is immediately active (IsActive=true and StartsAtUtc is null or in the past), THE Announcement_Service SHALL create a notification for each active user in the system using the existing NotificationService.CreateNotificationAsync with Category=System, Title='New Announcement', and Message set to the announcement's Title.

### Requirement 4: Update Announcements (Admin)

**User Story:** As an administrator, I want to edit existing announcements, so that I can correct or update information after publication.

#### Acceptance Criteria

1. WHEN an administrator submits a valid update request for an existing announcement, THE Announcement_Service SHALL update the specified fields and set UpdatedAtUtc to the current UTC time.
2. IF the specified announcement ID does not exist, THEN THE Announcement_Service SHALL throw a KeyNotFoundException.
3. WHEN an administrator updates an announcement and chooses to clear dismissals, THE Announcement_Service SHALL delete all Announcement_Dismissal_Entity records associated with that announcement so that all users see the updated version.
4. THE Announcement_Controller SHALL accept an optional ClearDismissals boolean flag on the update request (default false).
5. WHEN updating an announcement, THE Announcement_Service SHALL sanitize the Content field using an allowlist-based HTML sanitizer before storage. Allowed tags: p, strong, em, ul, ol, li, a (with href attribute only), h3, h4, br, blockquote. All other tags, attributes, and event handlers SHALL be stripped. The javascript: URI scheme SHALL be removed from href attributes.
6. THE Announcement_Controller SHALL accept an optional NotifyUsers boolean flag on the update request (default false). WHEN NotifyUsers is true and the announcement is currently active, THE Announcement_Service SHALL create a notification for each active user with Category=System, Title="Announcement Updated", and Message set to the announcement's Title.

### Requirement 5: Delete Announcements (Admin)

**User Story:** As an administrator, I want to delete announcements that are no longer needed, so that the system remains clean and manageable.

#### Acceptance Criteria

1. WHEN an administrator requests deletion of an announcement, THE Announcement_Service SHALL delete the Announcement_Entity and all associated Announcement_Dismissal_Entity records.
2. IF the specified announcement ID does not exist, THEN THE Announcement_Service SHALL throw a KeyNotFoundException.

### Requirement 6: List Announcements for Admin Management

**User Story:** As an administrator, I want to view all announcements with status filtering, so that I can manage the full lifecycle of announcements.

#### Acceptance Criteria

1. WHEN an administrator requests the announcement list, THE Announcement_Service SHALL return all announcements ordered by CreatedAtUtc descending.
2. WHERE the administrator provides a status filter of "Active", THE Announcement_Service SHALL return only announcements that satisfy the active criteria defined in Requirement 2.
3. WHERE the administrator provides a status filter of "Scheduled", THE Announcement_Service SHALL return only announcements where IsActive is true and StartsAtUtc is in the future.
4. WHERE the administrator provides a status filter of "Expired", THE Announcement_Service SHALL return only announcements where ExpiresAtUtc is in the past.
5. WHERE the administrator provides a status filter of "Draft", THE Announcement_Service SHALL return only announcements where IsActive is false and the announcement is not expired.

### Requirement 7: Top Banner Display

**User Story:** As an authenticated user, I want to see urgent announcements in a persistent banner at the top of the page, so that I do not miss critical information.

#### Acceptance Criteria

1. THE Top_Banner SHALL display the single highest-priority active, non-dismissed, Banner-type announcement for the current user.
2. THE Top_Banner SHALL determine priority by Severity (Critical > Warning > Info) and, when severity is tied, by most recent CreatedAtUtc.
3. WHILE no active, non-dismissed, Banner-type announcements exist for the current user, THE Top_Banner SHALL render nothing (no empty space or placeholder).
4. WHILE multiple active, non-dismissed, Banner-type announcements exist, THE Top_Banner SHALL display a "N more" link (where N is the count of remaining announcements minus one) that navigates to the Announcement_List_Page.
5. THE Top_Banner SHALL apply distinct visual styling based on the displayed announcement's Severity: Info (blue/neutral), Warning (amber/orange), Critical (red).
6. THE Top_Banner SHALL display the announcement Title (plain text) and a plain-text excerpt derived by stripping HTML tags from the Content field and truncating to 150 characters.

### Requirement 8: Banner Dismissal

**User Story:** As an authenticated user, I want to dismiss the banner announcement so that it no longer interrupts my workflow, while remaining visible to other users.

#### Acceptance Criteria

1. WHEN a user clicks the dismiss button on the Top_Banner, THE Announcement_Service SHALL create an Announcement_Dismissal_Entity record with the user's ID, the announcement ID, and the current UTC time.
2. WHEN a user dismisses a banner announcement, THE Announcement_Context SHALL update its cached state to reflect the dismissal and the Top_Banner SHALL immediately show the next highest-priority announcement or hide if none remain.
3. THE Announcement_Service SHALL treat an announcement as dismissed for a user WHEN an Announcement_Dismissal_Entity record exists for that user-announcement pair.
4. IF a user attempts to dismiss an announcement that is already dismissed, THEN THE Announcement_Service SHALL complete successfully without creating a duplicate record.

### Requirement 9: Dashboard Card

**User Story:** As an authenticated user, I want to see a summary of all active announcements on the dashboard, so that I have a centralized reference even for announcements I dismissed from the banner.

#### Acceptance Criteria

1. THE Dashboard_Card SHALL display a compact list of ALL active announcements (both Banner and Standard display types, all severity levels) regardless of dismissal status.
2. THE Dashboard_Card SHALL display each announcement with its Title (plain text), a brief plain-text excerpt (derived by stripping HTML tags from the Content field), Severity indicator (icon or color), and relative timestamp.
3. THE Dashboard_Card SHALL display a "View all" link that navigates to the Announcement_List_Page.
4. WHILE no active announcements exist, THE Dashboard_Card SHALL display a message indicating there are no current announcements.
5. THE Dashboard_Card SHALL order announcements by Severity descending (Critical first) then by CreatedAtUtc descending.

### Requirement 10: Announcement List Page

**User Story:** As an authenticated user, I want a dedicated page to browse all active and recently expired announcements in a master-detail layout, so that I can scan announcements quickly and read full rich content without excessive scrolling.

#### Acceptance Criteria

1. THE Announcement_List_Page SHALL be accessible at the route `/announcements` and require authentication.
2. THE Announcement_List_Page SHALL use a master-detail layout with a scrollable list pane on the left and a detail pane on the right.
3. THE left pane SHALL display all active announcements and announcements that expired within the last 30 days, sorted by CreatedAtUtc descending.
4. THE left pane SHALL display each announcement list item with: Severity indicator (colored chip), Title (truncated to one line), plain-text Content snippet (HTML tags stripped, truncated to 2 lines), relative timestamp, and an "Expired" label for expired items.
5. THE left pane SHALL visually distinguish active announcements from expired announcements using dimmed styling for expired items.
6. WHEN a user selects an announcement from the left pane, THE detail pane SHALL display the full announcement including: Title (untruncated), Severity colored chip (no label prefix), published date with relative time, an "Expired" chip with expiry date for expired items, and the full sanitized HTML Content rendered using MarkupString. THE detail pane SHALL NOT display the Display_Type — it is admin-only metadata.
7. THE selected announcement in the left pane SHALL be visually indicated using the existing notification-selected pattern (background highlight with left bar indicator).
8. WHILE no announcement is selected, THE detail pane SHALL display a placeholder message such as "Select an announcement to view details."
9. THE Announcement_List_Page SHALL use the PageContent loading wrapper during initial data fetch.

### Requirement 11: Admin Management Page

**User Story:** As an administrator, I want a full management interface for announcements, so that I can create, edit, activate, and delete announcements efficiently.

#### Acceptance Criteria

1. THE Admin_Management_Page SHALL be accessible at the route `/admin/announcements` and require the Admin role.
2. THE Admin_Management_Page SHALL display announcements in a MudDataGrid with columns for Title, Display_Type, Severity, Status (computed: Active/Scheduled/Expired/Draft), StartsAtUtc, ExpiresAtUtc, and CreatedAtUtc.
3. THE Admin_Management_Page SHALL provide filter chips for status filtering (All, Active, Scheduled, Expired, Draft).
4. THE Admin_Management_Page SHALL provide a "Create Announcement" button that opens a MudDialog with fields: Title (plain text input, required), Content (TinyMCE WYSIWYG editor via TinyMCE.Blazor NuGet package, required), Display_Type (select), Severity (select), StartsAtUtc (datetime picker, optional), ExpiresAtUtc (datetime picker, optional), IsActive (toggle), and NotifyUsers (toggle). The TinyMCE editor toolbar SHALL be limited to: bold, italic, underline, headings (h3/h4), bullet list, numbered list, blockquote, and hyperlink insertion.
5. THE Admin_Management_Page SHALL provide an "Edit" action per row that opens the same dialog pre-populated with the announcement's current values, including a ClearDismissals checkbox.
6. THE Admin_Management_Page SHALL provide a "Delete" action per row that shows a ConfirmationDialog before proceeding with deletion.
7. WHEN a create or edit operation succeeds, THE Admin_Management_Page SHALL display a success Snackbar and refresh the grid data.
8. IF a create or edit operation fails validation, THEN THE Admin_Management_Page SHALL display the validation error in the dialog without closing it.
9. THE create/edit dialog SHALL include a NotifyUsers toggle (default: true for Standard display type, false for Banner display type) that controls whether user notifications are sent when the announcement becomes active.

### Requirement 12: Announcement Context (Per-Circuit Caching)

**User Story:** As a developer, I want announcement state cached per circuit, so that layout components can access current announcements synchronously without repeated API calls.

#### Acceptance Criteria

1. THE Announcement_Context SHALL load active, non-dismissed announcements for the current user on circuit initialization via the Api_Announcement_Service.
2. THE Announcement_Context SHALL provide synchronous access to the list of active Banner-type announcements (for Top_Banner rendering) and all active announcements (for Dashboard_Card rendering).
3. WHEN a user dismisses an announcement via the Top_Banner, THE Announcement_Context SHALL remove it from the cached Banner-type list and notify subscribers of the state change.
4. THE Announcement_Context SHALL expose an event or callback mechanism so that the Top_Banner and Dashboard_Card components can re-render when cached state changes.
5. THE Announcement_Context SHALL separate the announcement list into two views: Banner-type announcements filtered by non-dismissed status (for the Top_Banner), and all active announcements regardless of dismissal (for the Dashboard_Card).

### Requirement 13: API Endpoints

**User Story:** As a developer, I want well-defined REST API endpoints for announcements, so that the Web project can communicate with the API service using the established typed HttpClient pattern.

#### Acceptance Criteria

1. THE Announcement_Controller SHALL expose a GET endpoint for retrieving active announcements for the current user (filtered by non-dismissed for banner, all active for dashboard/list), accessible to all authenticated users.
2. THE Announcement_Controller SHALL expose a GET endpoint for retrieving all announcements with optional status filter, accessible only to Admin role.
3. THE Announcement_Controller SHALL expose a POST endpoint for creating announcements, accessible only to Admin role.
4. THE Announcement_Controller SHALL expose a PUT endpoint for updating announcements (with optional ClearDismissals flag), accessible only to Admin role.
5. THE Announcement_Controller SHALL expose a DELETE endpoint for deleting announcements, accessible only to Admin role.
6. THE Announcement_Controller SHALL expose a POST endpoint for dismissing an announcement for the current user, accessible to all authenticated users.
7. THE Announcement_Controller SHALL expose a GET endpoint for retrieving active and recently expired announcements for the list page, accessible to all authenticated users.

### Requirement 14: HTML Content Sanitization

**User Story:** As a developer, I want all announcement HTML content sanitized server-side before storage, so that the system is protected from XSS attacks regardless of input source.

#### Acceptance Criteria

1. THE Announcement_Service SHALL sanitize the Content field using an instance of Ganss.Xss.HtmlSanitizer configured with an allowlist of permitted tags and attributes.
2. THE Announcement_Service SHALL perform HTML sanitization in the service layer (not in the controller) to ensure all code paths that persist content are protected.
3. THE HtmlSanitizer SHALL allow the following HTML tags: p, strong, em, ul, ol, li, a, h3, h4, br, blockquote. THE HtmlSanitizer SHALL allow the href attribute only on the a tag.
4. THE HtmlSanitizer SHALL strip all script tags, iframe tags, style tags, event handler attributes (onclick, onerror, onload, etc.), and any tags or attributes not in the allowlist.
5. THE HtmlSanitizer SHALL remove the javascript: URI scheme from href attributes to prevent script execution via links.
6. THE Announcement_Service SHALL apply HTML sanitization on both the create and update code paths before persisting the Announcement_Entity to the database.

### Requirement 15: Topbar Announcement Navigation Icon

**User Story:** As an authenticated user, I want a persistent announcement icon in the topbar, so that I can navigate to the announcements page from anywhere in the application.

#### Acceptance Criteria

1. THE Topbar_Announcement_Icon SHALL display a campaign/megaphone icon button positioned adjacent to the existing notification bell icon.
2. WHEN the user clicks the announcement icon, THE application SHALL navigate to the Announcement_List_Page at `/announcements`.
3. THE Topbar_Announcement_Icon SHALL NOT display any badge, count, or unread indicator. Announcements are broadcast content and do not track per-user read status.
4. THE Topbar_Announcement_Icon SHALL be visible to all authenticated users.
5. THE Topbar_Announcement_Icon SHALL use a Material icon (e.g., `Campaign` or `Announcement`) that visually distinguishes it from the notification bell.

### Requirement 16: Notification Integration

**User Story:** As a user, I want to receive a notification when a new announcement is published, so that I am aware of important updates without needing to manually check the announcements page.

#### Acceptance Criteria

1. WHEN an announcement with NotifyUsers=true transitions to active status (either immediately on creation or when a scheduled StartsAtUtc time arrives), THE Announcement_Service SHALL create one notification per active user using CreateNotificationAsync.
2. THE notification for a newly created announcement SHALL use Category=NotificationCategory.System, Title="New Announcement", and Message set to the announcement's Title field.
3. THE notification for an updated announcement SHALL use Category=NotificationCategory.System, Title="Announcement Updated", and Message set to the announcement's Title field.
4. THE notification's deep-link navigation (via the existing snackbar onclick mechanism) SHALL navigate to `/announcements?id={announcementId}` where announcementId is the announcement's Id.
5. THE Announcement_Service SHALL NOT create notifications for Banner-type announcements by default (NotifyUsers defaults to false for Banner type). Administrators MAY override this default.
6. THE Announcement_Service SHALL NOT create notifications for Standard-type announcements when NotifyUsers is explicitly set to false by the administrator.
7. IF the notification creation for any individual user fails, THE Announcement_Service SHALL log the failure and continue with remaining users. Notification failures SHALL NOT prevent the announcement from being created, updated, or activated.
