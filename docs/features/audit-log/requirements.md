# Requirements Document

## Introduction

The Audit Log feature provides administrators with a centralized, searchable, and exportable record of significant actions performed within the application. It captures who performed an action, what action was taken, which entity was affected, and when it occurred. The feature includes a dedicated UI page following existing MudDataGrid patterns, a background logging service, and the necessary database entities/tables to persist audit trail data.

## Glossary

- **Audit_Log_Service**: The backend service responsible for recording audit entries into the database when significant actions occur in the application.
- **Audit_Log_Page**: The Blazor Server UI page at `/audit-log` that displays audit entries in a filterable, searchable, paginated data grid. Located at `AspireWebAppTemplate.Web/Components/Pages/AuditLog/`.
- **AuditLogEntry**: The EF Core entity representing a single audit record, stored in the `AuditLogEntries` table. Located at `AspireWebAppTemplate.ApiService/Data/Entities/AuditLogEntry.cs`.
- **AuditActionType**: An enum defining the categories of auditable actions (e.g., UserCreated, UserUpdated, RoleAssigned, LoginSuccess, SettingsChanged). Located at `AspireWebAppTemplate.Core/Domain/Enums/AuditActionType.cs`.
- **AuditEntityType**: An enum defining the types of entities that can be audited (e.g., User, Role, Settings, System). Located at `AspireWebAppTemplate.Core/Domain/Enums/AuditEntityType.cs`.
- **IAuditLogService**: The service interface abstraction for the Audit_Log_Service. Located at `AspireWebAppTemplate.ApiService/Abstractions/IAuditLogService.cs`.
- **Administrator**: A user assigned the "Admin" role who has access to the Audit_Log_Page.
- **CSV_Export**: A comma-separated values file generated from filtered audit log data for offline analysis.

## Requirements

### Requirement 1: Audit Log Entity and Database Schema

**User Story:** As a developer, I want a well-structured audit log database table with proper indexing, so that audit entries can be efficiently stored and queried.

#### Acceptance Criteria

1. THE AuditLogEntry entity SHALL contain the following properties: Id (Guid, primary key), UserId (string, max 450 characters, foreign key to ApplicationUser), UserDisplayName (string, max 256 characters, denormalized for display), ActionType (AuditActionType enum), EntityType (AuditEntityType enum), EntityId (string, max 450 characters, the affected entity identifier), EntityName (string, max 256 characters, human-readable name of the affected entity), Description (string, max 1024 characters, human-readable summary of the action), OldValues (string, nullable, JSON-serialized previous state), NewValues (string, nullable, JSON-serialized new state), IpAddress (string, nullable, max 45 characters), Timestamp (DateTime, UTC)
2. THE ApplicationDbContext SHALL register the AuditLogEntry entity with the table name "AuditLogEntries"
3. THE ApplicationDbContext SHALL configure an index on the Timestamp column
4. THE ApplicationDbContext SHALL configure an index on the UserId column
5. THE ApplicationDbContext SHALL configure an index on the ActionType column
6. THE ApplicationDbContext SHALL store the ActionType and EntityType properties as string conversions
7. THE ApplicationDbContext SHALL configure the UserId foreign key with restrict delete behavior so that audit log entries are preserved when a user is deleted
8. THE ApplicationDbContext SHALL configure the Timestamp property with a default value of the current UTC time

### Requirement 2: Audit Action and Entity Type Enums

**User Story:** As a developer, I want clearly defined enums for audit action types and entity types, so that audit entries are categorized consistently across the application.

#### Acceptance Criteria

1. THE AuditActionType enum SHALL define the following values in this order: UserCreated, UserUpdated, UserDeleted, UserActivated, UserDeactivated, RoleCreated, RoleUpdated, RoleDeleted, RoleAssigned, RoleUnassigned, LoginSuccess, LoginFailed, LogoutSuccess, SettingsChanged, PasswordChanged, ProfileUpdated
2. THE AuditEntityType enum SHALL define the following values in this order: User, Role, Settings, System
3. THE enum values SHALL be stored as PascalCase strings in the database via EF Core HasConversion, consistent with the string conversion configuration in Requirement 1

### Requirement 3: Audit Log Recording Service

**User Story:** As a developer, I want a service that records audit entries automatically when significant actions occur, so that the audit trail is populated without manual intervention in each feature.

#### Acceptance Criteria

