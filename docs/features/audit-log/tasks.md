# Implementation Plan: Audit Log

## Overview

This plan implements a comprehensive audit trail system for the AspireWebAppTemplate application. The implementation follows a bottom-up approach: enums → entity → database configuration → service → reusable grid utility → UI page → integration hooks → data retention → navigation. Each task builds on the previous ones, ensuring no orphaned code.

## Tasks

- [x] 1. Define enums and entity model
  - [x] 1.1 Create AuditActionType and AuditEntityType enums
    - Create `AspireWebAppTemplate.Core/Domain/Enums/AuditActionType.cs` with values: UserCreated, UserUpdated, UserDeleted, UserActivated, UserDeactivated, RoleCreated, RoleUpdated, RoleDeleted, RoleAssigned, RoleUnassigned, LoginSuccess, LoginFailed, LogoutSuccess, SettingsChanged, PasswordChanged, ProfileUpdated
    - Create `AspireWebAppTemplate.Core/Domain/Enums/AuditEntityType.cs` with values: User, Role, Settings, System
    - Include full XML `<summary>` documentation on each enum value explaining when it is used
    - _Requirements: 2.1, 2.2, 2.3, 11.4_

  - [x] 1.2 Create AuditLogEntry entity
    - Create `AspireWebAppTemplate.ApiService/Data/Entities/AuditLogEntry.cs`
    - Define properties: Id (Guid), UserId (string, max 450), UserDisplayName (string, max 256), ActionType (AuditActionType), EntityType (AuditEntityType), EntityId (string, max 450), EntityName (string, max 256), Description (string, max 1024), OldValues (string?, nullable JSON), NewValues (string?, nullable JSON), IpAddress (string?, max 45), Timestamp (DateTime, UTC)
    - Add navigation property `ApplicationUser? User`
    - Include full XML documentation on every property describing purpose, constraints, and format
    - _Requirements: 1.1, 11.7_

  - [x] 1.3 Configure AuditLogEntry in ApplicationDbContext
    - Add `DbSet<AuditLogEntry> AuditLogEntries` to ApplicationDbContext
    - In `OnModelCreating`, configure: table name "AuditLogEntries", primary key, max lengths, string conversions for ActionType and EntityType, indexes on Timestamp/UserId/ActionType, FK to ApplicationUser with restrict delete, default value `GETUTCDATE()` on Timestamp
    - Include inline comments explaining rationale for restrict delete, string conversion, and index choices
    - _Requirements: 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 11.5_

  - [x] 1.4 Add AuditLog configuration to appsettings.json
    - Add `"AuditLog": { "RetentionDays": 365 }` section to `appsettings.json`
    - _Requirements: 10.1_

