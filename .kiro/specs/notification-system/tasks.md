# Implementation Plan: Notification System

## Overview

Implement an in-app notification system with backend service layer, REST API endpoints, typed HttpClient, per-circuit caching, and MudBlazor UI components (bell with badge, notification page, settings integration). Follows the established thin-controller / full-service-layer pattern.

## Tasks

- [x] 1. Define shared domain types in Core project
  - [x] 1.1 Create NotificationCategory enum and notification DTOs
    - Create `Core/Domain/Enums/NotificationCategory.cs` with Security, UserManagement, System values
    - Create `Core/Contracts/Notifications/` directory with all DTOs: `NotificationDto`, `CreateNotificationRequest`, `NotificationQueryParams`, `BulkDismissRequest`, `NotificationPreferenceDto`, `UpdateNotificationPreferenceRequest`
    - Include full XML documentation on all public members
    - _Requirements: 1.2, 2.4, 5.3, 9.3_

- [x] 2. Implement data layer in ApiService
  - [x] 2.1 Create Notification and NotificationPreference entities with EF Core configuration
    - Create `ApiService/Data/Entities/Notification.cs` with all fields (Id, UserId, Category, Title, Message, IsRead, CreatedAtUtc, ReadAtUtc)
    - Create `ApiService/Data/Entities/NotificationPreference.cs` with all fields (Id, UserId, Category, InAppEnabled, EmailEnabled)
    - Add EF Core entity configurations: table names, max lengths, string enum conversion, composite indexes on (UserId, IsRead) and (UserId, CreatedAtUtc), unique index on (UserId, Category) for preferences
    - Configure cascade delete FK to ApplicationUser
    - Add DbSet properties to ApplicationDbContext
    - _Requirements: 1.2, 9.3_

  - [x] 2.2 Create EF Core migration for notification tables
    - Run `dotnet ef migrations add AddNotificationSystem` in the ApiService project
    - Verify migration creates Notifications and NotificationPreferences tables with correct schema
    - _Requirements: 1.2, 9.3_

