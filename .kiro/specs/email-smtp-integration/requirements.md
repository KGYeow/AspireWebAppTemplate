# Requirements Document

## Introduction

This feature replaces the existing `NoOpEmailSender` with a real SMTP email sending implementation. All email templates (both system security and business notification) are stored in the database, seeded with production-ready default content on first deployment. System templates are read-only at runtime — administrators cannot modify them. Business notification templates are admin-editable with one template per predefined `EmailType`. The set of email types is defined by a code-level enum; adding new types requires code changes. SMTP connection settings follow the existing Aspire pattern: non-secrets (host, port, from address) in `appsettings.json`, secrets (username, password) via Aspire parameters.

## Glossary

- **Email_Service**: The backend service implementation (`EmailService`) that sends emails via SMTP using resolved templates.
- **Template_Service**: The backend service (`EmailTemplateService`) that resolves and renders email templates from the database.
- **SMTP_Client**: The configured .NET `SmtpClient` instance used to send email messages to the SMTP relay.
- **System_Template**: A database-stored email template for security-critical emails (password reset, email confirmation, etc.) that is read-only at runtime. Seeded with production-ready default content on first deployment.
- **Business_Template**: A database-stored email template for a predefined EmailType that admins can edit at runtime via the admin UI. Each business EmailType has exactly one template.
- **Template_Category**: An enum distinguishing between system security templates and business notification templates. Determines editability — system templates are read-only, business templates are admin-editable.
- **EmailType**: A code-defined enum representing all predefined email types — both system (PasswordReset, EmailConfirmation, TwoFactorCode, AccountLockout, EmailChanged, PasswordChanged) and business (WelcomeEmail, AccountDeactivated, CustomNotification). New types require code changes — they cannot be created at runtime.
- **Email_Template_Entity**: The EF Core entity representing an email template stored in the database. One row per EmailType.
- **Template_Variable**: A named placeholder (e.g., `{{UserName}}`, `{{ResetLink}}`) within a template body that is replaced with actual values at send time.
- **Email_Controller**: The thin REST API controller (`EmailTemplateController`) that exposes endpoints for editing business templates and read-only access to system templates.
- **Api_Email_Client**: The typed HttpClient service (`ApiEmailTemplateService`) in the Web project for the admin UI to manage templates.
- **Admin_Template_Page**: The Blazor Server admin page (`/admin/email-templates`) for editing business notification templates and viewing system templates as read-only.

## Requirements

### Requirement 1: SMTP Email Sending

**User Story:** As a system administrator, I want the application to send real emails via SMTP, so that users receive password reset links, email confirmations, and other critical communications.

#### Acceptance Criteria

1. WHEN a password reset, email confirmation, or password reset code email is triggered by ASP.NET Core Identity, THE Email_Service SHALL send the email via the configured SMTP_Client to the recipient's email address.
2. THE Email_Service SHALL implement `IEmailSender<ApplicationUser>` and replace the existing `NoOpEmailSender` registration in the DI container.
3. THE Email_Service SHALL connect to the SMTP server using the host, port, and SSL settings from the `Smtp` configuration section in `appsettings.json`.
4. THE Email_Service SHALL authenticate with the SMTP server using the username and password provided via Aspire secret parameters.
5. THE Email_Service SHALL use the sender address from the `Smtp:FromAddress` configuration value as the "From" field on all outgoing emails.
6. THE Email_Service SHALL use the sender display name from the `Smtp:FromName` configuration value as the display name on the "From" field.
7. IF the SMTP_Client fails to connect or authenticate, THEN THE Email_Service SHALL log the error at Error level and throw an InvalidOperationException with a message indicating the SMTP connection failed.
8. IF the SMTP_Client fails to deliver a message (rejected recipient, relay denied), THEN THE Email_Service SHALL log the error at Error level and throw an InvalidOperationException with a message indicating the email delivery failed.
9. THE Email_Service SHALL support sending emails with HTML body content.
10. WHILE the SMTP configuration section is missing or the host value is empty, THE Email_Service SHALL log a warning at startup and fall back to no-op behavior (log email details at Information level without sending).

### Requirement 2: SMTP Configuration via Aspire Parameters

**User Story:** As a developer, I want SMTP connection settings to follow the same pattern as the AWS AI integration (non-secrets in appsettings.json, secrets via Aspire parameters), so that configuration is consistent and secrets are never committed to source control.

#### Acceptance Criteria

