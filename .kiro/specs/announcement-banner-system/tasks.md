# Implementation Plan: Announcement Banner System

## Overview

Implements a multi-surface announcement system with admin CRUD, persistent top banner, dashboard card, dedicated list page, per-user dismissals, scheduling, HTML sanitization via Ganss.Xss, TinyMCE rich text editing, and notification integration. Follows the existing thin-controller/full-service pattern with per-circuit caching via a scoped AnnouncementContext.

## Tasks

- [x] 1. Set up data model, enums, and DTOs
  - [x] 1.1 Create enums and DTO contracts in Core project
    - Create `Core/Domain/Enums/AnnouncementDisplayType.cs` (Banner, Standard)
    - Create `Core/Domain/Enums/AnnouncementSeverity.cs` (Info, Warning, Critical)
    - Create `Core/Contracts/Announcements/AnnouncementDto.cs` with all fields including computed Status
    - Create `Core/Contracts/Announcements/CreateAnnouncementRequest.cs`
    - Create `Core/Contracts/Announcements/UpdateAnnouncementRequest.cs` (includes ClearDismissals and NotifyUsers flags)
    - Create `Core/Contracts/Announcements/AnnouncementStatusFilter.cs` enum (All, Active, Scheduled, Expired, Draft)
    - _Requirements: 1.1, 1.2, 4.4, 4.6_

  - [x] 1.2 Create EF Core entities and configure DbContext
    - Create `ApiService/Data/Entities/Announcement.cs` with all fields (Id, Title, Content, DisplayType, Severity, StartsAtUtc, ExpiresAtUtc, IsActive, NotifyUsers, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc)
    - Create `ApiService/Data/Entities/AnnouncementDismissal.cs` with composite key (UserId, AnnouncementId, DismissedAtUtc)
    - Add navigation properties: Announcement → CreatedByUser (ApplicationUser), Announcement → Dismissals collection
    - Configure entity in `ApplicationDbContext` with composite index on (IsActive, StartsAtUtc, ExpiresAtUtc), index on CreatedAtUtc, cascade delete for dismissals, restrict delete for CreatedByUser
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 1.3 Create EF Core migration for Announcements and AnnouncementDismissals tables
    - Run `dotnet ef migrations add AddAnnouncementSystem` in ApiService project
    - Verify migration creates both tables with correct schema, indexes, and relationships
    - _Requirements: 1.1, 1.2, 1.3_