1. THE Audit_Log_Service SHALL implement the IAuditLogService interface
2. THE IAuditLogService SHALL expose a method to log an audit entry accepting parameters: userId (nullable), actionType, entityType, entityId, entityName, description, oldValues (optional), newValues (optional), ipAddress (optional)
3. WHEN the Audit_Log_Service receives a log request, THE Audit_Log_Service SHALL persist the AuditLogEntry to the database with the Timestamp set to the current UTC time at the moment the request is received
4. WHEN the Audit_Log_Service receives a log request with a non-null userId that matches an existing ApplicationUser, THE Audit_Log_Service SHALL resolve and store the user display name from the ApplicationUser entity
5. IF the Audit_Log_Service receives a log request with a null userId, THEN THE Audit_Log_Service SHALL store an empty string as the UserDisplayName and persist the entry without attempting user resolution
6. IF the Audit_Log_Service receives a log request with a non-null userId that does not match any ApplicationUser, THEN THE Audit_Log_Service SHALL store the userId value as the UserDisplayName and persist the entry
7. IF the Audit_Log_Service encounters a database error while persisting an entry, THEN THE Audit_Log_Service SHALL log the failure at Error level using ILogger including the actionType, entityType, entityId, and the exception details, and continue without throwing an exception to the caller
8. THE Audit_Log_Service SHALL be registered as a scoped service in the dependency injection container

### Requirement 4: Audit Log Data Retrieval via QueryableDataGridUtils

**User Story:** As a developer, I want a reusable database-level grid utility that translates MudDataGrid state into EF Core queries, so that the audit log page (and future large-dataset pages) can efficiently display data without loading all records into memory.

#### Acceptance Criteria

1. THE QueryableDataGridUtils\<T\> utility SHALL be located at `AspireWebAppTemplate.UI/Utilities/QueryableDataGridUtils.cs` alongside the existing `DataGridUtils<T>`
2. THE QueryableDataGridUtils\<T\> SHALL accept an `IQueryable<T>` source and a `GridState<T>` and translate column filters, global search, sorting, and pagination into EF Core expressions that execute at the database level
3. THE QueryableDataGridUtils\<T\> SHALL use `Expression<Func<T, ...>>` (not `Func<T, ...>`) for property mappings so that EF Core can translate them to SQL
4. THE QueryableDataGridUtils\<T\> SHALL support the same fluent MapString, MapInt, MapDateTime, MapBool pattern as the existing DataGridUtils\<T\>
5. THE QueryableDataGridUtils\<T\> SHALL provide a `ServerReloadAsync` method returning `GridData<T>` with paged items and total count, supporting an optional global search term across specified string fields, and optional line numbering (1-based, page-aware)
6. THE QueryableDataGridUtils\<T\> SHALL provide a `GetAllMatchingAsync` method that applies filters and global search but returns all matching rows (up to a configurable maxRows cap) without pagination, for export scenarios
7. THE QueryableDataGridUtils\<T\> SHALL support `CancellationToken` on all async methods
8. THE Audit_Log_Page SHALL use the QueryableDataGridUtils\<T\> with the base query `dbContext.AuditLogEntries.AsQueryable()` and apply additional toolbar filters (action type, entity type, date range) before passing to the utility
9. WHEN a search text is provided in the toolbar, THE Audit_Log_Page SHALL pass it as the globalSearchTerm filtering across UserDisplayName, EntityName, and Description (case-insensitive)
10. THE QueryableDataGridUtils\<T\> SHALL apply a default sort of Timestamp descending when no sort definition is present in the GridState

### Requirement 5: Audit Log Page with Server-Side Data Grid

**User Story:** As an administrator, I want a dedicated Audit Log page with a searchable, filterable data grid, so that I can review application activity efficiently.

#### Acceptance Criteria