- [x] 3. Implement notification service layer in ApiService
  - [x] 3.1 Create INotificationService interface
    - Create `ApiService/Abstractions/INotificationService.cs` with all methods: CreateNotificationAsync, GetNotificationsAsync, GetUnreadCountAsync, GetRecentAsync, MarkAsReadAsync, MarkAllAsReadAsync, BulkDismissAsync, GetPreferencesAsync, UpdatePreferenceAsync
    - Include full XML documentation with remarks on behavior guarantees
    - _Requirements: 1.1, 1.3, 2.1, 2.2, 2.3, 2.4, 3.1, 4.1, 4.3, 5.1, 5.2, 6.1, 6.2, 9.4, 9.5_

  - [x] 3.2 Implement NotificationService
    - Create `ApiService/Services/NotificationService.cs` implementing INotificationService
    - CreateNotificationAsync: validate user exists, check InAppEnabled preference (default true if no record), create entity or discard silently
    - GetNotificationsAsync: query with optional category/IsRead filters, order by CreatedAtUtc desc, apply pagination, return PagedResult
    - GetUnreadCountAsync: count where IsRead=false for user
    - GetRecentAsync: top N notifications ordered by CreatedAtUtc desc
    - MarkAsReadAsync: find by ID + userId, set IsRead=true and ReadAtUtc, idempotent
    - MarkAllAsReadAsync: bulk update all unread for user, return count
    - BulkDismissAsync: delete only notifications belonging to user, ignore invalid IDs
    - GetPreferencesAsync: return all category preferences, fill defaults for missing categories
    - UpdatePreferenceAsync: upsert preference for user-category pair
    - Register as scoped service in Program.cs
    - _Requirements: 1.1, 1.3, 2.1, 2.2, 2.3, 2.4, 3.1, 4.1, 4.3, 5.1, 5.2, 6.1, 6.2, 9.4, 9.5_

  - [x] 3.3 Write property test: Notification creation preserves all input fields
    - **Property 1: Notification creation preserves all input fields**
    - **Validates: Requirements 1.1**
    - Create `Tests/Notifications/NotificationCreationPropertyTests.cs`
    - Use SQLite in-memory database with seeded user
    - Verify UserId, Category, Title, Message, IsRead=false, CreatedAtUtc set

  - [x] 3.4 Write property test: Notification creation respects InAppEnabled preference
    - **Property 11: Notification creation respects InAppEnabled preference**
    - **Validates: Requirements 9.5**
    - Add to `Tests/Notifications/NotificationCreationPropertyTests.cs`
    - Verify no entity created when InAppEnabled=false; entity created when InAppEnabled=true or no preference exists

  - [x] 3.5 Write property test: Retrieval returns notifications ordered by CreatedAtUtc descending
    - **Property 2: Retrieval returns notifications ordered by CreatedAtUtc descending**
    - **Validates: Requirements 2.1**
    - Create `Tests/Notifications/NotificationRetrievalPropertyTests.cs`
    - Seed multiple notifications with varying timestamps, verify descending order

  - [x] 3.6 Write property test: Filtering returns only notifications matching all specified criteria
    - **Property 3: Filtering returns only notifications matching all specified criteria**
    - **Validates: Requirements 2.2, 2.3**
    - Add to `Tests/Notifications/NotificationRetrievalPropertyTests.cs`
    - Apply category and/or IsRead filters, verify all returned items match and none are excluded

  - [x] 3.7 Write property test: Pagination returns at most pageSize items
    - **Property 4: Pagination returns at most pageSize items**
    - **Validates: Requirements 2.4**
    - Add to `Tests/Notifications/NotificationRetrievalPropertyTests.cs`
    - Verify count ≤ pageSize and TotalCount reflects full filtered set

  - [x] 3.8 Write property test: Unread count matches actual count of unread notifications
    - **Property 5: Unread count matches actual count of unread notifications**
    - **Validates: Requirements 3.1**
    - Add to `Tests/Notifications/NotificationRetrievalPropertyTests.cs`
    - Seed mixed IsRead states, verify GetUnreadCountAsync equals count where IsRead=false

  - [x] 3.9 Write property test: Mark-as-read sets IsRead and ReadAtUtc correctly
    - **Property 7: Mark-as-read sets IsRead and ReadAtUtc correctly**
    - **Validates: Requirements 4.1**
    - Create `Tests/Notifications/NotificationMarkAsReadPropertyTests.cs`
    - Verify IsRead=true and ReadAtUtc is non-null after marking

  - [x] 3.10 Write property test: Mark-as-read is idempotent
    - **Property 8: Mark-as-read is idempotent**
    - **Validates: Requirements 4.3**
    - Add to `Tests/Notifications/NotificationMarkAsReadPropertyTests.cs`
    - Mark twice, verify ReadAtUtc unchanged on second call

  - [x] 3.11 Write property test: Bulk dismiss deletes only owned-and-existing notifications
    - **Property 9: Bulk dismiss deletes only owned-and-existing notifications**
    - **Validates: Requirements 5.1, 5.2**
    - Create `Tests/Notifications/BulkDismissPropertyTests.cs`
    - Seed notifications for multiple users, verify only owned IDs deleted

  - [x] 3.12 Write property test: Mark-all-as-read updates all unread and returns correct count
    - **Property 10: Mark-all-as-read updates all unread and returns correct count**
    - **Validates: Requirements 6.1, 6.2**
    - Create `Tests/Notifications/MarkAllAsReadPropertyTests.cs`
    - Seed N unread, verify all become read and returned count equals N

  - [x] 3.13 Write property test: Missing preferences default to both channels enabled
    - **Property 12: Missing preferences default to both channels enabled**
    - **Validates: Requirements 9.4**
    - Create `Tests/Notifications/NotificationPreferenceDefaultsPropertyTests.cs`
    - Query preferences for user with no records, verify InAppEnabled=true and EmailEnabled=true for all categories

- [x] 4. Implement NotificationController in ApiService
  - [x] 4.1 Create NotificationController with all endpoints
    - Create `ApiService/Controllers/NotificationController.cs` extending BaseController
    - Implement GET /api/notifications (paginated list with filters)
    - Implement GET /api/notifications/unread-count
    - Implement GET /api/notifications/recent
    - Implement PUT /api/notifications/{id}/read
    - Implement PUT /api/notifications/read-all
    - Implement POST /api/notifications/dismiss (validate max 100 IDs, return 400 if exceeded)
    - Implement GET /api/notifications/preferences
    - Implement PUT /api/notifications/preferences
    - All methods delegate to INotificationService, thin controller pattern
    - Exception-to-status mapping: KeyNotFoundException → 404, InvalidOperationException/ArgumentException → 400
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.1, 4.1, 4.2, 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 9.1_

  - [x] 4.2 Write unit tests for NotificationController
    - Create `Tests/Notifications/NotificationControllerTests.cs`
    - Mock INotificationService
    - Test status code mapping (200, 404, 400)
    - Test >100 IDs returns 400 without calling service
    - Test CurrentUserId passed correctly to service
    - _Requirements: 4.2, 5.3, 5.4_

