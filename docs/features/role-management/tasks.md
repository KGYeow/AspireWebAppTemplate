# Role Management — Completed Tasks

## Phase 1 — Grid Improvements

- [x] Remove Created Date column and update DataGridUtils mapping
- [x] Make Description column hideable
- [x] Update GlobalFields to include Status Text and remove Created Date
- [x] Add View Details icon button to Actions column
- [x] Create Details.razor.cs code-behind
- [x] Create Details.razor page UI

## Phase 2 — Bulk Actions & Icon Consistency

- [x] Add BulkActivateAsync and BulkDeactivateAsync methods
- [x] Add bulk Activate/Deactivate icon buttons to toolbar
- [x] Replace toggle icons with person_check/person_cancel in Actions column

## Phase 3 — Users Data Grid on Role Details

- [x] Refactor Details page to use ServerData pattern with UserViewModel
- [x] Update Details.razor UI with MudDataGrid (title + Assign Users on same row, reduced padding)
- [x] Implement single Remove User from Role action
- [x] Implement bulk Remove Users from Role
- [x] Create AssignUsersToRoleDialog (multi-select)
- [x] Wire up "Assign Users" button to dialog

## Phase 4 — System Role Protection & Authority

- [x] Add IsSystem, RequiresMinimumUser, IsDefault, Position properties + EF migration
- [x] Update seed data (Admin: IsSystem+RequiresMinimumUser+Position100, User: IsSystem+IsDefault+Position10)
- [x] Disable Delete/Deactivate for system roles in grid
- [x] Guard DeleteRoleAsync and BulkDeleteAsync against system roles
- [x] Guard ToggleActivationAsync against system role deactivation
- [x] Guard BulkActivateAsync/BulkDeactivateAsync against system roles
- [x] Guard EditRoleDialog — disable Name and Active fields for system roles
- [x] Guard RemoveUserFromRoleAsync (RequiresMinimumUser check)
- [x] Guard BulkRemoveUsersAsync (RequiresMinimumUser check)
- [x] Guard User Management — deactivate/delete last user in RequiresMinimumUser role
- [x] Replace hardcoded default role with IsDefault query
- [x] Add position check to User Management actions
- [x] Filter assignable roles by position
- [x] Add Position column to Role Management grid
- [x] Add Position field to Edit/Add Role dialogs (Min=0)
- [x] Show IsDefault, IsSystem badges and Position on Role Details page
- [x] UsersInRoleCount in section title (unfiltered count)

## Bug Fixes

- [x] Fixed: Bulk activate/deactivate not filtering system roles
- [x] Fixed: System role can be activated/deactivated via Edit dialog (disabled Active switch for IsSystem)

## UI Polish

- [x] Replaced MudCheckBox with MudSwitch for Active toggle (label left, switch right)
- [x] Used MudInputLabel for consistent font size
- [x] Added Counter + MaxLength on Role Name (50), Display Name (100), Description (500)
- [x] Added Position field to AddRoleDialog with Min=0 and Range validation
- [x] Set Spacing="4" on all 5 dialog form MudGrids for consistent compact spacing

## Earlier (from DEVELOPMENT.md review)

- [x] Server-side filtering, sorting, and pagination
- [x] Line number column
- [x] Bulk delete with UserCount guard and try-catch
- [x] Selection with SelectColumn
- [x] Selection count in toolbar with clear button
- [x] Status column with BoolFilterSelect
- [x] DisplayName column added
- [x] Activate/Deactivate button in Actions column
- [x] Removed unused using static import