- [x] 2. Implement AnnouncementService (backend business logic)
  - [x] 2.1 Create IAnnouncementService interface and service skeleton
    - Create `ApiService/Abstractions/IAnnouncementService.cs` with all method signatures (CreateAsync, UpdateAsync, DeleteAsync, GetAllAsync, GetActiveForUserAsync, GetForListPageAsync, DismissAsync)
    - Create `ApiService/Services/AnnouncementService.cs` with constructor injecting ApplicationDbContext, ICurrentUserAccessor, IAuditLogService, INotificationService, ILogger, and HtmlSanitizer
    - Register AnnouncementService as scoped in `ApiService/Extensions/ApplicationServiceExtensions.cs`
    - Register HtmlSanitizer (Ganss.Xss) as singleton with allowlist configuration (p, strong, em, ul, ol, li, a[href], h3, h4, br, blockquote)
    - _Requirements: 14.1, 14.2, 14.3_

  - [x] 2.2 Implement status computation and query helpers
    - Implement `ComputeStatus(announcement, utcNow)` method: Expired if ExpiresAtUtc non-null and now >= ExpiresAtUtc; Scheduled if StartsAtUtc non-null and now < StartsAtUtc; Active if IsActive true; else Draft
    - Implement priority ordering helper: Severity descending (Critical > Warning > Info), then CreatedAtUtc descending
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 2.3 Write property test for status classification (Property 1)
    - **Property 1: Status classification is consistent with IsActive, StartsAtUtc, and ExpiresAtUtc**
    - **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5**

  - [x] 2.4 Write property test for priority ordering (Property 8)
    - **Property 8: Priority ordering selects by Severity descending then CreatedAtUtc descending**
    - **Validates: Requirements 7.1, 7.2, 9.5**

  - [x] 2.5 Implement CreateAsync with HTML sanitization and validation
    - Validate Title ≤ 200 chars, Content ≤ 10000 chars, StartsAtUtc < ExpiresAtUtc (when both provided)
    - Sanitize Content using configured HtmlSanitizer before persistence
    - Set CreatedByUserId from ICurrentUserAccessor, set CreatedAtUtc and UpdatedAtUtc to UtcNow
    - Create notifications for active users when NotifyUsers=true and announcement is immediately active
    - Audit log the creation
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 14.2, 14.5, 14.6, 16.1, 16.2_

  - [x] 2.6 Write property test for creation preserves fields (Property 2)
    - **Property 2: Creation preserves all input fields and sets audit metadata**
    - **Validates: Requirements 3.1, 3.2**

  - [x] 2.7 Write property test for creation rejects invalid input (Property 3)
    - **Property 3: Creation rejects invalid input**
    - **Validates: Requirements 3.3, 3.4**

  - [x] 2.8 Implement UpdateAsync with ClearDismissals and notification support
    - Validate same constraints as create, throw KeyNotFoundException if not found
    - Sanitize Content using HtmlSanitizer
    - Update specified fields, set UpdatedAtUtc
    - Delete all dismissals when ClearDismissals=true
    - Create notifications when NotifyUsers=true and announcement is currently active
    - Audit log the update with old/new value tracking
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 14.5, 14.6, 16.3_

  - [x] 2.9 Write property test for update preserves fields (Property 4)
    - **Property 4: Update preserves fields and refreshes UpdatedAtUtc**
    - **Validates: Requirements 4.1**

  - [x] 2.10 Write property test for ClearDismissals (Property 5)
    - **Property 5: ClearDismissals removes all dismissal records for the announcement**
    - **Validates: Requirements 4.3**

  - [x] 2.11 Implement DeleteAsync
    - Throw KeyNotFoundException if not found
    - Delete announcement and all associated dismissals (cascade via EF)
    - Audit log the deletion
    - _Requirements: 5.1, 5.2_

  - [x] 2.12 Write property test for delete cascade (Property 6)
    - **Property 6: Delete removes announcement and all associated dismissals**
    - **Validates: Requirements 5.1**

  - [x] 2.13 Implement GetAllAsync with status filtering
    - Return all announcements ordered by CreatedAtUtc descending
    - Apply status filter (Active, Scheduled, Expired, Draft) using ComputeStatus logic
    - Map entities to AnnouncementDto with computed Status field
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

  - [x] 2.14 Write property test for status filter (Property 7)
    - **Property 7: Status filter returns exactly the matching subset**
    - **Validates: Requirements 6.1, 6.2, 6.3, 6.4, 6.5**

  - [x] 2.15 Implement GetActiveForUserAsync (banner + dashboard queries)
    - Return active announcements for the user, excluding dismissed Banner-type announcements from the banner subset
    - Include all active announcements (regardless of dismissal) for dashboard/list
    - Order by priority (Severity desc, CreatedAtUtc desc)
    - _Requirements: 7.1, 7.2, 9.1, 12.2, 12.5_

  - [x] 2.16 Write property test for dismissal excludes from banner (Property 9)
    - **Property 9: Dismissal excludes announcement from user's banner query**
    - **Validates: Requirements 8.1, 8.3**

  - [x] 2.17 Write property test for dashboard ignores dismissals (Property 11)
    - **Property 11: Dashboard query returns all active announcements regardless of dismissal status**
    - **Validates: Requirements 9.1, 12.5**

  - [x] 2.18 Implement GetForListPageAsync
    - Return all currently active announcements plus announcements expired within the last 30 days
    - Order by CreatedAtUtc descending
    - _Requirements: 10.3, 10.4_

  - [x] 2.19 Write property test for list page 30-day window (Property 12)
    - **Property 12: List page query includes active plus expired within 30 days**
    - **Validates: Requirements 10.2**

  - [x] 2.20 Implement DismissAsync
    - Create AnnouncementDismissal record (idempotent — do nothing if already dismissed)
    - _Requirements: 8.1, 8.3, 8.4_

  - [x] 2.21 Write property test for idempotent dismissal (Property 10)
    - **Property 10: Dismissal is idempotent**
    - **Validates: Requirements 8.4**

  - [x] 2.22 Write property test for notification delivery (Property 14)
    - **Property 14: Notification delivery respects NotifyUsers flag**
    - **Validates: Requirements 16.1, 16.4, 16.5**

