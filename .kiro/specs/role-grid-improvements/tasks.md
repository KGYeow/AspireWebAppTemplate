# Implementation Plan: Role Grid Improvements (Phase 4)

## Overview

This plan implements system role protection (`IsSystem` flag), minimum-one-user-in-system-role guards, and the remaining Role Details page enhancements (server-side users grid, bulk deassign, multi-user assign dialog).

## Tasks

- [x] 1. Add IsSystem, RequiresMinimumUser, IsDefault, and Position properties + Migration
  - [x] 1.1 Add all properties and create EF migration
    - In `ApplicationRole.cs`: add `IsSystem` (bool, default false), `RequiresMinimumUser` (bool, default false), `IsDefault` (bool, default false), `Position` (int, default 0) with XML documentation.
    - Run `dotnet ef migrations add AddRoleSystemFlags` to generate the migration.
    - _Requirements: 17.1, 18.1, 19.1, 20.1_

  - [x] 1.2 Update seed data with all role flags
    - In `SeedData.cs`: add `IsSystem`, `RequiresMinimumUser`, `IsDefault`, and `Position` to the `SeedRole` record.
    - "Admin": `IsSystem = true`, `RequiresMinimumUser = true`, `IsDefault = false`, `Position = 100`.
    - "User": `IsSystem = true`, `RequiresMinimumUser = false`, `IsDefault = true`, `Position = 10`.
    - In `SeedRolesAsync`, set all flags when creating the `ApplicationRole`.
    - _Requirements: 17.6, 18.6, 18.7, 19.3, 20.7_

- [x] 2. System Role Guards — Role Management
  - [x] 2.1 Disable Delete/Deactivate for system roles in grid
    - In `Index.razor`: disable Delete and Deactivate buttons when `context.Item.IsSystem` is true.
    - Add `IsSystem` property to `RoleViewModel` and map it in `LoadRoleViewModelsAsync`.
    - _Requirements: 17.5_

  - [x] 2.2 Guard DeleteRoleAsync and BulkDeleteAsync against system roles
    - In `Index.razor.cs`: `DeleteRoleAsync` — block with error if role is system.
    - `BulkDeleteAsync` — skip system roles and report as "skipped (system role)".
    - _Requirements: 17.2_

  - [x] 2.3 Guard ToggleActivationAsync against system role deactivation
    - In `Index.razor.cs`: block deactivation if `IsSystem = true` with error snackbar.
    - _Requirements: 17.3_

  - [x] 2.4 Guard EditRoleDialog — disable Name field for system roles
    - In `EditRoleDialog.razor.cs`: add `IsSystem` parameter.
    - In `EditRoleDialog.razor`: disable the Name field when `IsSystem = true`.
    - _Requirements: 17.4_

- [x] 3. Minimum One User Guards (RequiresMinimumUser)
  - [x] 3.1 Guard RemoveUserFromRoleAsync (Role Details page)
    - In `Details.razor.cs`: before removing, check if the role has `RequiresMinimumUser = true` AND the user is the last one assigned.
    - If so, block with error: "Cannot remove the last user from the role '{RoleName}'. At least one user must remain assigned."
    - _Requirements: 18.1, 18.2_

  - [x] 3.2 Guard BulkRemoveUsersAsync (Role Details page)
    - In `Details.razor.cs`: before bulk remove, check if removing all selected users would leave a `RequiresMinimumUser` role empty.
    - If so, block with error.
    - _Requirements: 18.1, 18.2, 18.5_

  - [x] 3.3 Guard User Management — deactivate/delete last user in RequiresMinimumUser role
    - In `UserManagement/Index.razor.cs`: `ToggleActivationAsync`, `DeleteUserAsync`, `BulkDeactivateAsync`, `BulkDeleteAsync` — before proceeding, check if the user is the last one in any role with `RequiresMinimumUser = true`.
    - If so, block with error: "Cannot deactivate/delete the last user in role '{RoleName}'."
    - _Requirements: 18.3, 18.4, 18.5_

- [x] 4. Replace Hardcoded Default Role with IsDefault Query
  - [x] 4.1 Update registration and provisioning services
    - In `RegisterService`, `LdapLoginService`, `AddLdapUserDialog`, `AddUserDialog`: replace hardcoded `"User"` with a query for the role where `IsDefault = true`.
    - Add fallback: if no role has `IsDefault = true`, fall back to `"User"`.
    - _Requirements: 19.2_

- [x] 5. Position-Based Authority Guards
  - [x] 5.1 Add position check to User Management actions
    - In `UserManagement/Index.razor.cs`: before edit/role-change/deactivate/delete, check that actor's highest role position >= target user's highest role position.
    - Block with error if actor position is lower.
    - _Requirements: 20.3_

  - [x] 5.2 Filter assignable roles by position
    - In `ManageRolesDialog` and `BulkAssignRoleDialog`: filter available roles to only those with position <= actor's highest role position.
    - _Requirements: 20.4_

- [x] 6. UI Updates for Position and IsDefault
  - [x] 6.1 Add Position column to Role Management grid
    - In `RoleManagement/Index.razor`: add `PropertyColumn` for Position (sortable).
    - In `RoleManagement/Index.razor.cs`: add `MapInt` for Position in `DataGridUtils`.
    - _Requirements: 20.6_

  - [x] 6.2 Add Position field to Edit Role dialog
    - In `EditRoleDialog.razor`: add a number field for Position.
    - In `EditRoleDialog.razor.cs`: add Position to the InputModel.
    - _Requirements: 20.5_

  - [x] 6.3 Show IsDefault and Position on Role Details page
    - In `Details.razor`: show "Default Role" badge when `IsDefault = true`. Show Position value in role info section.
    - _Requirements: 19.5, 20.6_

- [x] 7. Final checkpoint - Verify all changes compile and work together
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- `IsSystem` protects the role itself (cannot delete/deactivate/rename). `RequiresMinimumUser` protects system access (cannot remove the last user).
- `IsDefault` marks the auto-assigned role for new users (replaces hardcoded "User" string).
- `Position` determines authority hierarchy (higher = more authority). Guards prevent lower-positioned users from managing higher-positioned ones.
- All flags except `Position` are developer/seed-level only — not exposed in the Add/Edit Role UI.
- `Position` is editable by admins in the Edit Role dialog.
- "Admin": `IsSystem = true`, `RequiresMinimumUser = true`, `IsDefault = false`, `Position = 100`.
- "User": `IsSystem = true`, `RequiresMinimumUser = false`, `IsDefault = true`, `Position = 10`.
- EF Core migration is required for the four new columns.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "2.1", "2.2", "2.3", "2.4"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "4.1", "5.1", "5.2", "6.1", "6.2", "6.3"] }
  ]
}
```
