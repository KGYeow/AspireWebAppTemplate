# Data Layer

## Entity Framework Core

The application uses EF Core 10.0.3 with SQL Server as the database provider.

### ApplicationDbContext

Located at `BlazorWebAppTemplate/Data/ApplicationDbContext.cs`, extends `IdentityDbContext<ApplicationUser, ApplicationRole, string>`.

### Key Entities

| Entity | Table | Purpose |
|--------|-------|---------|
| `ApplicationUser` | AspNetUsers | Extended Identity user with profile, preferences, and LDAP fields |
| `ApplicationRole` | AspNetRoles | Extended Identity role with IsSystem, IsDefault, Position, display metadata |
| `AuditLogEntry` | AuditLogEntries | Audit trail records (planned) |

### Conventions

- Enum properties stored as strings via `HasConversion<string>()`
- UTC timestamps with `HasDefaultValueSql("GETUTCDATE()")`
- Indexes on frequently queried columns
- Restrict delete on foreign keys to preserve audit history
- Seed data for system roles (Admin, User)

### Migrations

Run migrations with:
```bash
dotnet ef migrations add MigrationName --project BlazorWebAppTemplate
dotnet ef database update --project BlazorWebAppTemplate
```

### Configuration (appsettings.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=BlazorWebAppTemplate;..."
  },
  "LdapSettings": { ... },
  "AuditLog": {
    "RetentionDays": 365
  }
}
```