- [x] 3. Implement AnnouncementController (API layer)
  - [x] 3.1 Create AnnouncementController with all endpoints
    - Create `ApiService/Controllers/AnnouncementController.cs` extending BaseController
    - GET `/api/announcements/active` — returns active announcements for current user (authenticated)
    - GET `/api/announcements/list` — returns active + recently expired for list page (authenticated)
    - GET `/api/announcements` — returns all with optional status filter (Admin only)
    - POST `/api/announcements` — create announcement (Admin only)
    - PUT `/api/announcements/{id}` — update announcement with ClearDismissals/NotifyUsers flags (Admin only)
    - DELETE `/api/announcements/{id}` — delete announcement (Admin only)
    - POST `/api/announcements/{id}/dismiss` — dismiss for current user (authenticated)
    - Map exceptions to HTTP status codes (KeyNotFoundException→404, ArgumentException→400)
    - _Requirements: 13.1, 13.2, 13.3, 13.4, 13.5, 13.6, 13.7_

- [x] 4. Checkpoint - Ensure backend compiles and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement Web project API client and context
  - [x] 5.1 Create ApiAnnouncementService (typed HttpClient)
    - Create `Web/Services/ApiClients/ApiAnnouncementService.cs`
    - Implement methods: GetActiveForUserAsync, GetForListPageAsync, GetAllAsync(filter), CreateAsync, UpdateAsync, DeleteAsync, DismissAsync
    - Register in `Web/Extensions/ApiClientServiceExtensions.cs` with Aspire service discovery base address
    - Return `ApiResult<T>` pattern — never throw on HTTP errors
    - _Requirements: 13.1, 13.2, 13.3, 13.4, 13.5, 13.6, 13.7_

  - [x] 5.2 Create IAnnouncementContext interface and AnnouncementContext implementation
    - Create `Web/Abstractions/IAnnouncementContext.cs` with BannerAnnouncements, AllActiveAnnouncements, IsLoaded, OnChange event, InitializeAsync, DismissAsync
    - Create `Web/Services/Contexts/AnnouncementContext.cs` implementing IAnnouncementContext
    - Load active announcements on InitializeAsync, separate into BannerAnnouncements (non-dismissed, Banner type, priority ordered) and AllActiveAnnouncements (all active regardless of dismissal)
    - On DismissAsync: call API, on success remove from BannerAnnouncements cache, fire OnChange
    - Register as scoped in `Web/Extensions/ApplicationServiceExtensions.cs`
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5_

  - [x] 5.3 Write property test for context dismissal (Property 13)
    - **Property 13: Context dismissal removes from cached banner list**
    - **Validates: Requirements 12.3**

- [x] 6. Implement Topbar Announcement Icon
  - [x] 6.1 Create AnnouncementIcon component and integrate in topbar
    - Create `Web/Components/Layout/Topbar/AnnouncementIcon.razor`
    - Render MudIconButton with Icons.Material.Filled.Campaign, Color.Inherit, aria-label="Announcements"
    - OnClick navigates to `/announcements` via NavigationManager
    - Add to topbar layout between NotificationBell and DropdownProfile
    - _Requirements: 15.1, 15.2, 15.3, 15.4, 15.5_

- [x] 7. Implement Top Banner component
  - [x] 7.1 Create TopBanner component with severity styling and dismissal
    - Create `Web/Components/Layout/Topbar/TopBanner.razor` + `.razor.cs`
    - Inject IAnnouncementContext, subscribe to OnChange event
    - Display highest-priority active, non-dismissed, Banner-type announcement
    - Show Title (plain text) and Content excerpt (strip HTML, truncate to 150 chars)
    - Apply severity-based styling: Info (blue), Warning (amber), Critical (red)
    - Show "N more" link navigating to `/announcements` when multiple banner announcements exist
    - Render nothing when no announcements available
    - Dismiss button calls AnnouncementContext.DismissAsync, which updates cache and re-renders
    - Integrate in MainLayout above MudMainContent
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 8.1, 8.2_

- [x] 8. Implement Dashboard Card component
  - [x] 8.1 Create AnnouncementDashboardCard component
    - Create `Web/Components/Shared/AnnouncementDashboardCard.razor` + `.razor.cs`
    - Inject IAnnouncementContext, subscribe to OnChange event
    - Display compact list of ALL active announcements (both types, all severities, regardless of dismissal)
    - Show Title, plain-text excerpt (strip HTML), severity indicator (icon/color), relative timestamp
    - Order by Severity descending then CreatedAtUtc descending
    - Show "View all" link navigating to `/announcements`
    - Show "No current announcements" message when empty
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_

