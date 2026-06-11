# Requirements Document

## Introduction

This feature improves the Role Management data grid by refining which columns are displayed, enhancing global search coverage, and adding a Role Details page for viewing full role information. The goal is to reduce visual clutter in the grid while ensuring all relevant data remains accessible.

## Glossary

- **Role_Grid**: The MudDataGrid component on the Role Management page that displays role records with server-side filtering, sorting, and pagination.
- **Global_Search**: The toolbar search box that filters role records by matching a search term against multiple text fields via the `GlobalFields` selector in `ServerReload`.
- **Role_Details_Page**: A dedicated page (`/role-management/{RoleId}`) that displays full information about a single role.
- **Description_Column**: The PropertyColumn in the Role_Grid that displays the role's description text.
- **Status_Text**: The human-readable representation of a role's active state, either "Active" or "Inactive".

## Requirements

### Requirement 1: Remove Created Date Column from Grid

**User Story:** As an administrator, I want the Created Date column removed from the grid, so that the grid displays only high-value day-to-day columns and reduces horizontal clutter.

#### Acceptance Criteria

1. THE Role_Grid SHALL NOT display a Created Date column.
2. WHEN the Role_Grid renders its columns, THE Role_Grid SHALL display the following columns in order: Line, Role Name, Display Name, Description, Status, User Count, Actions.
3. THE `DataGridUtils` mapping for `CreatedUtc` SHALL be removed since it is no longer a grid column.

### Requirement 2: Include Status Text in Global Search

**User Story:** As an administrator, I want to search roles by their status text ("Active" or "Inactive"), so that I can quickly filter roles by status using the search box.

#### Acceptance Criteria

1. WHEN a global search term is entered, THE Global_Search SHALL match against the Status_Text of each role.
2. WHEN a role is active, THE Global_Search SHALL use the text "Active" for matching.
3. WHEN a role is inactive, THE Global_Search SHALL use the text "Inactive" for matching.

### Requirement 3: Description Column Remains Visible and Hideable

**User Story:** As an administrator, I want the Description column to remain visible in the grid by default, so that I can see role descriptions at a glance, with the option to hide it.

#### Acceptance Criteria

1. THE Role_Grid SHALL display the Description_Column as a visible column by default.
2. THE Description_Column SHALL support being hidden via the MudDataGrid column visibility options (`Hideable="true"`).

### Requirement 4: Role Details Page

**User Story:** As an administrator, I want to view full details of a role on a dedicated page, so that I can see metadata (created date, last updated) and the list of users assigned to that role without cluttering the grid.

#### Acceptance Criteria

1. THE Role_Details_Page SHALL be accessible at the route `/role-management/{RoleId}`.
2. THE Role_Details_Page SHALL display the following role information:
   - Role Name
   - Display Name
   - Description
   - Status (Active/Inactive chip)
   - Created Date (formatted as "dd/MM/yyyy hh:mm:ss tt")
   - Last Updated Date (formatted as "dd/MM/yyyy hh:mm:ss tt", or "Never" if null)
3. THE Role_Details_Page SHALL display a list of users currently assigned to the role.
4. THE Role_Details_Page SHALL include a back navigation button to return to the Role Management grid.
5. THE Role_Grid Actions column SHALL include a "View Details" icon button that navigates to the Role_Details_Page.

### Requirement 5: Remove Created Date from Global Search

**User Story:** As an administrator, I do not need to search by Created Date since it is not displayed in the grid.

#### Acceptance Criteria

1. THE Global_Search SHALL NOT include Created Date in its searchable fields.
2. THE `GlobalFields` function SHALL include: Role Name, Display Name, Description, User Count, and Status_Text.

### Requirement 6: Bulk Activate and Deactivate Roles

**User Story:** As an administrator, I want to activate or deactivate multiple roles at once, so that I can efficiently manage role availability without toggling each one individually.

#### Acceptance Criteria

1. WHEN one or more roles are selected, THE toolbar SHALL display bulk Activate and Deactivate icon buttons.
2. WHEN the bulk Activate action is triggered, THE system SHALL show a confirmation dialog with the count of roles to be activated.
3. WHEN confirmed, THE system SHALL set `IsActive = true` and stamp `UpdatedUtc` for each selected role.
4. WHEN the bulk Deactivate action is triggered, THE system SHALL show a confirmation dialog with the count of roles to be deactivated.
5. WHEN confirmed, THE system SHALL set `IsActive = false` and stamp `UpdatedUtc` for each selected role.
6. THE bulk Activate and Deactivate icons SHALL use `material-symbols-rounded/person_check` and `material-symbols-rounded/person_cancel` respectively, matching the User Management page for visual consistency.

### Requirement 7: Consistent Activation Icons

**User Story:** As an administrator, I want the activate/deactivate icons in the Role Management grid to match the User Management page, so that the admin panel has a consistent visual language.

#### Acceptance Criteria

1. THE single-row Activate action icon SHALL use `material-symbols-rounded/person_check`.
2. THE single-row Deactivate action icon SHALL use `material-symbols-rounded/person_cancel`.
3. THE icons SHALL replace the current `toggle_on` / `toggle_off` icons.

