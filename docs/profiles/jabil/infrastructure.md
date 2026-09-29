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

Email is sent via the template's `EmailService` (`System.Net.Mail.SmtpClient`), configured through the
`Smtp` section of configuration. Used for: password reset, email confirmation, account lockout, and
other notification emails.

### Corporate relay pattern

Jabil apps send through an **internal SMTP relay** that authorizes by server/network (no SMTP
credentials) rather than username/password. The sibling South Asia PEN app (eStockAdjustmentForm /
eSAF) uses this exact pattern as a reference point:

- **Relay**: internal relay on `127.0.0.1:25` in eSAF (the app server relays through an internal
  gateway). **Confirm the correct relay host/port for this deployment with internal IT** — it may be a
  shared gateway host rather than localhost.
- **Authentication**: none — the relay authorizes the sending server, so `Smtp:Username` /
  `Smtp:Password` are left empty (the template then connects anonymously).
- **TLS**: a plain `:25` internal relay does not use SSL, so set `Smtp:EnableSsl` to `false`.
- **From address**: a per-app notification mailbox, e.g. eSAF uses `eSAF_Notification@jabil.com`.
  Follow the same convention (e.g. `AspireWebApp_Notification@jabil.com`).

### Configuration (`Smtp` section)

```json
{
  "Smtp": {
    "Host": "127.0.0.1",
    "Port": 25,
    "EnableSsl": false,
    "FromAddress": "AspireWebApp_Notification@jabil.com",
    "FromName": "AspireWebApp"
  }
}
```

> `Username`/`Password` are intentionally omitted so the template connects anonymously (matching the
> relay's server-authorized model). If `Smtp:Host` is left empty, the template runs in no-op mode —
> emails are logged but not sent — so the relay host must be set for production.

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