- [x] 2. Implement audit log service
  - [x] 2.1 Create IAuditLogService interface
    - Create `AspireWebAppTemplate.ApiService/Abstractions/IAuditLogService.cs`
    - Define `LogAsync` method accepting: userId (nullable), actionType, entityType, entityId, entityName, description, oldValues (optional), newValues (optional), ipAddress (optional)
    - Define `PurgeOldEntriesAsync` method returning `Task<int>`
    - Include full XML documentation on interface and every method with `<summary>`, `<param>`, `<returns>`, `<exception>` tags
    - _Requirements: 3.2, 10.3, 11.3_

  - [x] 2.2 Implement AuditLogService
    - Create `AspireWebAppTemplate.ApiService/Services/AuditLogService.cs` implementing `IAuditLogService`
    - Inject `ApplicationDbContext`, `UserManager<ApplicationUser>`, `ILogger<AuditLogService>`, `IConfiguration`
    - Implement `LogAsync`: resolve UserDisplayName (existing user → DisplayName, unknown userId → userId string, null → empty string), create and persist AuditLogEntry, swallow DB exceptions at Error level
    - Implement `PurgeOldEntriesAsync`: read `AuditLog:RetentionDays` from config (validate 1–3650, fallback to 365 with warning), delete entries older than retention period, log purged count at Information level, propagate DB exceptions
    - Register as scoped service in DI container (in `Program.cs` or service registration extension)
    - Include XML docs on class and all methods, inline comments on error handling and retention calculation
    - _Requirements: 3.1, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8, 10.1, 10.2, 10.4, 10.5, 10.6, 11.1, 11.2, 11.6_

  - [x] 2.3 Write property test for entity persistence round-trip
    - **Property 1: Entity persistence round-trip**
    - Create `AspireWebAppTemplate.Tests/AuditLog/EntityPersistenceRoundTripPropertyTests.cs`
    - Use FsCheck.Xunit `[Property(MaxTest = 1)]` to verify that persisting and retrieving an AuditLogEntry produces identical property values with enums stored as PascalCase strings
    - Use EF Core InMemory or SQLite in-memory provider
    - **Validates: Requirements 1.1, 1.6, 2.3**

  - [x] 2.4 Write property test for user display name resolution
    - **Property 2: User display name resolution**
    - Create `AspireWebAppTemplate.Tests/AuditLog/UserDisplayNameResolutionPropertyTests.cs`
    - Use FsCheck.Xunit `[Property(MaxTest = 1)]` to verify display name resolution: existing user → DisplayName, unknown userId → userId, null → empty string
    - Mock `UserManager<ApplicationUser>` for user lookup
    - **Validates: Requirements 3.4, 3.5, 3.6**

  - [x] 2.5 Write property test for retention configuration validation
    - **Property 10: Retention configuration validation**
    - Create `AspireWebAppTemplate.Tests/AuditLog/RetentionConfigPropertyTests.cs`
    - Use FsCheck.Xunit `[Property(MaxTest = 1)]` to verify: valid int 1–3650 → used as-is; missing/non-numeric/out-of-range → fallback to 365
    - **Validates: Requirements 10.1, 10.2**

  - [x] 2.6 Write property test for purge correctness
    - **Property 11: Purge correctness**
    - Create `AspireWebAppTemplate.Tests/AuditLog/PurgeCorrectnessPropertyTests.cs`
    - Use FsCheck.Xunit `[Property(MaxTest = 1)]` to verify: after purge, no entries older than retention remain, all entries within retention window are preserved
    - **Validates: Requirements 10.4**

- [x] 3. Checkpoint - Ensure data layer and service compile and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement QueryableDataGridUtils\<T\> reusable utility
  - [x] 4.1 Create QueryableDataGridUtils\<T\> class
    - Create `AspireWebAppTemplate.UI/Utilities/QueryableDataGridUtils.cs`
    - Implement fluent property mapping methods: `MapString`, `MapInt`, `MapDateTime`, `MapBool` using `Expression<Func<T, ...>>` for EF Core translation
    - Implement `ServerReloadAsync`: translate GridState column filters → WHERE, global search → OR across specified string fields (case-insensitive), sort definitions → ORDER BY (default Timestamp DESC when no sort), pagination → SKIP/TAKE, count via `CountAsync`, materialize via `ToListAsync`, line numbering (1-based, page-aware)
    - Implement `GetAllMatchingAsync`: apply filters and global search without pagination, cap at configurable maxRows (default 50,000)
    - Support `CancellationToken` on all async methods
    - Include XML documentation on class and all public methods, inline comments explaining the filter/sort/pagination pipeline
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.10, 11.1, 11.6_

  - [x] 4.2 Write property test for search text filtering correctness
    - **Property 3: Search text filtering correctness**
    - Create `AspireWebAppTemplate.Tests/AuditLog/SearchFilteringPropertyTests.cs`
    - Use FsCheck.Xunit `[Property(MaxTest = 1)]` to verify that global search results contain only entries where at least one of UserDisplayName, EntityName, Description contains the search text (case-insensitive), and no matching entries are excluded
    - **Validates: Requirements 4.9**

  - [x] 4.3 Write property test for single-field filter correctness
    - **Property 4: Single-field filter correctness**
    - Create `AspireWebAppTemplate.Tests/AuditLog/SingleFieldFilterPropertyTests.cs`
    - Use FsCheck.Xunit `[Property(MaxTest = 1)]` to verify that applying ActionType/EntityType filter returns only matching entries with no exclusions
    - **Validates: Requirements 5.6, 5.7, 5.8**

  - [x] 4.4 Write property test for date range filter correctness
    - **Property 5: Date range filter correctness**
    - Create `AspireWebAppTemplate.Tests/AuditLog/DateRangeFilterPropertyTests.cs`
    - Use FsCheck.Xunit `[Property(MaxTest = 1)]` to verify that date range filtering (start only, end only, both) returns only entries within range (inclusive), no entries within range excluded
    - **Validates: Requirements 5.8**

  - [x] 4.5 Write property test for default sort order
    - **Property 6: Default sort order**
    - Create `AspireWebAppTemplate.Tests/AuditLog/DefaultSortOrderPropertyTests.cs`
    - Use FsCheck.Xunit `[Property(MaxTest = 1)]` to verify that with empty SortDefinitions, results are ordered by Timestamp descending
    - **Validates: Requirements 4.10**

  - [x] 4.6 Write property test for page overflow
    - **Property 7: Page overflow returns empty with correct total**
    - Create `AspireWebAppTemplate.Tests/AuditLog/PageOverflowPropertyTests.cs`
    - Use FsCheck.Xunit `[Property(MaxTest = 1)]` to verify that requesting a page beyond available data returns empty items with correct total count
    - **Validates: Requirements 4.5**

