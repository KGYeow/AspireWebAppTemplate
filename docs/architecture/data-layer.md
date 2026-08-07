# Data Layer

## Entity Framework Core

The application uses EF Core 10 with SQL Server as the database provider.

### ApplicationDbContext

Located at `AspireWebAppTemplate.Infrastructure/Data/ApplicationDbContext.cs`, extends `IdentityDbContext<ApplicationUser, ApplicationRole, string>`.

### Key Entities

| Entity | Table | Location | Purpose |
|--------|-------|----------|---------|
| `ApplicationUser` | AspNetUsers | `Infrastructure/Identity/` | Extended Identity user with profile, preferences, and LDAP fields |
| `ApplicationRole` | AspNetRoles | `Infrastructure/Identity/` | Extended Identity role with IsSystem, IsDefault, Position, display metadata |
| `Announcement` | Announcements | `Infrastructure/Data/Entities/` | Multi-surface announcements with scheduling, severity, and HTML content |
| `AnnouncementDismissal` | AnnouncementDismissals | `Infrastructure/Data/Entities/` | Per-user announcement dismissal tracking |
| `AuditLogEntry` | AuditLogEntries | `Infrastructure/Data/Entities/` | Security-sensitive audit trail records with old/new value change tracking |
| `Notification` | Notifications | `Infrastructure/Data/Entities/` | In-app notifications with category, read state, and deep-link support |
| `NotificationPreference` | NotificationPreferences | `Infrastructure/Data/Entities/` | Per-user, per-category notification channel preferences |
| `PagePermission` | PagePermissions | `Infrastructure/Data/Entities/` | Database-driven page access whitelist (role-based) |
| `EmailTemplate` | EmailTemplates | `Domain/Entities/` | Email templates resolved by EmailType enum, stored in database |

### Entity Type Configurations

Located at `AspireWebAppTemplate.Infrastructure/Data/Configurations/`. Each entity has a dedicated `IEntityTypeConfiguration<T>` class defining indexes, constraints, conversions, and relationships.

### Conventions

- Enum properties stored as strings via `HasConversion<string>()`
- UTC timestamps with `HasDefaultValueSql("GETUTCDATE()")`
- Indexes on frequently queried columns
- Restrict delete on foreign keys to preserve audit history
- Seed data for system roles (Admin, User) and email templates

### Migrations

Run migrations with:
```bash
dotnet ef migrations add MigrationName --project AspireWebAppTemplate.Infrastructure --startup-project AspireWebAppTemplate.ApiService
dotnet ef database update --project AspireWebAppTemplate.Infrastructure --startup-project AspireWebAppTemplate.ApiService
```

### Configuration (appsettings.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=AspireWebAppTemplate;..."
  },
  "LdapSettings": { ... },
  "AuditLog": {
    "RetentionDays": 365
  }
}
```