1. THE application SHALL read SMTP host, port, from address, from name, and EnableSsl flag from the `Smtp` section in `appsettings.json`. These are non-secret configuration values.
2. THE AppHost project SHALL define two secret parameters (`smtp-username` and `smtp-password`) and pass them to the ApiService project as environment variables (`Smtp__Username` and `Smtp__Password`).
3. THE Email_Service SHALL read SMTP credentials from the `Smtp:Username` and `Smtp:Password` configuration values (populated via Aspire environment variables at runtime).
4. WHEN `Smtp:Username` and `Smtp:Password` configuration values are both present and non-empty, THE Email_Service SHALL use them as SMTP authentication credentials.
5. WHEN `Smtp:Username` or `Smtp:Password` configuration values are absent or empty, THE Email_Service SHALL connect to the SMTP server without authentication (supporting relay-only configurations).
6. THE `appsettings.json` SHALL include a default `Smtp` section with placeholder values: host `localhost`, port `587`, EnableSsl `true`, FromAddress `noreply@example.com`, and FromName `AspireWebApp`.

### Requirement 3: System Security Templates

**User Story:** As a developer, I want security-critical email templates (password reset, email confirmation, 2FA codes, account lockout) stored in the database and seeded with production-ready content, so that the email feature works immediately after deployment without manual template creation — while preventing accidental admin modifications to security-critical flows.

#### Acceptance Criteria

1. THE Template_Service SHALL load system security templates from the database by EmailType.
2. THE Template_Service SHALL support the following system security templates: PasswordReset, EmailConfirmation, TwoFactorCode, AccountLockout, EmailChanged, and PasswordChanged.
3. WHEN a system security email is triggered, THE Template_Service SHALL query the database for the template matching that EmailType, render it with the provided Template_Variable values using `{{placeholder}}` string replacement, and return the rendered subject and HTML body.
4. THE system security templates SHALL be seeded with production-ready default content that works immediately after deployment without customization.
5. THE system security templates SHALL support the following Template_Variable placeholders at minimum: `UserName`, `ResetLink` (password reset), `ConfirmationLink` (email confirmation), `TwoFactorCode` (2FA), `LockoutEnd` (account lockout), `NewEmail` and `ConfirmationLink` (email changed), and `UserName` (password changed — informational only, no action link).
6. THE system security templates SHALL have Template_Category set to System, making them read-only at runtime — administrators cannot edit them via the UI or API.
7. IF a system security template for the requested EmailType does not exist in the database, THEN THE Template_Service SHALL throw a KeyNotFoundException indicating the template was not found.

### Requirement 4: Business Notification Templates (Edit-Only, Database-Stored)

**User Story:** As an administrator, I want to edit business notification email templates (welcome email, account deactivated, custom notifications) at runtime, so that I can customize email content without requiring developer involvement or code deployment.

#### Acceptance Criteria

1. THE Email_Template_Entity SHALL be stored in the application database with the following properties: Id (Guid), EmailType (EmailType enum), DisplayName (string), Subject (string), HtmlBody (string), Category (Template_Category), IsActive (bool), PlaceholderHints (string), CreatedAtUtc (DateTime), UpdatedAtUtc (DateTime nullable).
2. THE Template_Service SHALL load business notification templates from the database by EmailType.
3. WHEN a business notification email is triggered with an EmailType, THE Template_Service SHALL query the database for the active template matching that type, render it with provided Template_Variable values, and return the rendered subject and HTML body.
4. THE Template_Service SHALL replace Template_Variable placeholders in both the subject and body of all templates using a simple string replacement approach with `{{VariableName}}` syntax.
5. THE application SHALL seed one template per business EmailType on startup: WelcomeEmail, AccountDeactivated, and CustomNotification. Each seeded template SHALL include sensible default subject and body content.
6. IF a business notification template for the requested EmailType does not exist or is inactive, THEN THE Template_Service SHALL throw a KeyNotFoundException indicating the template was not found or is disabled.
7. THE Email_Template_Entity SHALL include a PlaceholderHints property (comma-separated string) that lists the available Template_Variable names for that template, displayed in the admin UI as guidance.
8. THE WelcomeEmail and CustomNotification seeded templates SHALL have `IsActive` set to `true` by default. THE AccountDeactivated seeded template SHALL have `IsActive` set to `false` by default, allowing administrators to enable it per deployment needs.
9. THE database SHALL enforce a unique constraint on EmailType to ensure exactly one template per EmailType.
10. THE set of email types is fixed by the EmailType enum. Administrators CANNOT create new templates or delete existing ones — they can only edit the content of seeded business templates.

