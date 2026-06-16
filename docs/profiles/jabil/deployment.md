# Jabil Deployment

## Environment

- **Target**: Internal IIS on Windows Server
- **Network**: Corporate intranet only
- **Database**: SQL Server (Windows Auth)
- **LDAP**: Active Directory on corporate domain

## Build

```bash
dotnet publish AspireWebAppTemplate.Web -c Release -o ./publish
```

## Configuration

### Production appsettings

Create `appsettings.Production.json` (or use environment variables):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=PROD-SQL;Database=AspireWebAppTemplate;Trusted_Connection=true;TrustServerCertificate=true;"
  },
  "LdapSettings": {
    "Host": "ldap.jabil.com",
    "Port": 389,
    "BaseDn": "DC=jabil,DC=com",
    "BindDn": "CN=svc-blazorapp,OU=Service Accounts,DC=jabil,DC=com",
    "BindPassword": "***"
  },
  "AuditLog": {
    "RetentionDays": 365
  }
}
```

### Sensitive values

Store in environment variables or Windows Credential Manager — never commit to source control:
- `LdapSettings__BindPassword`
- `ConnectionStrings__DefaultConnection` (if using SQL auth)

## Database Migration

Apply before first deployment:

```bash
dotnet ef database update --project AspireWebAppTemplate.ApiService --startup-project AspireWebAppTemplate.ApiService --connection "Server=PROD-SQL;..."
```

## IIS Setup

1. Create Application Pool (No Managed Code, Integrated Pipeline)
2. Create Site pointing to `./publish` folder
3. Set Application Pool identity to domain account with SQL access
4. Configure HTTPS binding with internal CA certificate
5. Set `ASPNETCORE_ENVIRONMENT=Production` environment variable

## Verification

After deployment:
1. Navigate to application URL
2. Verify LDAP login works with corporate credentials
3. Verify local login works for service accounts
4. Check User Management page loads with LDAP users
5. Verify theme switching persists across sessions
