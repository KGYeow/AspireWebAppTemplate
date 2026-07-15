# Implementation Plan: Email SMTP Integration

## Overview

This plan implements an all-in-database email template system with SMTP sending. All templates (system security and business notification) are stored in the database, seeded with production-ready content on first deployment. The `EmailTemplateCategory` determines editability — system templates are read-only, business templates are admin-editable. A unified `EmailType` enum covers all template types. The implementation follows the existing thin controller / full service layer pattern, Aspire parameter-based secrets, and FsCheck property testing conventions.

## Tasks

- [x] 1. Create shared domain enums and DTO contracts
  - [x] 1.1 Create EmailTemplateCategory and EmailType enums in Core/Domain/Enums/
    - Create `EmailTemplateCategory.cs` with `System` and `Business` values
    - Create `EmailType.cs` with system values (PasswordReset, EmailConfirmation, TwoFactorCode, AccountLockout, EmailChanged, PasswordChanged) and business values (WelcomeEmail, AccountDeactivated, CustomNotification)
    - Include full XML documentation on each enum and value
    - _Requirements: 3.6, 4.1, 4.9, 4.10, 8.5_

  - [x] 1.2 Create email DTO contracts in Core/Contracts/Email/
    - Create `EmailTemplateDto.cs` (sealed class, all properties with XML docs)
    - Create `UpdateEmailTemplateRequest.cs` (sealed class, Required/MaxLength validation attributes)
    - Create `SendTestEmailRequest.cs` (sealed class, Required + EmailAddress validation)
    - Create `PreviewTemplateRequest.cs` (sealed class, Dictionary<string, string> SampleData)
    - Create `RenderedEmailResult.cs` (sealed class, Subject + HtmlBody)
    - Follow existing Core DTO conventions: data annotations, empty string defaults
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6, 11.7_

- [x] 2. Create EmailTemplate entity and EF Core configuration
  - [x] 2.1 Create EmailTemplate entity in ApiService/Data/Entities/
    - Define all properties: Id, EmailType, DisplayName, Subject, HtmlBody, Category, IsActive, PlaceholderHints, CreatedAtUtc, UpdatedAtUtc
    - Include full XML documentation
    - _Requirements: 4.1_

  - [x] 2.2 Create EmailTemplateConfiguration in ApiService/Data/Configurations/
    - Configure table name, key, column constraints (MaxLength on DisplayName, Subject, PlaceholderHints)
    - Store enums as strings via HasConversion<string>()
    - Add unique index on EmailType (IX_EmailTemplates_EmailType)
    - Add index on Category (IX_EmailTemplates_Category)
    - _Requirements: 4.1, 4.9_

  - [x] 2.3 Register EmailTemplate DbSet in ApplicationDbContext and create EF migration
    - Add `DbSet<EmailTemplate> EmailTemplates` to ApplicationDbContext
    - Generate EF Core migration for the new table
    - _Requirements: 4.1_

- [x] 3. Implement SMTP configuration and Aspire parameters
  - [x] 3.1 Add SMTP configuration section to appsettings.json
    - Add `Smtp` section with Host, Port, EnableSsl, FromAddress, FromName defaults
    - _Requirements: 2.1, 2.6_

  - [x] 3.2 Add Aspire secret parameters in AppHost Program.cs
    - Define `smtp-username` and `smtp-password` parameters with `secret: true`
    - Pass to ApiService as `Smtp__Username` and `Smtp__Password` environment variables
    - _Requirements: 2.2_

- [x] 4. Implement service interfaces
  - [x] 4.1 Create IEmailService interface in ApiService/Abstractions/
    - Define `SendEmailAsync(EmailType, string, Dictionary<string, string>)` and `SendTestEmailAsync(string)` methods
    - Include full XML documentation with exception docs
    - Use `#region` grouping (Email Operations, Test Operations)
    - _Requirements: 9.1, 9.4_

  - [x] 4.2 Create IEmailTemplateService interface in ApiService/Abstractions/
    - Define `RenderAsync(EmailType, Dictionary<string, string>)`, `RenderPreviewAsync`, `GetAllAsync`, `GetByIdAsync`, `UpdateAsync` methods
    - Include full XML documentation with exception docs
    - Use `#region` grouping (Template Rendering, Query Operations, Edit Operations)
    - _Requirements: 9.2_

