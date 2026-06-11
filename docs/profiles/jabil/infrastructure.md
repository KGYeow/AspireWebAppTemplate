# Jabil Infrastructure

## Authentication

### LDAP / Active Directory

Jabil deployments use LDAP authentication alongside local Identity accounts. LDAP allows employees to log in with their corporate credentials.

**Configuration** (`appsettings.json`):

```json
{
  "LdapSettings": {
    "Host": "ldap.jabil.com",
    "Port": 389,
    "BaseDn": "DC=jabil,DC=com",
    "BindDn": "CN=svc-blazorapp,OU=Service Accounts,DC=jabil,DC=com",
    "BindPassword": "*** (use environment variable or Key Vault in production)"
  }
}
```

### LDAP Attributes Synced

| LDAP Attribute | ApplicationUser Property |
|---------------|------------------------|
| `displayName` | DisplayName |
| `givenName` | FirstName |
| `sn` | LastName |
| `mail` | Email |
| `title` | JobTitle |
| `department` | Department |
| `employeeNumber` | EmployeeNumber |
| `samaccountname` | UserName |

### LDAP User Behavior

- LDAP-synced fields are read-only on the Profile page (managed by AD)
- Non-LDAP fields (PhoneNumber, TimeZoneId, Locale, Theme) remain editable
- Users can be provisioned via the "Add LDAP User" dialog in User Management
- Bulk LDAP sync fetches and updates all LDAP users

## Database

- **Server**: Internal SQL Server instance
- **Connection**: Windows Authentication (Trusted_Connection) on corporate network
- **Migrations**: Applied manually before deployment via `dotnet ef database update`

## Email / SMTP

- Corporate SMTP relay (configured via internal IT)
- Used for: password reset, email confirmation, account lockout notifications

## Hosting

- Internal IIS or Windows Server hosting
- Corporate network access only (no public internet exposure)
- HTTPS via internal CA certificate

## Branding

- ApplicationTheme uses Jabil Blue (`#003B6B`) as primary color
- See [brand-guidelines.md](./brand-guidelines.md) for full Jabil brand specifications
- Roboto font family (consistent with Jabil brand typography)

## Features Enabled

| Feature | Status | Notes |
|---------|--------|-------|
| LDAP Authentication | ✅ Active | Primary auth for employees |
| Local Authentication | ✅ Active | Fallback for service accounts |
| Audit Log | 📋 Planned | Required for compliance |
| LDAP Sync | ✅ Active | Bulk import/update from AD |