- [x] 5. Checkpoint - Ensure QueryableDataGridUtils compiles and all property tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Implement audit log UI page
  - [x] 6.1 Create AuditLog Index page with MudDataGrid
    - Create `AspireWebAppTemplate.Web/Components/Pages/AuditLog/Index.razor` and `Index.razor.cs`
    - Set route to `/audit-log` with `[Authorize(Roles = "Admin")]`
    - Inject `ApplicationDbContext`, `IAuditLogService`, `IUserTimeZoneContext`, `IDialogService`, `IJSRuntime`
    - Configure `MudDataGrid<AuditLogViewModel>` with `ServerData` callback, default page size 10, default sort Timestamp descending
    - Display columns: LineNumber, Timestamp (formatted via IUserTimeZoneContext), UserDisplayName, ActionType, EntityType, EntityName, Description
    - Implement `ServerReload` method: apply toolbar filters (ActionType, EntityType, date range) to base query, delegate to `QueryableDataGridUtils<AuditLogEntry>`
    - Implement toolbar: search field (500ms debounce), ActionType dropdown, EntityType dropdown, date range pickers, Export CSV button
    - Handle Loading state ("Loading audit entries...") and NoRecordsContent ("No audit entries found.")
    - Row click opens detail dialog
    - Include XML documentation on the class and all methods
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9, 5.10, 5.11, 4.8, 4.9, 11.8_

  - [x] 6.2 Create AuditLogViewModel
    - Create a view model class with properties: LineNumber, Id, Timestamp, UserDisplayName, ActionType, EntityType, EntityName, Description
    - Used for binding the MudDataGrid display layer
    - _Requirements: 5.4_

  - [x] 6.3 Create AuditLogDetailDialog
    - Create `AspireWebAppTemplate.Web/Components/Pages/AuditLog/AuditLogDetailDialog.razor`
    - Receive `AuditLogEntry` as dialog parameter
    - Display: Timestamp (user timezone), UserDisplayName, ActionType, EntityType, EntityId, EntityName, Description, IpAddress, OldValues (pretty-printed JSON), NewValues (pretty-printed JSON)
    - Show "N/A" for null OldValues, NewValues, IpAddress
    - Include close button that dismisses dialog and returns focus to grid
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x] 6.4 Implement CSV export functionality
    - Implement Export CSV button click handler in Index.razor.cs
    - Use `QueryableDataGridUtils<T>.GetAllMatchingAsync` with current filters, capped at 50,000 rows
    - Generate CSV with columns: Timestamp (ISO 8601 `yyyy-MM-ddTHH:mm:ssZ` UTC), User, Action Type, Entity Type, Entity Name, Description, IP Address
    - Trigger browser download via JS interop with filename `audit-log-{yyyy-MM-dd}.csv`
    - Manage button state: loading indicator while generating, disabled when zero results
    - Display Snackbar error on failure, re-enable button
    - Include inline comments on column ordering, date formatting, and row cap logic
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7, 7.8, 11.6_

  - [x] 6.5 Write property test for CSV row format correctness
    - **Property 8: CSV row format correctness**
    - Create `AspireWebAppTemplate.Tests/AuditLog/CsvRowFormatPropertyTests.cs`
    - Use FsCheck.Xunit `[Property(MaxTest = 1)]` to verify CSV row contains Timestamp (ISO 8601), UserDisplayName, ActionType, EntityType, EntityName, Description, IpAddress in correct column positions
    - **Validates: Requirements 7.3, 7.8**

  - [x] 6.6 Write property test for CSV export filter and row cap
    - **Property 9: CSV export respects filters and row cap**
    - Create `AspireWebAppTemplate.Tests/AuditLog/CsvExportFilterPropertyTests.cs`
    - Use FsCheck.Xunit `[Property(MaxTest = 1)]` to verify exported entries match all active filters, limited to 50,000 rows, with no non-matching entries
    - **Validates: Requirements 7.2**

