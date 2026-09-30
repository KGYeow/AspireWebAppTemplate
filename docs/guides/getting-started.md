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
cd AspireWebAppTemplate
```

### 2. Configure the database

Update `AspireWebAppTemplate.ApiService/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=AspireWebAppTemplate;Trusted_Connection=true;"
  }
}
```

### 3. Apply migrations

```bash
dotnet ef database update --project AspireWebAppTemplate.Infrastructure --startup-project AspireWebAppTemplate.ApiService
```

### 4. Run the application

```bash
dotnet run --project AspireWebAppTemplate.AppHost
```

The Aspire dashboard opens at `https://localhost:17024` with links to both the Web frontend and API service.

### 5. Default credentials

After first run, seed data creates:
- Admin role (IsSystem, RequiresMinimumUser)
- User role (IsSystem, IsDefault)

Register a new user through the UI — the first user can be promoted to Admin via the database or SQL.

## LDAP Configuration (Optional)

Update `appsettings.json` with your LDAP settings:

```json
{
  "LDAP": {
    "Enabled": true,
    "Server": "ldaps.company.com",
    "Port": "636",
    "BaseDn": "DC=company,DC=com",
    "Domain": "COMPANY",
    "Path": "LDAP://ldaps.company.com:636"
  }
}
```

## Email / SMTP Configuration (Optional)

Email sending is handled by `EmailService` (`Infrastructure/Services/Email/`), which also satisfies
ASP.NET Core Identity's `IEmailSender<ApplicationUser>`. All email templates (both system-security and
business) are stored in the database and resolved by the `EmailType` enum; `EmailTemplateCategory`
controls editability (System = read-only, Business = admin-editable at `/admin/email-templates`).

Configure the SMTP relay via the `Smtp` section in `appsettings.json`:

```json
{
  "Smtp": {
    "Host": "smtp.company.com",
    "Port": 587,
    "EnableSsl": true,
    "FromAddress": "noreply@company.com",
    "FromName": "MyApp"
  }
}
```

- **Host** — the SMTP server hostname. When empty, `EmailService` runs in **no-op mode**: it logs the
  intended send but does not connect (so the app runs fine with email unconfigured).
- **Port / EnableSsl** — `587` with STARTTLS is typical. `System.Net.Mail.SmtpClient` supports 25/587
  with STARTTLS; it does **not** support implicit TLS (465).
- **FromAddress / FromName** — the sender. Some relays only deliver mail from an approved/provisioned
  mailbox even when they accept anonymous relay — set an address your relay actually delivers for.
- **Credentials** — never commit these. Provide `Smtp__Username` / `Smtp__Password` as environment
  variables (wired through Aspire parameters in `AppHost.cs`). Credentials are applied only when both
  are non-empty; a relay that allows anonymous/IP-based relay needs neither.

## Project Structure

See [Architecture Overview](../architecture/overview.md) for detailed project layout.

## Running Tests

```bash
dotnet test AspireWebAppTemplate.Tests
```

Tests use:
- xUnit as the test framework
- FsCheck 3.3.3 for property-based testing
- SQLite in-memory for data layer tests
- Aspire.Hosting.Testing for integration tests
