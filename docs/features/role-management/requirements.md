# Role Management — Requirements

## Overview

Admin-only page (`/role-management`) for managing application roles, their descriptions, authority hierarchy, and user assignments.

## Core Requirements

- Server-side filtering, sorting, and pagination via `DataGridUtils<T>`
- Global search across role name, display name, description, user count, and status text
- Per-column filtering (string columns + bool Status column with `BoolFilterSelect`)
- Page-aware line numbering
- Multi-selection with `SelectColumn` (always visible checkboxes)
- Bulk actions: Activate, Deactivate, Delete (with system role and user count guards)
- Single-row actions: View Details, Edit, Activate/Deactivate, Delete
- Add Role dialog (name, display name, description, position, active status)
- Edit Role dialog (same fields, pre-populated, name conflict detection)
- Delete guard: cannot delete roles with users assigned or system roles
- Confirmation dialogs for destructive actions

## Role Details Page (`/role-management/{RoleId}`)

- Displays role metadata: Name, Display Name, Description, Status, Position, Created Date, Updated Date
- Shows IsSystem, IsDefault, RequiresMinimumUser as read-only badges
- Users data grid (server-side with DataGridUtils<UserViewModel>): Username, Display Name, Email, Remove action
- Multi-select bulk deassign users from role
- Assign multiple users dialog (searchable, excludes already-assigned)
- UsersInRoleCount in section title (unfiltered)

## System Role Protection

- `IsSystem` flag: cannot delete, deactivate, or rename system roles
- `RequiresMinimumUser` flag: cannot remove the last user from critical roles
- `IsDefault` flag: marks the auto-assigned role for new users (replaces hardcoded "User")
- `Position` (int): determines authority hierarchy (higher = more authority)
- All flags except Position are developer/seed-level only (not in Add/Edit UI)
- Position is editable by admins, must be >= 0

## Seed Data

- "Admin": IsSystem=true, RequiresMinimumUser=true, IsDefault=false, Position=100
- "User": IsSystem=true, RequiresMinimumUser=false, IsDefault=true, Position=10
