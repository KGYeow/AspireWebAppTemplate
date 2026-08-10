# User Management — Requirements

## Overview

Admin-only page (`/user-management`) for managing user accounts, roles, and status.

---

## Requirements

- Server-side filtering, sorting, and pagination via `DataGridHelper<T>`
- Global search across username, display name, job title, department, and roles
- Per-column filtering (string columns + bool Status column with `BoolFilterSelect`)
- Page-aware line numbering
- Multi-selection with `SelectColumn` (always visible checkboxes)
- Bulk actions: Activate, Deactivate, Assign Role, Delete (shown on selection)
- Clear selection button with count (`[✕ N selected]`)
- Single-row actions via overflow menu (`[View] + [⋮ Edit | Manage Roles | Activate/Deactivate | Delete]`)
- LDAP integration: Add LDAP User, Sync Users (with progress bar and cancel)
- LDAP sync fetches: displayName, givenName, sn, title, department, mail, samaccountname, employeeNumber
- Confirmation dialogs for all destructive actions
- Self-protection: logged-in user cannot modify/delete their own account
- Bulk Assign Role supports Add mode (default) and Replace mode (optional toggle)