1. THE Audit_Log_Page SHALL be accessible at the route "/audit-log"
2. THE Audit_Log_Page SHALL require the "Admin" role for access using the Authorize attribute
3. THE Audit_Log_Page SHALL display a MudDataGrid with server-side pagination, sorting, and filtering using the DataGridUtils pattern, with a default sort order of Timestamp descending (newest first) and a default page size of 10 rows
4. THE Audit_Log_Page SHALL display the following columns: Line number (page-aware, starting at 1), Timestamp (formatted in the user's timezone using the IUserTimeZoneContext), User (display name), Action Type, Entity Type, Entity Name, Description
5. THE Audit_Log_Page SHALL include a search text field in the toolbar that filters across UserDisplayName, EntityName, and Description with a 500ms debounce delay
6. THE Audit_Log_Page SHALL include an Action Type dropdown filter that allows selecting a specific AuditActionType value or clearing the selection to show all action types
7. THE Audit_Log_Page SHALL include an Entity Type dropdown filter that allows selecting a specific AuditEntityType value or clearing the selection to show all entity types
8. WHEN a date range is specified, THE Audit_Log_Page SHALL filter entries with a Timestamp on or after the start date and on or before the end date; IF only a start date is provided, THEN THE Audit_Log_Page SHALL filter entries on or after that date; IF only an end date is provided, THEN THE Audit_Log_Page SHALL filter entries on or before that date
9. WHILE data is being retrieved from the server, THE Audit_Log_Page SHALL display the MudDataGrid in its Loading state with a "Loading audit entries..." message
10. WHEN no records match the current filters, THE Audit_Log_Page SHALL display a "No audit entries found." message in the grid's NoRecordsContent area
11. THE Audit_Log_Page SHALL combine all active toolbar filters (search text, Action Type dropdown, Entity Type dropdown, and date range) using AND logic so that only entries matching all active filters are displayed

### Requirement 6: Audit Log Detail View

**User Story:** As an administrator, I want to view the full details of an audit entry including old and new values, so that I can understand exactly what changed.

#### Acceptance Criteria

1. WHEN an administrator clicks a row in the audit data grid, THE Audit_Log_Page SHALL display a detail dialog showing the complete entry information, with a close button that dismisses the dialog and returns focus to the data grid
2. THE detail view SHALL display the following fields: Timestamp (formatted in the user's timezone), User display name, Action Type, Entity Type, Entity ID, Entity Name, Description, IP Address, Old Values (pretty-printed JSON with indentation), New Values (pretty-printed JSON with indentation)
3. WHEN OldValues or NewValues are null, THE detail view SHALL display "N/A" in place of the JSON content for those fields
4. WHEN IpAddress is null, THE detail view SHALL display "N/A" for the IP Address field

### Requirement 7: CSV Export

**User Story:** As an administrator, I want to export the current filtered audit log results to a CSV file, so that I can perform offline analysis or share audit data with stakeholders.

#### Acceptance Criteria

1. THE Audit_Log_Page SHALL display an "Export CSV" button in the toolbar
2. WHEN the administrator clicks the Export CSV button, THE Audit_Log_Page SHALL generate a CSV file containing all entries matching the current filter criteria (not limited to the current page), up to a maximum of 50,000 rows
3. THE CSV export SHALL include columns: Timestamp, User, Action Type, Entity Type, Entity Name, Description, IP Address
4. THE CSV export SHALL trigger a browser file download with the filename format "audit-log-{yyyy-MM-dd}.csv"
5. WHILE the CSV export is being generated, THE Audit_Log_Page SHALL display a loading indicator on the Export CSV button and disable the button
6. WHEN the current filters return zero results, THE Export CSV button SHALL be disabled
7. IF the CSV export operation fails, THEN THE Audit_Log_Page SHALL display an error alert and re-enable the Export CSV button
8. THE CSV export SHALL format the Timestamp column in ISO 8601 format (yyyy-MM-ddTHH:mm:ssZ) in UTC

### Requirement 8: Navigation Menu Integration

**User Story:** As an administrator, I want the Audit Log page to appear in the Administration navigation group, so that I can access it from the main menu alongside other admin pages.

#### Acceptance Criteria

1. THE DefaultNavigationProvider SHALL include a NavItem of type Link with Text "Audit Log", Href "audit-log", and Icon "material-symbols-rounded/history" as the last entry in the Administration group's Children collection
2. WHEN a user without the "Admin" role views the main navigation menu, THE System SHALL NOT display the "Audit Log" navigation item
3. WHEN a user with the "Admin" role views the main navigation menu, THE System SHALL display the "Audit Log" navigation item within the Administration group

### Requirement 9: Audit Event Integration Points

**User Story:** As a developer, I want clear integration points where audit logging is triggered, so that all significant user and system actions are captured consistently.

#### Acceptance Criteria

1. WHEN a user is created, THE Audit_Log_Service SHALL record an entry with ActionType UserCreated, EntityType User, EntityId set to the created user's identifier, and EntityName set to the created user's display name
2. WHEN a user profile is updated, THE Audit_Log_Service SHALL record an entry with ActionType UserUpdated, EntityType User, EntityId set to the updated user's identifier, and OldValues/NewValues containing the changed profile fields serialized as JSON
3. WHEN a user is deleted, THE Audit_Log_Service SHALL record an entry with ActionType UserDeleted, EntityType User, EntityId set to the deleted user's identifier, and EntityName set to the deleted user's display name
4. WHEN a user is activated, THE Audit_Log_Service SHALL record an entry with ActionType UserActivated, EntityType User, and EntityId set to the affected user's identifier
5. WHEN a user is deactivated, THE Audit_Log_Service SHALL record an entry with ActionType UserDeactivated, EntityType User, and EntityId set to the affected user's identifier
6. WHEN a role is assigned to a user, THE Audit_Log_Service SHALL record an entry with ActionType RoleAssigned, EntityType Role, EntityId set to the user's identifier, and EntityName set to the assigned role name
7. WHEN a role is removed from a user, THE Audit_Log_Service SHALL record an entry with ActionType RoleUnassigned, EntityType Role, EntityId set to the user's identifier, and EntityName set to the removed role name
8. WHEN a user logs in successfully, THE Audit_Log_Service SHALL record an entry with ActionType LoginSuccess, EntityType User, EntityId set to the authenticated user's identifier, and IpAddress set to the request source IP
9. WHEN a login attempt fails, THE Audit_Log_Service SHALL record an entry with ActionType LoginFailed, EntityType System, EntityId set to the attempted username or email, and IpAddress set to the request source IP
10. WHEN a user changes their password, THE Audit_Log_Service SHALL record an entry with ActionType PasswordChanged, EntityType User, and EntityId set to the user's identifier
11. WHEN a user updates their settings, THE Audit_Log_Service SHALL record an entry with ActionType SettingsChanged, EntityType Settings, EntityId set to the user's identifier, and OldValues/NewValues containing the changed settings fields serialized as JSON
12. IF the Audit_Log_Service fails to record an audit entry for any integration point, THEN THE Audit_Log_Service SHALL log the failure via ILogger without interrupting the triggering operation

### Requirement 10: Audit Log Data Retention

**User Story:** As an administrator, I want audit log entries to be retained for a configurable period, so that storage usage is managed while maintaining compliance with data retention policies.

#### Acceptance Criteria

1. THE application configuration (appsettings.json) SHALL include an "AuditLog:RetentionDays" setting with a default value of 365 and an accepted range of 1 to 3650
2. IF the "AuditLog:RetentionDays" setting is missing, non-numeric, or outside the range of 1 to 3650, THEN THE Audit_Log_Service SHALL fall back to the default value of 365 and log a warning indicating the invalid configuration value
3. THE Audit_Log_Service SHALL expose a method to purge entries older than the configured retention period
4. WHEN the purge method is invoked, THE Audit_Log_Service SHALL delete all AuditLogEntry records with a Timestamp older than the current UTC time minus the configured retention days
5. IF the purge operation fails due to a database error, THEN THE Audit_Log_Service SHALL log an error indicating the failure reason and propagate the exception to the caller
6. WHEN the purge method completes successfully, THE Audit_Log_Service SHALL log the count of purged entries using ILogger at Information level

### Requirement 11: Code Documentation and Maintainability

**User Story:** As a developer, I want all audit log code to include complete XML documentation comments and inline comments, so that the codebase remains maintainable and easy to understand for current and future team members.

#### Acceptance Criteria

1. ALL public classes, interfaces, methods, properties, and enum values SHALL include complete XML documentation comments using the `<summary>`, `<param>`, `<returns>`, `<remarks>`, and `<exception>` tags as appropriate
2. ALL non-trivial private methods SHALL include XML documentation comments with at least a `<summary>` tag explaining their purpose
3. THE IAuditLogService interface SHALL include XML documentation comments on the interface itself and on every method, describing the contract, parameters, return values, and expected exceptions
4. ALL enum values in AuditActionType and AuditEntityType SHALL include `<summary>` comments explaining when each value is used and what it represents
5. ALL EF Core configuration blocks (entity configuration, index definitions, relationship setup) SHALL include inline comments explaining the design rationale (e.g., why restrict delete, why string conversion for enums, why specific indexes)
6. ALL complex logic blocks (filtering, sorting, pagination, retention calculation, CSV generation) SHALL include inline comments explaining the algorithm or business rule being implemented
7. THE AuditLogEntry entity properties SHALL include XML documentation comments describing the purpose, constraints (max length, nullability), and format (e.g., "JSON-serialized previous state") of each property
8. ALL Razor component code-behind files SHALL include XML documentation comments on the class and on all methods explaining their role in the page lifecycle and user interaction flow

## Suggested Project Structure

```
AspireWebAppTemplate.ApiService/
├── Abstractions/
│   └── IAuditLogService.cs                    (service interface - LogAsync + PurgeOldEntriesAsync)
├── Data/
│   └── Entities/
│       └── AuditLogEntry.cs                   (EF Core entity)
├── Services/
│   └── AuditLogService.cs                     (service implementation)

AspireWebAppTemplate.Web/
├── Components/
│   └── Pages/
│       └── AuditLog/
│           ├── Index.razor                    (main page with MudDataGrid)
│           ├── Index.razor.cs                 (code-behind, calls API via HTTP client service)
│           └── AuditLogDetailDialog.razor     (detail view dialog)

AspireWebAppTemplate.UI/
├── Utilities/
│   ├── DataGridUtils.cs                       (existing in-memory utility)
│   └── QueryableDataGridUtils.cs              (database-level utility, used by ApiService)

AspireWebAppTemplate.Core/
├── Domain/
│   └── Enums/
│       ├── AuditActionType.cs                 (action type enum)
│       └── AuditEntityType.cs                 (entity type enum)

AspireWebAppTemplate.Tests/
└── AuditLog/
    └── ...                                    (property-based + unit tests)
```
