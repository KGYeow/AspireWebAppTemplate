# Personal Infrastructure

## Authentication

### Local Identity Only

Personal deployments use ASP.NET Core Identity with local username/password accounts. No LDAP integration is needed.

**Configuration**: No `LdapSettings` section required in appsettings.json. The LDAP services can be removed from DI registration if desired.

### Removing LDAP (Optional)

If you want a cleaner codebase for personal use:

1. Remove `ILdapAuthService`, `ILdapLoginService` interfaces from `Abstractions/`
2. Remove `LdapAuthService`, `LdapLoginService` from `Services/`
3. Remove `LdapSettings` from `Options/`
4. Remove LDAP service registrations from `Program.cs`
5. Remove "Add LDAP User" and "Sync Users" buttons from User Management page
6. Remove `System.DirectoryServices` package references from `.csproj`

This is completely optional — the LDAP code doesn't interfere if unconfigured.

## Database

### Development

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=AspireWebAppTemplate;Trusted_Connection=true;"
  }
}
```

### Production Options

| Provider | Use Case |
|----------|----------|
| SQL Server (LocalDB) | Local development |
| SQL Server Express | Self-hosted production |
| Azure SQL | Cloud hosting |
| PostgreSQL | Alternative (requires EF Core provider swap) |

## Email / SMTP

Options for personal deployments:
- **Development**: Use `IdentityNoOpEmailSender` (emails logged, not sent)
- **Production**: Configure any SMTP provider (SendGrid, Mailgun, Gmail SMTP)

## Hosting Options

| Platform | Notes |
|----------|-------|
| Azure App Service | Easiest for .NET apps, free tier available |
| Docker + VPS | DigitalOcean, Linode, Hetzner |
| Self-hosted IIS | If you have a Windows server |
| Railway / Render | Simple container hosting |

## Features Enabled

| Feature | Status | Notes |
|---------|--------|-------|
| LDAP Authentication | ❌ Disabled | Not needed for personal use |
| Local Authentication | ✅ Active | Primary auth method |
| Audit Log | Optional | Enable if needed for client compliance |
| LDAP Sync | ❌ Disabled | No AD to sync from |