- [x] 5. Checkpoint — Verify schema and interfaces compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Implement EmailTemplateService
  - [x] 6.1 Implement EmailTemplateService in ApiService/Services/
    - Implement `RenderAsync` — query DB by EmailType, replace `{{placeholder}}` tokens in subject and body
    - Implement `RenderPreviewAsync` — render any template with sample data
    - Implement `GetAllAsync`, `GetByIdAsync` — query database and map to DTOs
    - Implement `UpdateAsync` — reject System category, update Business template fields
    - Use traditional constructor, `#region` blocks, full XML docs
    - _Requirements: 3.1, 3.3, 3.7, 4.2, 4.3, 4.4, 4.6, 5.3, 5.6, 8.1, 8.2, 8.5_

  - [x] 6.2 Write property test: Template placeholder replacement (Property 3)
    - **Property 3: Template placeholder replacement produces correct output**
    - **Validates: Requirements 3.3, 4.3, 4.4, 8.2**
    - File: `Tests/Email/TemplatePlaceholderPropertyTests.cs`
    - Use SQLite in-memory DB, seed active template, verify all `{{Key}}` replaced

  - [x] 6.3 Write property test: Inactive or missing template rejected (Property 4)
    - **Property 4: Inactive or missing template is rejected**
    - **Validates: Requirements 4.6, 3.7**
    - File: `Tests/Email/TemplateResolutionPropertyTests.cs`
    - Verify `KeyNotFoundException` thrown for inactive/missing templates

  - [x] 6.4 Write property test: All templates resolve from database uniformly (Property 5)
    - **Property 5: All templates resolve from the database uniformly**
    - **Validates: Requirements 8.1, 8.2, 8.5**
    - File: `Tests/Email/TemplateResolutionPropertyTests.cs`
    - Verify both system and business EmailTypes resolve from same DB query path

  - [x] 6.5 Write property test: System templates cannot be updated (Property 6)
    - **Property 6: System templates cannot be updated**
    - **Validates: Requirements 5.3, 5.6**
    - File: `Tests/Email/SystemTemplateProtectionPropertyTests.cs`
    - Verify `InvalidOperationException` thrown, record unchanged

- [x] 7. Implement EmailService (SMTP sending)
  - [x] 7.1 Implement EmailService in ApiService/Services/
    - Implement `IEmailService` and `IEmailSender<ApplicationUser>`
    - Configure SmtpClient from Smtp config section + Aspire env vars
    - Implement no-op fallback when config missing/empty (log warning at startup)
    - Implement `SendEmailAsync` — delegate to template service for rendering, compose and send
    - Implement `SendTestEmailAsync` — send hardcoded test template
    - Implement `IEmailSender<ApplicationUser>` methods — map Identity callbacks to `SendEmailAsync` with appropriate EmailType
    - Log email sends at Information level with masked recipient
    - Handle SMTP errors (connection, auth, delivery) with appropriate exceptions
    - Use traditional constructor, `#region` blocks, full XML docs
    - _Requirements: 1.1–1.10, 2.3, 2.4, 2.5, 8.3, 8.4_

  - [x] 7.2 Write property test: Email message composition (Property 1)
    - **Property 1: Email message composition includes all required fields from configuration**
    - **Validates: Requirements 1.5, 1.6, 1.9, 8.3**
    - File: `Tests/Email/EmailCompositionPropertyTests.cs`
    - Mock SmtpClient, verify MailMessage fields match config + rendered result

  - [x] 7.3 Write property test: SMTP credentials applied conditionally (Property 2)
    - **Property 2: SMTP credentials are applied if and only if both username and password are present**
    - **Validates: Requirements 2.4, 2.5**
    - File: `Tests/Email/SmtpCredentialPropertyTests.cs`
    - Verify credentials applied when both present, not applied when either missing

  - [x] 7.4 Write property test: Email recipient address masking (Property 7)
    - **Property 7: Email recipient address is masked in log entries**
    - **Validates: Requirements 8.4**
    - File: `Tests/Email/EmailLoggingPropertyTests.cs`
    - Verify first 3 chars + `***@domain` format in log output

- [x] 8. Register services and replace NoOpEmailSender
  - [x] 8.1 Register services in ApiService DI and replace NoOpEmailSender
    - Register `IEmailService` / `EmailService` as scoped in `ApplicationServiceExtensions.AddApplicationServices()`
    - Register `IEmailTemplateService` / `EmailTemplateService` as scoped
    - Register `EmailService` as `IEmailSender<ApplicationUser>` (replacing NoOpEmailSender)
    - Remove existing NoOpEmailSender registration
    - _Requirements: 1.2, 9.3, 9.4_

- [x] 9. Implement database seeding for all templates
  - [x] 9.1 Add email template seed data in SeedData.cs
    - Seed ALL 9 templates (6 system + 3 business) with full production-ready Subject and HtmlBody content
    - System templates: PasswordReset, EmailConfirmation, TwoFactorCode, AccountLockout, EmailChanged, PasswordChanged — Category = System, IsActive = true
    - Business templates: WelcomeEmail (active), AccountDeactivated (inactive), CustomNotification (active) — Category = Business
    - Include appropriate PlaceholderHints for each template
    - Use upsert pattern: seed only if EmailType not already present
    - _Requirements: 3.4, 4.5, 4.8, 10.1, 10.2, 10.3, 10.4_

  - [x] 9.2 Write property test: Database seeding preserves existing templates (Property 8)
    - **Property 8: Database seeding preserves existing templates (upsert by EmailType)**
    - **Validates: Requirements 10.4**
    - File: `Tests/Email/TemplateSeedingPropertyTests.cs`
    - Verify existing records not modified, missing records inserted