### Requirement 8: Role Details — Users Data Grid with Assign/Deassign

**User Story:** As an administrator, I want to view, search, and manage users assigned to a role from the Role Details page, so that I can quickly see who has a role and add or remove users without navigating elsewhere.

#### Acceptance Criteria

1. THE Role_Details_Page SHALL display assigned users in a `MudDataGrid` with server-side search and pagination (not a simple table).
2. THE users data grid SHALL display columns: Username, Display Name, Email, and a Remove action button.
3. THE users data grid SHALL include a toolbar search box for filtering users within the role.
4. THE Role_Details_Page SHALL include an "Assign User" button that opens a dialog to search and select users to add to the role.
5. THE "Assign User" dialog SHALL allow searching users by username or display name and SHALL exclude users already assigned to the role.
6. WHEN a user is assigned via the dialog, THE system SHALL call `UserManager.AddToRoleAsync` and reload the users grid.
7. WHEN the Remove action is clicked on a user row, THE system SHALL show a confirmation dialog, then call `UserManager.RemoveFromRoleAsync` and reload the users grid.

### Requirement 9: Line Number on Users Data Grid

**User Story:** As an administrator, I want to see line numbers on the users data grid in the Role Details page, so that I have a consistent visual reference across all grids in the admin panel.

#### Acceptance Criteria

1. THE users data grid SHALL display a page-aware line number as the first column.
2. THE line numbers SHALL recalculate correctly after filtering, sorting, or page changes.

### Requirement 10: Users Data Grid Uses Loader Pattern (No Pre-Loading)

**User Story:** As an administrator, I want the users data grid to always show fresh data after assign/deassign operations, without requiring manual page refresh.

#### Acceptance Criteria

1. THE users data grid SHALL use a `ServerData` callback that fetches users via a `LoadUsersInRoleAsync()` method on every grid reload.
2. THE Role_Details_Page SHALL NOT pre-load users in `OnInitializedAsync` — the grid's `ServerData` callback handles all data loading.
3. AFTER any assign or deassign operation, THE system SHALL call `dataGrid.ReloadServerData()` to refresh the users grid with fresh data.

### Requirement 11: Bulk Deassign Users from Role

**User Story:** As an administrator, I want to select multiple users and remove them from the role in bulk, so that I can efficiently manage role membership without removing users one by one.

#### Acceptance Criteria

1. THE users data grid SHALL support multi-selection via `SelectColumn`.
2. WHEN one or more users are selected, THE toolbar SHALL display a bulk "Remove from Role" action button.
3. WHEN the bulk remove action is triggered, THE system SHALL show a confirmation dialog with the count of users to be removed.
4. WHEN confirmed, THE system SHALL call `UserManager.RemoveFromRoleAsync` for each selected user and reload the grid.
5. THE system SHALL display a summary snackbar with success/failed counts.

### Requirement 12: Assign Multiple Users at Once

**User Story:** As an administrator, I want to select and assign multiple users to the role in a single operation, so that I can efficiently add team members without repeating the dialog for each user.

#### Acceptance Criteria

1. THE "Assign User" dialog SHALL support multi-selection (selecting multiple users at once).
2. THE dialog SHALL display a searchable list/grid of available users (excluding those already assigned).
3. WHEN the admin confirms the selection, THE system SHALL call `UserManager.AddToRoleAsync` for each selected user.
4. THE system SHALL display a summary snackbar with success/failed counts.
5. AFTER assignment, THE users data grid SHALL reload to reflect the newly assigned users.

### Requirement 13: Use UserViewModel for Users Data Grid

**User Story:** As a developer, I want the users data grid on the Role Details page to use `UserViewModel` (from UserManagement) instead of `ApplicationUser`, so that multi-selection works correctly with `Equals`/`GetHashCode` by `Id` and the pattern is consistent across the app.

#### Acceptance Criteria

1. THE users data grid SHALL use `UserViewModel` as its item type (reusing the existing class from UserManagement).
2. THE `DataGridUtils` instance SHALL map `UserViewModel` properties (UserName, DisplayName, Email).
3. THE `LoadUsersInRoleAsync()` method SHALL map `ApplicationUser` entities to `UserViewModel` instances.

### Requirement 14: Separate Action Buttons Row on Role Details Page

**User Story:** As an administrator, I want page-level actions (Back, Assign Users) separated from grid-level bulk actions, so that the toolbar is not visually cluttered.

#### Acceptance Criteria

1. THE Role_Details_Page SHALL have a separate Action Buttons Row above the users data grid containing the "Assign Users" button.
2. THE "Back" button SHALL be placed at the top of the page (above the page title), separate from the Action Buttons Row — consistent with the User Management Details page.
3. THE users data grid toolbar SHALL only contain the search box and bulk actions (shown on selection).
4. THE bulk actions in the toolbar SHALL NOT be placed beside the page-level action buttons.

### Requirement 15: Remove AssignUserToRoleDialog (Single-User)

