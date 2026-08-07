# Deployment

## Build

```bash
dotnet publish AspireWebAppTemplate.Web -c Release -o ./publish
```

## Environment Configuration

### Production Settings

Create `appsettings.Production.json` with:
- Production SQL Server connection string
- LDAP settings for production AD
- `AuditLog:RetentionDays` (default: 365)

### Environment Variables

Override settings via environment variables:
```
ConnectionStrings__DefaultConnection=Server=prod-sql;...
LdapSettings__Host=prod-ldap.company.com
```

## Database Migrations

Apply migrations before first deployment:
```bash
dotnet ef database update --project AspireWebAppTemplate.Infrastructure --startup-project AspireWebAppTemplate.ApiService --connection "Server=prod-sql;..."
```

Or use `MigrateAsync()` in Program.cs for automatic migration on startup (development only).

## Health Checks

The application exposes `/health` for monitoring (when configured).

## Security Considerations

- Store connection strings in Azure Key Vault or environment variables (not in appsettings.json)
- Use HTTPS in production
- Configure CORS if exposing any API endpoints
- Set appropriate cookie and session timeouts
- Enable audit logging for compliance