- [x] 9. Checkpoint - Ensure all components compile and render correctly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Implement Announcement List Page
  - [x] 10.1 Create Announcements/Index page with master-detail layout
    - Create `Web/Components/Pages/Announcements/Index.razor` + `.razor.cs`
    - Route: `/announcements`, require authentication
    - Master-detail layout: scrollable left pane (list) + right pane (detail)
    - Left pane: active + expired within 30 days, sorted by CreatedAtUtc desc
    - List items show: severity chip, title (one-line truncated), content snippet (HTML stripped, 2-line truncated), relative timestamp, "Expired" label for expired items
    - Dimmed styling for expired items
    - Selected state: background highlight with left bar indicator (notification-selected pattern)
    - Detail pane: full title, severity chip, published date (relative), "Expired" chip with date if applicable, full HTML content via MarkupString. Display_Type NOT shown.
    - Empty state: "Select an announcement to view details" placeholder
    - Support `?id={announcementId}` query parameter for deep-link auto-selection
    - Use PageContent loading wrapper during initial data fetch
    - Auto-select first item on load if no query param specified
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7, 10.8, 10.9_

- [x] 11. Implement Admin Management Page
  - [x] 11.1 Create AdminAnnouncements page with data grid and CRUD dialogs
    - Create `Web/Components/Pages/Admin/AdminAnnouncements.razor` + `.razor.cs`
    - Route: `/admin/announcements`, require Admin role
    - MudDataGrid with columns: Title, DisplayType, Severity, Status (computed), StartsAtUtc, ExpiresAtUtc, CreatedAtUtc
    - Filter chips for status filtering (All, Active, Scheduled, Expired, Draft)
    - "Create Announcement" button opens MudDialog with: Title input, TinyMCE editor (Content), DisplayType select, Severity select, StartsAtUtc/ExpiresAtUtc datetime pickers, IsActive toggle, NotifyUsers toggle
    - TinyMCE toolbar limited to: bold, italic, underline, h3/h4, bullet list, numbered list, blockquote, hyperlink
    - NotifyUsers default: true for Standard, false for Banner display type
    - Edit action opens same dialog pre-populated with current values + ClearDismissals checkbox
    - Delete action shows ConfirmationDialog before deletion
    - Success: Snackbar + refresh grid. Validation error: show in dialog without closing
    - Install TinyMCE.Blazor NuGet package
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6, 11.7, 11.8, 11.9_

- [x] 12. Wire up navigation and page permissions
  - [x] 12.1 Register announcement pages in navigation and seed page permissions
    - Add announcement list page link to DefaultNavigationProvider
    - Add admin announcements page link to admin navigation group
    - Add page permission seed data for `/announcements` and `/admin/announcements`
    - Initialize AnnouncementContext in MainLayout circuit initialization (alongside existing contexts)
    - _Requirements: 10.1, 11.1, 15.1_

- [x] 13. Final checkpoint - Ensure full solution compiles and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The TinyMCE.Blazor NuGet package and Ganss.Xss.HtmlSanitizer NuGet package must be installed before their respective tasks
- All service registrations follow the existing extension method pattern (no inline registrations in Program.cs)
- The AnnouncementContext follows the same scoped-per-circuit pattern as NotificationContext

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["1.3", "2.1"] },
    { "id": 3, "tasks": ["2.2", "2.5", "2.8", "2.11", "2.13", "2.15", "2.18", "2.20"] },
    { "id": 4, "tasks": ["2.3", "2.4", "2.6", "2.7", "2.9", "2.10", "2.12", "2.14", "2.16", "2.17", "2.19", "2.21", "2.22", "3.1"] },
    { "id": 5, "tasks": ["5.1"] },
    { "id": 6, "tasks": ["5.2"] },
    { "id": 7, "tasks": ["5.3", "6.1", "7.1", "8.1"] },
    { "id": 8, "tasks": ["10.1"] },
    { "id": 9, "tasks": ["11.1"] },
    { "id": 10, "tasks": ["12.1"] }
  ]
}
```
