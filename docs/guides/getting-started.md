# Getting Started

## Prerequisites

- .NET 10.0 SDK
- SQL Server (LocalDB, Express, or full instance)
- Visual Studio 2022+ or VS Code with C# Dev Kit
- Node.js (optional — only if modifying JS interop modules)

## Setup

### 1. Clone the repository

```bash
git clone <repository-url>
cd BlazorWebAppTemplate
```

### 2. Configure the database

Update `BlazorWebAppTemplate/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=BlazorWebAppTemplate;Trusted_Connection=true;"
  }
}
```

### 3. Apply migrations

```bash
dotnet ef database update --project BlazorWebAppTemplate
```

### 4. Run the application

```bash
dotnet run --project BlazorWebAppTemplate
```

Navigate to `https://localhost:5001`

### 5. Default credentials

After first run, seed data creates:
- Admin role (IsSystem, RequiresMinimumUser)
- User role (IsSystem, IsDefault)

Register a new user through the UI — the first user can be promoted to Admin via the database or SQL.

## LDAP Configuration (Optional)

Update `appsettings.json` with your LDAP settings:

```json
{
  "LdapSettings": {
    "Host": "ldap.company.com",
    "Port": 389,
    "BaseDn": "DC=company,DC=com",
    "BindDn": "CN=svc-app,OU=Service Accounts,DC=company,DC=com",
    "BindPassword": "..."
  }
}
```

## Project Structure

See [Architecture Overview](../architecture/overview.md) for detailed project layout.

## Running Tests

```bash
dotnet test BlazorWebAppTemplate.Tests
```

Tests use:
- xUnit as the test framework
- FsCheck 3.1.0 for property-based testing
- In-memory database providers for data layer tests