**User Story:** As a developer, I want to remove the obsolete single-user `AssignUserToRoleDialog` since it has been superseded by the multi-select `AssignUsersToRoleDialog`.

#### Acceptance Criteria

1. THE `AssignUserToRoleDialog.razor` and `AssignUserToRoleDialog.razor.cs` files SHALL be deleted if they exist.
2. ALL references to `AssignUserToRoleDialog` SHALL be removed from the codebase.

### Requirement 16: Allow Role Deactivation Regardless of User Count

**User Story:** As an administrator, I want to deactivate a role even if users are still assigned to it, so that I can temporarily disable a role without first removing all user assignments.

#### Acceptance Criteria

1. THE system SHALL allow deactivating a role regardless of how many users are assigned to it.
2. WHEN deactivating a role that has users assigned, THE confirmation dialog SHALL display a warning: "This role has X user(s) assigned. Deactivating it will not remove their assignment but the role will no longer be active."
3. THE system SHALL NOT block role deactivation based on user count (unlike deletion which is blocked).

### Requirement 17: System Role Protection (IsSystem Flag)

**User Story:** As a system administrator, I want critical roles to be protected from deletion, deactivation, and renaming, so that the system always has functioning administrative and default roles.

#### Acceptance Criteria

1. THE `ApplicationRole` model SHALL have an `IsSystem` boolean property (default: `false`).
2. WHEN `IsSystem` is `true`, THE system SHALL NOT allow the role to be deleted.
3. WHEN `IsSystem` is `true`, THE system SHALL NOT allow the role to be deactivated.
4. WHEN `IsSystem` is `true`, THE system SHALL NOT allow the role name to be changed in the Edit Role dialog.
5. THE Role_Grid Actions column SHALL disable the Delete and Deactivate buttons for system roles.
6. THE system SHALL seed the following system roles on first run with `IsSystem = true`: "Admin" and "User".

### Requirement 18: Minimum One User in Critical Role (RequiresMinimumUser Flag)

**User Story:** As a system administrator, I want the system to prevent removing the last user from roles that require at least one assigned user, so that no one can accidentally lock all administrators out of the system.

#### Acceptance Criteria

1. THE `ApplicationRole` model SHALL have a `RequiresMinimumUser` boolean property (default: `false`).
2. WHEN `RequiresMinimumUser` is `true` AND a user is the last one assigned to that role, THE system SHALL block the removal and display an error: "Cannot remove the last user from the role '{RoleName}'. At least one user must remain assigned."
3. WHEN deactivating a user who is the last one in a role with `RequiresMinimumUser = true`, THE system SHALL block the deactivation and display an error.
4. WHEN deleting a user who is the last one in a role with `RequiresMinimumUser = true`, THE system SHALL block the deletion and display an error.
5. THE guard SHALL apply to: single remove from role, bulk remove from role, single user deactivation, bulk user deactivation, single user deletion, and bulk user deletion.
6. THE system SHALL seed the "Admin" role with `RequiresMinimumUser = true` on first run.
7. THE "User" role SHALL have `RequiresMinimumUser = false` (it can be empty without breaking the system).
8. THE `RequiresMinimumUser` flag SHALL NOT be exposed in the Add/Edit Role UI — it is developer/seed-level only (same as `IsSystem`).

### Requirement 19: Default Role Flag (IsDefault)

**User Story:** As a developer, I want a configurable default role flag so that the system can automatically assign the correct role to new users without hardcoding a role name.

#### Acceptance Criteria

1. THE `ApplicationRole` model SHALL have an `IsDefault` boolean property (default: `false`).
2. WHEN a new user is registered or provisioned (local or LDAP), THE system SHALL assign the role marked `IsDefault = true` instead of hardcoding `"User"`.
3. THE system SHALL seed the "User" role with `IsDefault = true` on first run.
4. THE `IsDefault` flag SHALL NOT be exposed in the Add/Edit Role UI — it is developer/seed-level only.
5. THE Role_Details_Page SHALL display the `IsDefault` flag as a read-only badge when true.
6. ONLY one role SHALL have `IsDefault = true` at any time.

### Requirement 20: Role Position (Authority Hierarchy)

**User Story:** As an administrator, I want roles to have a position value that determines authority level, so that lower-positioned users cannot modify higher-positioned users or assign roles above their own level.

#### Acceptance Criteria

1. THE `ApplicationRole` model SHALL have a `Position` integer property (default: `0`).
2. A higher `Position` value SHALL indicate higher authority.
3. WHEN a user attempts to modify another user (edit, change role, deactivate, delete), THE system SHALL check that the actor's highest role position is greater than or equal to the target user's highest role position.
4. WHEN a user attempts to assign a role to another user, THE system SHALL check that the role's position is less than or equal to the actor's highest role position.
5. THE `Position` property SHALL be editable in the Edit Role dialog by admins.
6. THE Role_Grid SHALL display the Position column (sortable).
7. THE system SHALL seed "Admin" with `Position = 100` and "User" with `Position = 10`.
