# Tech Stack

## Runtime & Framework
- **.NET 10** — all projects target net10.0
- **Blazor Server** — interactive UI via SignalR circuits (not WASM)
- **.NET Aspire** — service orchestration, discovery, telemetry, health checks

## Frontend
- **MudBlazor 9.x** — Material Design UI component library
- **CSS isolation** — scoped `.razor.css` files per component
- **MudBlazor utility classes** — spacing, flex, alignment (no custom CSS frameworks)

## Backend
- **ASP.NET Core Web API** — REST controllers with JSON responses
- **Entity Framework Core 10** — code-first with SQL Server
- **ASP.NET Core Identity** — user/role management, password hashing, claims
- **System.DirectoryServices** — LDAP/Active Directory integration

## Database
- **SQL Server** — production database
- **SQLite in-memory** — test database (via Microsoft.EntityFrameworkCore.Sqlite)
- **EF Core Migrations** — schema management

## Testing
- **xUnit** — test runner
- **FsCheck.Xunit 3.3.3** — property-based testing
- **Moq** — mocking framework
- **Aspire.Hosting.Testing** — integration test hosting

## Key Libraries
- **EPPlus** — Excel export (in ApiService)
- **System.Text.Json** — JSON serialization (camelCase policy for audit values)

## Development
- **Visual Studio / VS Code / Kiro** — IDE
- **dotnet CLI** — build, test, migrations
- **Aspire AppHost** — local dev orchestrator (start with `dotnet run` in AppHost)
