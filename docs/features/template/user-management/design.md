# User Management — Design

## Architecture

- `ServerData` callback pattern (not client-side `Items`) for scalability
- `DataGridHelper<T>` as reusable server-side filter/sort/paginate utility
- `UserViewModel` with `Equals`/`GetHashCode` by `Id` for cross-page selection persistence
- Overflow menu for row actions (reduces visual clutter from 5 buttons to 2 elements)
- Bulk actions appear contextually in toolbar only when selection exists
- `BoolFilterSelect<T>` reusable component for bool column dropdown filters
- `UpdatedUtc` stamped on activation toggle for audit trail
- New `HashSet` instance assigned (not `.Clear()`) to trigger Blazor binding updates

## Key Components

- `Index.razor` / `Index.razor.cs` — Main page with data grid
- `AddUserDialog` — Local user creation
- `EditUserDialog` — Edit user profile fields
- `ManageRolesDialog` — Multi-select role assignment
- `BulkAssignRoleDialog` — Bulk role assignment with Add/Replace modes
- `AddLdapUserDialog` — LDAP user provisioning
- `Details.razor` — User details view page

## Data Flow

1. `ServerReload` called by grid on page/filter/sort change
2. `LoadUserViewModelsAsync` fetches all users + roles from Identity
3. `DataGridHelper.ServerReloadAsync` applies filters → search → sort → paginate → line numbers
4. Grid renders paged results
5. Mutations (add/edit/delete/role/activation) call `dataGrid.ReloadServerData()` to refresh