- [x] 10. Checkpoint — Verify services, seeding, and property tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Implement EmailTemplateController (thin controller)
  - [x] 11.1 Create EmailTemplateController in ApiService/Controllers/
    - Implement GET `/api/email-templates` — list all templates
    - Implement GET `/api/email-templates/{id}` — get single template
    - Implement PUT `/api/email-templates/{id}` — update business template (edit-only)
    - Implement POST `/api/email-templates/{id}/preview` — render with sample data
    - Implement POST `/api/email-templates/test` — send test email
    - Use exception-to-status mapping pattern (KeyNotFoundException→404, InvalidOperationException→400)
    - Add `[Authorize]` attribute, extend BaseController
    - NO POST create or DELETE endpoints
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 7.1_

  - [x] 11.2 Write unit tests for EmailTemplateController
    - Test HTTP status code mapping (200, 400, 404) for each endpoint
    - Test delegation to mocked services
    - Verify no create/delete endpoints exist
    - _Requirements: 5.1–5.7_

- [x] 12. Implement ApiEmailTemplateService (typed HttpClient)
  - [x] 12.1 Create ApiEmailTemplateService in Web/Services/ApiClients/
    - Implement `GetAllAsync`, `GetByIdAsync`, `UpdateAsync`, `PreviewAsync`, `SendTestEmailAsync`
    - Return `ApiResult<T>` pattern (never throw on HTTP errors)
    - Use traditional constructor, `#region` blocks, full XML docs
    - _Requirements: 9.5_

  - [x] 12.2 Register ApiEmailTemplateService in ApiClientServiceExtensions
    - Register in `AddApiClients()` with Aspire service discovery base address
    - Add `UserIdentityDelegatingHandler` for auth propagation
    - _Requirements: 9.5_

- [x] 13. Implement Admin Template Management Page
  - [x] 13.1 Create EmailTemplates admin page in Web/Components/Pages/Admin/EmailTemplates/
    - Create `EmailTemplates.razor` and `EmailTemplates.razor.cs` (code-behind)
    - Use `MudDataGrid<EmailTemplateDto>` with `Items` binding (small dataset)
    - Display columns: DisplayName, EmailType, Category badge, Subject, IsActive, LastUpdated
    - Show system templates as read-only with lock icon
    - Show business templates with edit action
    - Add page permission authorization
    - _Requirements: 6.1, 6.2, 6.4, 6.8_

  - [x] 13.2 Implement edit dialog for business templates
    - Create edit dialog/form with: DisplayName, Subject, HtmlBody (rich text), PlaceholderHints, IsActive toggle
    - Display available placeholder hints for guidance
    - Add validation: reject empty Subject or HtmlBody
    - Save via ApiEmailTemplateService.UpdateAsync
    - Show success/error Snackbar notifications
    - _Requirements: 6.3, 6.6, 6.7_

  - [x] 13.3 Implement preview dialog and test email action
    - Add "Preview" action that renders template with sample data in a dialog
    - Add "Send Test Email" button that prompts for recipient address
    - Show success Snackbar on test email success
    - Show error Snackbar with failure description on test email failure
    - _Requirements: 6.5, 7.2, 7.3, 7.4, 7.5_

- [x] 14. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The design uses C# (.NET 10) — no language selection was needed
- All templates (system + business) stored in database with same structure and rendering
- All services registered as scoped to align with per-request DbContext lifetime
- No Razor template files — all templates use `{{placeholder}}` string replacement from DB

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "3.1", "3.2", "4.1", "4.2"] },
    { "id": 2, "tasks": ["2.2"] },
    { "id": 3, "tasks": ["2.3"] },
    { "id": 4, "tasks": ["6.1"] },
    { "id": 5, "tasks": ["6.2", "6.3", "6.4", "6.5", "7.1"] },
    { "id": 6, "tasks": ["7.2", "7.3", "7.4", "8.1"] },
    { "id": 7, "tasks": ["9.1"] },
    { "id": 8, "tasks": ["9.2", "11.1"] },
    { "id": 9, "tasks": ["11.2", "12.1"] },
    { "id": 10, "tasks": ["12.2"] },
    { "id": 11, "tasks": ["13.1"] },
    { "id": 12, "tasks": ["13.2", "13.3"] }
  ]
}
```
