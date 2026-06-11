# Feature Ideas — Blazor Web App Template

A curated list of pages and features that would complement the existing template. Grouped by priority and category.

---

## What Already Exists

| Page / Feature | Path |
|---|---|
| Home (landing) | `/` |
| User Profile (view + edit) | `/profile` |
| Settings (theme, timezone, date format) | `/settings` (instant-save) |
| User Management (CRUD, LDAP import, bulk role assign) | `/user-management` |
| Role Management (CRUD, user assignment) | `/role-management` |
| Full Auth flow (login, register, 2FA, passkeys, password reset, lockout) | `/Account/*` |
| Theme Switching (light/dark/system, real-time) | Built into layout + settings |
| Example pages (counter, weather, auth status) | `/counter`, `/weather`, `/auth` |

---

## High Priority — Common in Every Internal/Enterprise App

### ~~1. Dashboard / Analytics Page~~ (Not suitable for template)
> A dashboard is inherently domain-specific — the metrics, cards, and charts depend on what the consuming app actually does. Including one in the template would be either too generic or too opinionated. Better left to the real project implementation.

### 2. Audit Log / Activity Feed
- Table showing who did what and when (user CRUD, role changes, logins)
- Filterable by user, action type, date range
- Export to CSV/Excel
- Route: `/audit-log`

### 3. Notification System
- In-app notification bell in the topbar
- Notification preferences page (email, in-app toggles)
- Mark as read, bulk dismiss
- Route: `/notifications`

### 4. Email Templates & SMTP Configuration
- Admin page to configure SMTP settings (stored in DB, not just appsettings)
- Preview/test email sending
- Customizable email templates for password reset, account confirmation, etc.
- Route: `/admin/email-settings`

### 5. Application Settings (Admin)
- Site-wide config stored in DB (site name, logo URL, maintenance mode toggle)
- Feature flags page
- Route: `/admin/app-settings`

---

## Medium Priority — Enhances Usability

### 6. User Invitation System
- Admin sends invite link via email
- Invitation token with expiry
- Invited user completes registration via link
- Route: `/admin/invitations`

### 7. File / Avatar Upload
- Profile picture upload with cropping
- Reusable file upload component (drag & drop, progress bar)
- Store in local filesystem or blob storage (configurable)

### 8. Session Management
- View active sessions for current user (device, IP, last seen)
- Ability to revoke/sign out other sessions
- Route: `/account/sessions`

### 9. Multi-Tenant Support Page
- Tenant switcher in topbar (for users in multiple tenants)
- Admin tenant management (create, configure, deactivate)
- Route: `/admin/tenants`

### 10. Help / Documentation Page
- Static markdown-rendered docs or FAQ
- In-app contextual help tooltips
- Route: `/help`

---

## Lower Priority — Nice-to-Have / Progressive Enhancement

### 11. Data Export Page
- Export users, roles, audit logs to CSV/Excel/PDF
- Scheduled exports (background job)
- Route: `/admin/exports`

### 12. Bulk Operations Page
- Bulk user import from CSV
- Bulk deactivate/activate users
- Bulk password reset
- Route: `/admin/bulk-operations`

### 13. System Health / Status Page
- Database connectivity check
- External service health (LDAP, SMTP)
- App version, uptime, memory usage
- Route: `/admin/health`

### 14. Localization / Language Switcher
- Multi-language support (resource files or DB-driven)
- Language preference in user settings
- Admin page to manage translations
- Route: Settings page enhancement + `/admin/translations`

### 15. Password Policy Configuration
- Admin page to configure password rules (min length, complexity, expiry)
- View/edit lockout policy (max attempts, duration)
- Route: `/admin/security-policies`

### 16. API Key Management
- Users can generate personal API tokens
- Admin can view/revoke all keys
- Scoped permissions per key
- Route: `/account/api-keys`

### 17. Announcement / Banner System
- Admin posts site-wide banners (info, warning, maintenance)
- Dismissible by users
- Scheduled start/end dates
- Route: `/admin/announcements`

### 18. Report Builder (CRUD)
- Simple saved queries / report definitions
- Render as table or chart
- Share with roles
- Route: `/reports`

### 19. User Onboarding Wizard
- First-login guided setup (set display name, avatar, timezone)
- Skip-able steps
- Tracks completion state

### 20. Change Log / Release Notes Page
- Markdown-driven list of app changes
- Highlights new features on login
- Route: `/changelog`

---

## Infrastructure / Non-Page Enhancements

| Enhancement | Description |
|---|---|
| Background Job Dashboard | Hangfire/Quartz dashboard for scheduled tasks |
| SignalR Real-Time Notifications | Push updates to connected users |
| Rate Limiting Middleware | Protect login and API endpoints |
| Structured Logging Dashboard | Seq/ELK viewer embedded or linked |
| CI/CD Pipeline (GitHub Actions) | Build, test, deploy workflow |
| Docker Support | Dockerfile + docker-compose for local dev |
| OpenAPI / Swagger | Document any REST endpoints |
| Health Checks Endpoint | `/health` with ASP.NET Core health checks |

---

## How to Use This List

1. Pick a feature that interests you
2. Open a new Kiro spec session and describe the feature
3. Walk through requirements → design → tasks
4. Execute the tasks to build it

Each feature above is scoped to be independently implementable without breaking existing functionality.
