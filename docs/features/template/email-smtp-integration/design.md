# Email & SMTP Integration — Design

## Overview

The application sends transactional email over SMTP using database-stored templates. A single
`EmailService` implements both the app's `IEmailService` and ASP.NET Core Identity's
`IEmailSender<ApplicationUser>`, so Identity flows (email confirmation, password reset, 2FA) and
business notifications share one sender. Every template — system and business alike — lives in the
database and is resolved by the `EmailType` enum. When SMTP is not configured the service runs in a
safe no-op mode, so the application works out of the box without a mail relay.

## Architecture

### Layering

- **Domain** — `EmailType` (`Domain/Enums/EmailType.cs`) and `EmailTemplateCategory` (`Domain/Enums/`). `EmailTemplate` entity (`Domain/Entities/EmailTemplate.cs`).
- **Application** (`Application/Features/Email/`) — `IEmailService` and `IEmailTemplateService` at the feature root; DTOs in `Contracts/` (`SendEmailRequest`, `TrySendEmailRequest`, `EmailTemplateDto`, `UpdateEmailTemplateRequest`, `RenderedEmailResult`, `PreviewTemplateRequest`).
- **Infrastructure** (`Infrastructure/Services/Email/EmailService.cs`, `EmailTemplateService.cs`) — SMTP sending and template resolution/rendering/management over `ApplicationDbContext`.
- **ApiService** — `EmailTemplateController` (thin; query/edit/preview endpoints). There is intentionally **no** create or delete endpoint.
- **Web** — `ApiEmailTemplateService` (typed client) and the admin page at `/admin/email-templates`.

Both services are registered **scoped**. `EmailService` is registered for `IEmailService` and for `IEmailSender<ApplicationUser>` (replacing the framework's `NoOpEmailSender`).

### Single-tier template model

All templates are stored in the database — there are no on-disk/Razor templates. `EmailType` defines
the fixed set (one template per type, seeded on first deployment via `SeedData.EmailTemplates`).
`EmailTemplateCategory` governs **editability, not storage**:

- **System** (PasswordReset, EmailConfirmation, TwoFactorCode, AccountLockout, EmailChanged, PasswordChanged) — read-only at runtime; the admin UI/API rejects edits.
- **Business** (WelcomeEmail, AccountDeactivated, CustomNotification) — admins can edit content via `/admin/email-templates`, but cannot create or delete (edit-only). `AccountDeactivated` seeds `IsActive=false` by default.

Adding a new email type requires a code change (new `EmailType` value + seed entry).

### Sending flow

```
Caller → IEmailService.SendEmailAsync(SendEmailRequest{ EmailType, RecipientEmail, Variables })
      or IEmailService.TrySendEmailAsync(TrySendEmailRequest{ ... , UserId, Category })
        v
IEmailTemplateService.RenderAsync(EmailType, variables)   // DB lookup + {{placeholder}} replacement
        v
SmtpClient (System.Net.Mail) → SMTP relay      // no-op + log when Smtp:Host is empty
```

- `SendEmailAsync` throws on failure (`KeyNotFoundException` if no active template; `InvalidOperationException` on SMTP error).
- `TrySendEmailAsync` is best-effort: it checks the user's `EmailEnabled` preference for the notification `Category` and never throws — failures are logged so primary operations are never disrupted.
- Identity integration methods (`SendConfirmationLinkAsync`, `SendPasswordResetLinkAsync`, `SendPasswordResetCodeAsync`) delegate to `SendEmailAsync` with the matching `EmailType`.
- Recipient addresses are masked in logs (first 3 chars + domain).

## Components and Interfaces

### IEmailService (`Application/Features/Email/`)

| Method | Behavior |
|---|---|
| `SendEmailAsync(SendEmailRequest)` | Resolve template by `EmailType`, render, send. Throws on missing template / SMTP failure. |
| `TrySendEmailAsync(TrySendEmailRequest)` | Respects the user's per-category `EmailEnabled` preference; best-effort, never throws. |

### IEmailTemplateService (`Application/Features/Email/`)

| Method | Behavior |
|---|---|
| `RenderAsync(EmailType, variables)` | DB template lookup + `{{placeholder}}` replacement on subject and body → `RenderedEmailResult`. Throws if no active template. |
| `RenderPreviewAsync(templateId, sampleData)` | Renders any template with sample data for admin preview. |
| `GetAllAsync()` / `GetByIdAsync(id)` | Query templates. |
| `UpdateAsync(id, UpdateEmailTemplateRequest)` | The only mutation. Rejects system-template edits (`InvalidOperationException`). No create/delete. |

### EmailTemplateController (`ApiService/Controllers/`) & admin UI

Thin controller exposing GET (all / by id), PUT (update business template), and POST (preview) — no
create/delete. The Web admin page `/admin/email-templates` lists templates in a DataGrid and edits
business templates with an HTML editor; system templates are shown read-only.

## Configuration

SMTP settings are read from the `Smtp` section of `appsettings.json`:

```json
{
  "Smtp": {
    "Host": "smtp.company.com",
    "Port": 587,
    "EnableSsl": true,
    "FromAddress": "noreply@company.com",
    "FromName": "MyApp"
  }
}
```

- **Host** — when empty/missing, `EmailService` runs in **no-op mode** (logs the intended send, does not connect), so the app runs without a relay configured.
- **Port / EnableSsl** — `587` + STARTTLS is typical. `System.Net.Mail.SmtpClient` supports 25/587 with STARTTLS; it does not support implicit TLS (465).
- **FromAddress / FromName** — the sender. Some relays only deliver from an approved/provisioned mailbox even when they accept anonymous relay; use an address your relay delivers for.
- **Credentials** — never committed. Provide `Smtp__Username` / `Smtp__Password` as environment variables (wired through Aspire parameters in `AppHost.cs`). Credentials are applied only when **both** are non-empty; an anonymous/IP-allowlisted relay needs neither.

The committed template default uses a neutral placeholder `FromAddress` and an empty/placeholder host so
no environment-specific value ships in the template. See `docs/guides/getting-started.md` for the
setup walkthrough.

## Key Design Decisions

- **All templates in the database** — uniform storage and rendering; `EmailTemplateCategory` controls editability rather than where a template lives.
- **Edit-only business templates** — the template set is fixed by `EmailType` + seed data; admins customize content but cannot add/remove types, keeping the code and the template set in sync.
- **One service, two contracts** — implementing `IEmailSender<ApplicationUser>` lets Identity reuse the same templated, SMTP-backed sender instead of the framework no-op.
- **Best-effort `TrySendEmailAsync`** — notification emails never block or fail the primary operation.
- **No-op when unconfigured** — the template is runnable without SMTP; configuring `Smtp:Host` switches on real delivery.
- **Upsert seeding** — templates seed only if absent, preserving admin edits across redeployments.