- [x] 5. Checkpoint - Ensure all backend tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Implement Web project API client and context
  - [x] 6.1 Create ApiNotificationService typed HttpClient
    - Create `Web/Services/ApiClients/ApiNotificationService.cs`
    - Implement all methods returning ApiResult<T>: GetNotificationsAsync, GetUnreadCountAsync, GetRecentAsync, MarkAsReadAsync, MarkAllAsReadAsync, BulkDismissAsync, GetPreferencesAsync, UpdatePreferenceAsync
    - Register HttpClient with Aspire service discovery in Program.cs
    - _Requirements: 2.1, 3.1, 4.1, 5.1, 6.1, 9.1_

  - [x] 6.2 Create INotificationContext and NotificationContext implementation
    - Create `Web/Abstractions/INotificationContext.cs` interface with UnreadCount, IsLoaded, OnChange event, InitializeAsync, DecrementCount, ClearCount, RefreshAsync
    - Create `Web/Services/NotificationContext.cs` implementing INotificationContext
    - Implement per-circuit caching: load from API on InitializeAsync, synchronous UnreadCount access, decrement/clear/refresh operations
    - Clamp UnreadCount to zero on decrement
    - Register as scoped service in Program.cs
    - _Requirements: 3.2, 3.3, 3.4_

  - [x] 6.3 Write property test: NotificationContext cache correctly reflects mark/dismiss operations
    - **Property 6: NotificationContext cache correctly reflects mark/dismiss operations**
    - **Validates: Requirements 3.4**
    - Create `Tests/Notifications/NotificationContextPropertyTests.cs`
    - Mock ApiNotificationService
    - Test sequences of DecrementCount/ClearCount, verify UnreadCount = max(0, initial - decrements)

- [x] 7. Implement NotificationBell component
  - [x] 7.1 Create NotificationBell component with badge and dropdown
    - Create `Web/Components/Layout/Topbar/NotificationBell.razor` and `NotificationBell.razor.cs`
    - Display bell icon (Icons.Material.Outlined.Notifications) with MudBadge showing unread count
    - Hide badge when count is zero
    - On click: open MudPopover showing 5 most recent notifications (title, category icon, relative timestamp)
    - "View All" link navigates to /notifications
    - Clicking individual notification marks as read and navigates to /notifications
    - Subscribe to INotificationContext.OnChange for reactive updates
    - Initialize INotificationContext on first render
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

  - [x] 7.2 Integrate NotificationBell into Topbar
    - Add NotificationBell component to Topbar RightContent area, positioned before the profile dropdown
    - _Requirements: 7.1_

- [x] 8. Implement Notifications page
  - [x] 8.1 Create Notifications page with list layout and filtering
    - Create `Web/Components/Pages/Account/Notifications/Notifications.razor` and `Notifications.razor.cs`
    - Route: `/notifications`, require authentication
    - Display notifications in list/feed layout with category icon, title (bold if unread), message preview, relative timestamp
    - Visually distinguish unread (bold text, subtle background highlight)
    - Toolbar with filter chips for category and read status (All, Unread, Read)
    - Implement "Load More" pagination button
    - Use PageContent wrapper for initial loading state
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.10_

  - [x] 8.2 Add notification item actions (mark read, dismiss, bulk actions)
    - Add "Mark as Read" action on individual unread items
    - Add "Dismiss" action on individual items via action button
    - Add selection checkboxes with "Dismiss Selected" toolbar button for bulk dismiss
    - Add "Mark All as Read" button in the toolbar
    - Update NotificationContext cache after each action (DecrementCount, ClearCount, RefreshAsync)
    - _Requirements: 8.6, 8.7, 8.8, 8.9, 3.4_

- [x] 9. Implement notification preferences in Settings page
  - [x] 9.1 Add Notifications section to Settings page
    - Add a "Notifications" section below the Appearance section in the existing Settings page
    - Display all NotificationCategory values with MudSwitch toggles for In-App and Email channels
    - Load preferences on initialization via ApiNotificationService.GetPreferencesAsync
    - Implement instant-save pattern: on toggle change, call UpdatePreferenceAsync immediately
    - On save failure: revert toggle to previous state, show Snackbar error
    - _Requirements: 9.1, 9.2, 9.4, 9.6_

- [x] 10. Add navigation entry for Notifications page
  - [x] 10.1 Register Notifications page in navigation provider
    - Add navigation entry for `/notifications` in DefaultNavigationProvider under the Account section
    - Use appropriate notification icon
    - _Requirements: 8.1_

- [x] 11. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The thin controller pattern means controller tests focus solely on HTTP concerns (status codes, input validation)
- All services registered as scoped to align with per-request DbContext lifetime

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["2.2", "3.1"] },
    { "id": 3, "tasks": ["3.2"] },
    { "id": 4, "tasks": ["3.3", "3.4", "3.5", "3.6", "3.7", "3.8", "3.9", "3.10", "3.11", "3.12", "3.13", "4.1"] },
    { "id": 5, "tasks": ["4.2", "6.1"] },
    { "id": 6, "tasks": ["6.2"] },
    { "id": 7, "tasks": ["6.3", "7.1"] },
    { "id": 8, "tasks": ["7.2", "8.1"] },
    { "id": 9, "tasks": ["8.2", "9.1", "10.1"] }
  ]
}
```