### Requirement 5: Template Management API

**User Story:** As a developer, I want REST API endpoints for managing email templates, so that the admin UI can edit business templates and preview rendered content.

#### Acceptance Criteria

1. THE Email_Controller SHALL expose a GET endpoint at `api/email-templates` that returns all Email_Template_Entity records (both system and business templates).
2. THE Email_Controller SHALL expose a GET endpoint at `api/email-templates/{id}` that returns a single Email_Template_Entity by its Id.
3. THE Email_Controller SHALL expose a PUT endpoint at `api/email-templates/{id}` that updates an existing business notification template's DisplayName, Subject, HtmlBody, PlaceholderHints, and IsActive fields. THE endpoint SHALL reject updates to templates with Template_Category set to System.
4. THE Email_Controller SHALL expose a POST endpoint at `api/email-templates/{id}/preview` that renders a template with sample data and returns the rendered HTML for admin preview.
5. THE Email_Controller SHALL require authorization for all endpoints.
6. IF a template update operation targets a system security template, THEN THE Email_Controller SHALL return a 400 Bad Request with a message indicating system templates cannot be modified via the API.
7. THE Email_Controller SHALL NOT expose POST (create) or DELETE endpoints for templates. The template set is fixed by seed data.

### Requirement 6: Template Management Admin UI

**User Story:** As an administrator, I want an admin page to view and edit email templates, so that I can customize business notification email content, preview rendered emails, and see which system security templates exist (read-only).

#### Acceptance Criteria

1. THE Admin_Template_Page SHALL be accessible at route `/admin/email-templates` and require page permission authorization.
2. THE Admin_Template_Page SHALL display a data grid listing all email templates with columns: DisplayName, EmailType, Category (system/business badge), Subject, IsActive status, and LastUpdated date.
3. THE Admin_Template_Page SHALL allow administrators to edit existing business notification templates via a dialog or form with fields for DisplayName, Subject, HtmlBody (rich text editor), PlaceholderHints, and IsActive toggle.
4. THE Admin_Template_Page SHALL display system security templates as read-only rows (no edit actions) with a visual indicator (e.g., lock icon or "System" badge) distinguishing them from business templates.
5. THE Admin_Template_Page SHALL provide a "Preview" action for any template that renders the template with sample placeholder values and displays the result in a dialog.
6. THE Admin_Template_Page SHALL display the available placeholder hints for each template so administrators know which variables can be used.
7. IF an administrator attempts to save a business template with an empty Subject or HtmlBody, THEN THE Admin_Template_Page SHALL display a validation error preventing the save.
8. THE Admin_Template_Page SHALL NOT provide create or delete actions for templates. The template set is fixed by the EmailType enum and seed data.

### Requirement 7: Send Test Email

**User Story:** As an administrator, I want to send a test email from the admin page, so that I can verify SMTP configuration is working correctly.

#### Acceptance Criteria

1. THE Email_Controller SHALL expose a POST endpoint at `api/email-templates/test` that sends a test email to a specified recipient address using a simple test template.
2. WHEN an administrator triggers the test email action, THE Email_Service SHALL send a test email containing the application name, current timestamp, and a confirmation message to the specified recipient address.
3. THE Admin_Template_Page SHALL include a "Send Test Email" action (button or menu item) that prompts for a recipient email address and triggers the test email endpoint.
4. IF the test email is sent successfully, THEN THE Admin_Template_Page SHALL display a success notification via Snackbar.
5. IF the test email fails (SMTP connection error, authentication failure, delivery failure), THEN THE Admin_Template_Page SHALL display an error notification via Snackbar with a description of the failure.

### Requirement 8: Template Resolution and Email Composition

**User Story:** As a developer, I want the email service to resolve all templates from the database uniformly, so that both system security emails and business notification emails use the same rendering pipeline.

#### Acceptance Criteria

1. WHEN the Email_Service is called to send any email (system or business), THE Template_Service SHALL resolve the template from the database by EmailType.
2. THE Template_Service SHALL render all templates using the same `{{placeholder}}` string replacement approach on both subject and body.
3. THE Email_Service SHALL compose a complete email message with: resolved subject, rendered HTML body, sender address (from configuration), and recipient address.
4. THE Email_Service SHALL log each email send attempt at Information level with: EmailType identifier, recipient address (masked to first 3 characters + domain), and whether the send succeeded.
5. THE Template_Category SHALL determine editability only — System templates are read-only, Business templates are admin-editable. Both categories use the same database storage and rendering approach.

