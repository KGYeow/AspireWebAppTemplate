# Role Management — Design

## Architecture

- Same `DataGridUtils<T>` + `ServerData` pattern as UserManagement
- `RoleViewModel` with `Equals`/`GetHashCode` by `Id` for selection persistence
- `ModalDialog` shared component for Add/Edit dialogs
- `ApplicationRole` extends `IdentityRole` with: DisplayName, Description, IsActive, IsSystem, RequiresMinimumUser, IsDefault, Position, CreatedUtc, UpdatedUtc

## Key Components

- `Index.razor` / `Index.razor.cs` — Main grid page with bulk actions
- `Details.razor` / `Details.razor.cs` — Role details with users data grid
- `AddRoleDialog` — Role creation (Counter/MaxLength, Position Min=0, MudSwitch for Active)
- `EditRoleDialog` — Edit role (system role guards: disable Name, Active, Position for IsSystem)
- `AssignUsersToRoleDialog` — Multi-select user assignment

## Authority Guards

- Position-based: actor's highest role position must be >= target's to modify
- Assignable roles filtered to position <= actor's highest
- System roles: cannot delete/deactivate/rename
- RequiresMinimumUser: cannot remove last user from critical roles
- Guards apply across both Role Management and User Management pages

## Data Flow

1. `ServerReload` → `LoadRoleViewModelsAsync` → `DataGridUtils.ServerReloadAsync`
2. Details page: `ServerReloadUsers` → `LoadUsersInRoleAsync` (maps to UserViewModel) → `DataGridUtils.ServerReloadAsync`
3. Mutations → `dataGrid.ReloadServerData()` / `usersDataGrid.ReloadServerData()`