- [x] 7. Checkpoint - Ensure UI page compiles and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Integrate audit logging into existing operations
  - [x] 8.1 Add audit logging to user management operations
    - Wire `IAuditLogService.LogAsync` calls into existing user CRUD pages/services
    - Log UserCreated, UserUpdated, UserDeleted, UserActivated, UserDeactivated events
    - Include entityId, entityName, oldValues/newValues (JSON) where applicable
    - Ensure audit failures don't interrupt the primary operation
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.12_

  - [x] 8.2 Add audit logging to role management operations
    - Wire `IAuditLogService.LogAsync` calls into existing role assignment/removal pages/services
    - Log RoleAssigned and RoleUnassigned events with userId as entityId and role name as entityName
    - Ensure audit failures don't interrupt the primary operation
    - _Requirements: 9.6, 9.7, 9.12_

  - [x] 8.3 Add audit logging to authentication events
    - Wire `IAuditLogService.LogAsync` calls into login/logout handlers
    - Log LoginSuccess (EntityType User, include IpAddress), LoginFailed (EntityType System, entityId = attempted username/email, include IpAddress), LogoutSuccess
    - Ensure audit failures don't interrupt the primary operation
    - _Requirements: 9.8, 9.9, 9.12_

  - [x] 8.4 Add audit logging to settings and password changes
    - Wire `IAuditLogService.LogAsync` calls into password change and settings/profile update pages
    - Log PasswordChanged (EntityType User) and SettingsChanged (EntityType Settings, include oldValues/newValues JSON)
    - Ensure audit failures don't interrupt the primary operation
    - _Requirements: 9.10, 9.11, 9.12_

- [x] 9. Add navigation menu integration
  - [x] 9.1 Register Audit Log in DefaultNavigationProvider
    - Add NavItem to Administration group's Children collection as the last entry
    - Set Type = NavItemType.Link, Text = "Audit Log", Href = "audit-log", Icon = "material-symbols-rounded/history"
    - Verify existing `Roles = "Admin"` on Administration group gates visibility
    - _Requirements: 8.1, 8.2, 8.3_

- [x] 10. Final checkpoint - Ensure all code compiles and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation at logical boundaries
- Property tests use `MaxTest = 1` (single iteration) to validate correctness without consuming excessive test runner credits
- Unit tests validate specific examples and edge cases
- The implementation language is C# (.NET / Blazor Server) as specified in the design document
- All code must include comprehensive XML documentation and inline comments per Requirement 11

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.4"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["1.3", "2.1"] },
    { "id": 3, "tasks": ["2.2", "4.1"] },
    { "id": 4, "tasks": ["2.3", "2.4", "2.5", "2.6", "4.2", "4.3", "4.4", "4.5", "4.6"] },
    { "id": 5, "tasks": ["6.2"] },
    { "id": 6, "tasks": ["6.1", "6.3"] },
    { "id": 7, "tasks": ["6.4"] },
    { "id": 8, "tasks": ["6.5", "6.6"] },
    { "id": 9, "tasks": ["8.1", "8.2", "8.3", "8.4", "9.1"] }
  ]
}
```