### Requirement 9: Service Layer and DI Registration

**User Story:** As a developer, I want the email and template services to follow the existing interface-driven DI pattern, so that the integration is testable and consistent with the rest of the codebase.

#### Acceptance Criteria

1. THE Email_Service SHALL implement an `IEmailService` interface located in `ApiService/Abstractions/` with a unified method for sending emails: `SendEmailAsync(EmailType emailType, string recipientEmail, Dictionary<string, string> variables)` and `SendTestEmailAsync(string recipientEmail)`.
2. THE Template_Service SHALL implement an `IEmailTemplateService` interface located in `ApiService/Abstractions/` with methods: `RenderAsync(EmailType emailType, Dictionary<string, string> variables)`, `RenderPreviewAsync(Guid templateId, Dictionary<string, string> sampleData)`, `GetAllAsync()`, `GetByIdAsync(Guid id)`, and `UpdateAsync(Guid id, UpdateEmailTemplateRequest request)`.
3. THE Email_Service and Template_Service SHALL be registered as scoped services via the existing `ApplicationServiceExtensions.AddApplicationServices()` method in the API project.
4. THE Email_Service SHALL also implement `IEmailSender<ApplicationUser>` to satisfy ASP.NET Core Identity's email sending contract (replacing `NoOpEmailSender`).
5. THE Api_Email_Client SHALL be located in `Web/Services/ApiClients/` and registered in `ApiClientServiceExtensions.AddApiClients()` with the Aspire service discovery base address and the `UserIdentityDelegatingHandler`.

### Requirement 10: Seeded Default Templates

**User Story:** As a developer deploying the application for the first time, I want all templates to have sensible defaults out of the box, so that the email feature works immediately after setup without manual template creation.

#### Acceptance Criteria

1. THE application SHALL seed ALL templates (system and business) in the database via `SeedData` with production-ready default content: PasswordReset (includes `{{UserName}}` and `{{ResetLink}}`), EmailConfirmation (includes `{{UserName}}` and `{{ConfirmationLink}}`), TwoFactorCode (includes `{{UserName}}` and `{{TwoFactorCode}}`), AccountLockout (includes `{{UserName}}` and `{{LockoutEnd}}`), EmailChanged (includes `{{UserName}}`, `{{NewEmail}}`, and `{{ConfirmationLink}}`), PasswordChanged (includes `{{UserName}}`), WelcomeEmail (includes `{{UserName}}`), AccountDeactivated (includes `{{UserName}}`, `{{DeactivationReason}}`), and CustomNotification (includes `{{UserName}}`, `{{Subject}}`, `{{Body}}`).
2. THE system templates (PasswordReset, EmailConfirmation, TwoFactorCode, AccountLockout, EmailChanged, PasswordChanged) SHALL be seeded with Category set to System and IsActive set to `true`.
3. THE WelcomeEmail and CustomNotification seeded templates SHALL have `IsActive` set to `true` by default. THE AccountDeactivated seeded template SHALL have `IsActive` set to `false` by default.
4. THE database seeding SHALL use an upsert pattern — seed only if a template for that EmailType does not already exist, preserving any admin customizations on subsequent deployments.

### Requirement 11: DTO Contracts

**User Story:** As a developer, I want DTOs for the email template feature to follow the existing contract conventions, so that the integration is consistent and discoverable.

#### Acceptance Criteria

1. THE email template DTOs SHALL be located in `Core/Contracts/Email/`.
2. THE `EmailTemplateDto` SHALL contain: Id (Guid), EmailType (EmailType enum), DisplayName (string), Subject (string), HtmlBody (string), Category (Template_Category enum), IsActive (bool), PlaceholderHints (string), CreatedAtUtc (DateTime), UpdatedAtUtc (DateTime nullable).
3. THE `UpdateEmailTemplateRequest` SHALL contain: DisplayName (required, max 200 characters), Subject (required, max 500 characters), HtmlBody (required), PlaceholderHints (optional, max 1000 characters), IsActive (bool).
4. THE `SendTestEmailRequest` SHALL contain: RecipientEmail (required, valid email format).
5. THE `PreviewTemplateRequest` SHALL contain: SampleData (Dictionary<string, string>) for placeholder values to use during preview rendering.
6. ALL DTOs SHALL follow existing Core DTO conventions: sealed class, XML documentation on all public properties, data annotation validation attributes, and string properties initialized to empty string defaults.
7. THE feature SHALL NOT include a CreateEmailTemplateRequest DTO — template creation is handled exclusively by seed data.
