# Feature Ideas — Aspire Web App Template

A curated list of pages and features that would complement the existing template. Grouped by priority and category.

---

## What Already Exists

| Page / Feature | Path | Notes |
|---|---|---|
| Home (landing) | `/` | |
| User Profile (view + edit) | `/account/profile` | |
| Settings (theme, timezone, date format, notifications) | `/account/settings/*` | Tabbed layout with profile, appearance, regional, notifications |
| Notifications (inbox + preferences) | `/account/notifications` | Bell dropdown, click-to-expand detail, mark as read, bulk dismiss, category/status filters |
| User Management (CRUD, LDAP, bulk ops) | `/admin/user-management` | Server-side grid, multi-select, bulk activate/deactivate/delete/role-assign |
| Role Management (CRUD, user assignment) | `/admin/role-management` | Position hierarchy, user assignment dialog |
| Audit Log (searchable, filterable, export) | `/admin/audit-log` | Old/new value tracking, Excel export |
| Page Permissions (role × page matrix) | `/admin/page-permissions` | Bordered matrix grid, per-circuit cache, nav filtering |
| Auth (login, register, 2FA, passkeys, password reset) | `/Account/*` | Full ASP.NET Core Identity flow + LDAP |
| Theme Switching (light/dark/system) | Built into layout + settings | Real-time toggle, per-user DB persistence |
| StatusAlert (reusable component) | UI library | Self-hiding, @bind-Message, dismissible, dense mode |
| Example pages (counter, weather + notification test) | `/counter`, `/weather`, `/auth` | Weather page includes notification testing |

### Recently Completed (This Session)

- **AWS AI Integration** — Provider-agnostic AI text generation service via Amazon Bedrock (Nova 2 Lite), with three-tier credential resolution, Aspire parameter-based secrets, and structured error handling
- StatusAlert component created and deployed across all pages
- Service registration extensions (`AddApiClients()`, `AddApplicationServices()`)
- SystemPageDefaults expanded (all self-service pages bypass permissions)
- RedirectToLogin → AccessDenied for authenticated users without permission
- Notification bell redesign (MudMenu, unread background, click-to-expand-and-read)
- AssetDefaults centralized (logos, backgrounds)
- Theme separation (DefaultTheme + JabilTheme from brand guidelines)
- Notification settings table layout (MudSimpleTable)
- Removed Bordered from data grids (except page permissions matrix)
- Settings nav icon color fix (IconColor.Inherit)

---

## High Priority — Common in Every Internal/Enterprise App

### 1. Email Templates & SMTP Configuration 🔧 IN PROGRESS
- Replace `NoOpEmailSender` with real SMTP implementation
- Two-tier template architecture: system security (codebase) + business (database, admin-editable)
- Preview/test email sending from admin UI
- Aspire parameter-based secret management for SMTP credentials
- Route: `/admin/email-templates`
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

### 3. ~~Wire Up Notification Triggers~~ ✅ COMPLETED
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

### 7. ~~Announcement / Banner System~~ ✅ COMPLETED
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
| ~~SignalR Real-Time Notifications~~ ✅ | Push notification count updates to connected users without polling — implemented via NotificationHub + NotificationContext | ~~High~~ Done |
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

1. **Email Templates & SMTP** (#1) — Low-to-medium effort. Replaces `NoOpEmailSender` with a real implementation, making password resets and account confirmations actually work.

2. **Dashboard Widgets** (#8) — The home page is currently empty. Adding a few widgets (recent notifications, quick stats) makes the template feel complete and demonstrates component composition.

3. **Application Settings** (#2) — Admin-configurable site settings stored in DB. Enables runtime changes without redeployment.

4. **User Invitation System** (#4) — Admin sends invite link via email. Natural next step after SMTP is configured.

---

## How to Use This List

1. Pick a feature that interests you
2. Open a new Kiro spec session and describe the feature
3. Walk through requirements → design → tasks
4. Execute the tasks to build it

Each feature above is scoped to be independently implementable without breaking existing functionality.
