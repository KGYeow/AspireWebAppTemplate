# Feature Ideas — Aspire Web App Template

A curated list of pages and features that would complement the existing template. Grouped by priority and category.

---

## High Priority — Common in Every Internal/Enterprise App

### 1. Email Templates & SMTP Configuration
- SMTP email sending with database-stored templates (all-in-database architecture)
- Unified `EmailType` enum for template resolution — no file-based templates
- `IEmailService.SendEmailAsync(EmailType, ...)` as primary sending interface
- `EmailService` implements both `IEmailService` and `IEmailSender<ApplicationUser>` (Identity integration)
- Two categories: System (security emails, read-only) and Business (admin-editable)
- Admin page at `/admin/email-templates` — edit-only (no create/delete), DataGrid with inline navigation to edit form
- Aspire parameter-based secret management for SMTP credentials
- **Spec:** `.kiro/specs/email-smtp-integration/`

#### Deferred Email Templates (Future Enhancement)
The following templates are not in the initial implementation but should be added when the corresponding features or needs arise:

| Template | Category | Storage | Trigger |
|---|---|---|---|
| TwoFactorEnabled | Security | Codebase | When user enables 2FA |
| TwoFactorDisabled | Security | Codebase | When user disables 2FA |
| SuspiciousSignIn | Security | Codebase | Login from unrecognized device/location (requires device fingerprinting) |
| AccountActivated | Administrative | Database | When admin reactivates a user account |
| AdminPasswordReset | Administrative | Database | When admin resets a user's password (distinct from user-initiated) |
| UserInvitation | Business | Database | When admin invites a user to join (requires invitation feature) |
| MaintenanceNotification | System | Database | Planned downtime notice (requires scheduling feature) |

#### Deferred Email Infrastructure (Future Enhancement)
| Enhancement | Description |
|---|---|
| Shared layout/master template | Reusable HTML wrapper (logo, footer, colors) inherited by all emails |
| Plain-text alternative | MultiPart/alternative with text fallback for accessibility |
| Localization | Per-culture template variants with fallback to default |
| Template versioning | Version history with rollback capability |
| Email scheduling | Send emails at a specified future time (requires background jobs) |
| Branding configuration | Admin-configurable logo, colors, and footer applied to all emails |

### 2. Application Settings (Admin)
- Site-wide config stored in DB (site name, logo URL, maintenance mode toggle)
- Feature flags page
- Runtime-configurable settings without redeployment
- Route: `/admin/app-settings`

### 3. ~~Wire Up Notification Triggers~~
- ~~Connect `CreateNotificationAsync` calls to actual user events~~
- Implemented in UserService (account deactivation, password reset by admin) and AnnouncementService (announcement published notifications to all users)
- Excluded by design: role assignment/removal, account activation (per industry standard — no user value, creates noise)

---

## Medium Priority — Enhances Usability

### 4. User Invitation System
- Admin sends invite link via email
- Invitation token with expiry
- Invited user completes registration via link
- Track invitation status (pending, accepted, expired)
- Route: `/admin/invitations`

### 5. File / Avatar Upload
- Profile picture upload with cropping
- Reusable file upload component (drag & drop, progress bar)
- Store in local filesystem or blob storage (configurable)
- Display avatar in topbar profile dropdown and user management

### 6. Session Management
- View active sessions for current user (device, IP, last seen)
- Ability to revoke/sign out other sessions
- Admin view of all active sessions
- Route: `/account/sessions`

### 7. ~~Announcement / Banner System~~
- ~~Admin posts site-wide banners (info, warning, maintenance)~~
- ~~Dismissible by users (remember dismissal)~~
- ~~Scheduled start/end dates~~
- ~~Renders at top of MainLayout~~
- ~~Route: `/admin/announcements`~~
- Fully implemented with multi-surface display (banner + list page), three severity levels, scheduling, per-user dismissal, HTML content editing, and notification integration

### 8. Dashboard / Home Page Widgets
- Replace blank home page with useful widgets:
  - Recent notifications summary
  - Quick stats (user count, active sessions)
  - Recent activity feed
  - System health indicators
- Admin-configurable widget layout

### 9. Help / Documentation Page
- Static markdown-rendered docs or FAQ
- In-app contextual help tooltips
- Version/changelog display
- Route: `/help`

---

## Lower Priority — Nice-to-Have / Progressive Enhancement

### 10. Multi-Tenant Support
- Tenant switcher in topbar (for users in multiple tenants)
- Admin tenant management (create, configure, deactivate)
- Data isolation per tenant
- Route: `/admin/tenants`

### 11. Bulk Import / Export
- Bulk user import from CSV/Excel
- Template download for correct format
- Validation preview before import
- Route: `/admin/bulk-operations`

### 12. System Health / Status Page
- Database connectivity check
- External service health (LDAP, SMTP, Aspire services)
- App version, uptime, memory usage
- Route: `/admin/health`

### 13. Localization / Language Switcher
- Multi-language support (resource files or DB-driven)
- Language preference in user settings
- Admin page to manage translations
- Route: Settings page enhancement + `/admin/translations`

### 14. Password Policy Configuration
- Admin page to configure password rules (min length, complexity, expiry)
- View/edit lockout policy (max attempts, duration)
- Password expiry notifications
- Route: `/admin/security-policies`

### 15. API Key Management
- Users can generate personal API tokens
- Admin can view/revoke all keys
- Scoped permissions per key
- Route: `/account/api-keys`

### 16. Report Builder
- Simple saved queries / report definitions
- Render as table or chart (MudChart)
- Share with roles
- Export to Excel/PDF
- Route: `/reports`

### 17. User Onboarding Wizard
- First-login guided setup (set display name, avatar, timezone)
- Skip-able steps
- Tracks completion state
- Only shows once per user

### 18. Change Log / Release Notes Page
- Markdown-driven list of app changes
- "What's new" badge on first login after update
- Route: `/changelog`

---

## Infrastructure / Non-Page Enhancements

| Enhancement | Description | Priority |
|---|---|---|
| ~~SignalR Real-Time Notifications~~ | Push notification count updates to connected users without polling — implemented via NotificationHub + NotificationContext | ~~High~~ Done |
| Background Job Dashboard | Hangfire/Quartz for scheduled tasks (email sending, cleanup) | Medium |
| Rate Limiting Middleware | Protect login and API endpoints from brute force | Medium |
| CI/CD Pipeline (GitHub Actions) | Build, test, deploy workflow | Medium |
| Docker Support | Production Dockerfile + docker-compose | Medium |
| Structured Logging Dashboard | Seq/ELK viewer embedded or linked | Low |
| Health Checks UI | Custom health checks beyond Aspire defaults | Low |
| Response Caching | Cache static API responses (navigation, roles list) | Low |

---

## Suggested Next Features

Based on what's built and what would add the most value:

1. **Dashboard Widgets** (#8) — The home page is currently empty. Adding a few widgets (recent notifications, quick stats) makes the template feel complete and demonstrates component composition.

2. **Application Settings** (#2) — Admin-configurable site settings stored in DB. Enables runtime changes without redeployment.

3. **User Invitation System** (#4) — Admin sends invite link via email. Natural next step now that SMTP is configured and working.

---

## How to Use This List

1. Pick a feature that interests you
2. Open a new Kiro spec session and describe the feature
3. Walk through requirements → design → tasks
4. Execute the tasks to build it

Each feature above is scoped to be independently implementable without breaking existing functionality.
