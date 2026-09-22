# Bugfix Requirements Document

## Introduction

The `AspireWebAppTemplate.Scheduler` console app is a short-lived, Windows-Task-Scheduler-triggered batch runner. Its only registered job today is `AuditLogRetentionJob` (`purge-audit-logs`), which deletes audit-log entries older than the configured retention period.

The Scheduler currently boots the API/Web application's entire service graph and Identity stack even though its job needs almost none of it. `Program.cs` calls the API's catch-all `AddInfrastructureServices()` (registering UserService, RoleService, EmailService/`IEmailSender`, AiService — which constructs an `AmazonBedrockRuntimeClient` against AWS Bedrock, LdapAuthService, LdapLoginService, NotificationService, AuthService, AnnouncementService, EmailTemplateService, PagePermissionService, NavigationService, `HtmlSanitizer`, a `WebCallbackClient` typed HttpClient pointing at `https+http://webfrontend`, and the HTTP-backed `CurrentUserAccessor`). It then adds `AddDataProtection()` and full `AddIdentityCore<ApplicationUser>()...` registration solely to satisfy `AuditLogService`'s constructor dependency on `UserManager<ApplicationUser>`.

That constructor dependency is unused on the code path the Scheduler exercises: `PurgeOldEntriesAsync()` uses only `ApplicationDbContext` and `IConfiguration`. The `UserManager` is consumed only by `ResolveDisplayNameAsync`, which is called only from `LogAsync` (the audit-write path), which the purge job never triggers. The `SystemCurrentUserAccessor` registered in the Scheduler exists only defensively, because the imported full graph would otherwise register the HTTP-backed `CurrentUserAccessor` that fails without an `HttpContext`.

The result is architectural over-inheritance: the Scheduler loads Identity, Data Protection, an AWS Bedrock client, a Web-pointing HttpClient, and a full feature service graph that its job does not consume. This bloats startup, pulls in unnecessary/external dependencies (AWS, LDAP, Web callback target), and couples an optional batch host to infrastructure it does not need.

This bugfix makes the Scheduler's registered dependency set equal to exactly what its job(s) consume — while preserving the API/Web audit-write display-name behavior exactly, keeping Clean Architecture dependency direction intact, and keeping the build green with all existing tests passing.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN the Scheduler process starts THEN it registers the API's full service graph via `AddInfrastructureServices()`, including UserService, RoleService, EmailService/`IEmailSender`, AiService, LdapAuthService, LdapLoginService, NotificationService, AuthService, AnnouncementService, EmailTemplateService, PagePermissionService, and NavigationService — none of which its registered job consumes

1.2 WHEN the Scheduler process starts THEN it constructs an `AmazonBedrockRuntimeClient` (AWS Bedrock) as part of AI-service registration, even though no job invokes AI

1.3 WHEN the Scheduler process starts THEN it registers a `WebCallbackClient` typed HttpClient targeting `https+http://webfrontend` (a Web-host dependency) that no job uses

1.4 WHEN the Scheduler process starts THEN it registers the `HtmlSanitizer` (Ganss.Xss) singleton that no job uses

1.5 WHEN the Scheduler process starts THEN it registers ASP.NET Core Identity (`AddIdentityCore<ApplicationUser>().AddRoles<ApplicationRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders()`) and `AddDataProtection()` solely to satisfy `AuditLogService`'s `UserManager<ApplicationUser>` constructor dependency, which the purge path never uses

1.6 WHEN the Scheduler purge job executes THEN `AuditLogService` requires a `UserManager<ApplicationUser>` to be constructed even though `PurgeOldEntriesAsync()` uses only `ApplicationDbContext` and `IConfiguration`

1.7 WHEN the Scheduler process starts THEN it must register `SystemCurrentUserAccessor` defensively to shadow the HTTP-backed `CurrentUserAccessor` pulled in by the full graph, despite the purge job never reading `ICurrentUserAccessor`

### Expected Behavior (Correct)

2.1 WHEN the Scheduler process starts THEN it SHALL register only the services its registered job(s) consume — `ApplicationDbContext`, `IConfiguration`, and `IAuditLogService` (plus its actual transitive dependencies) — and SHALL NOT register UserService, RoleService, EmailService, NotificationService, AuthService, AnnouncementService, EmailTemplateService, PagePermissionService, NavigationService, LdapAuthService, or LdapLoginService

2.2 WHEN the Scheduler process starts THEN it SHALL NOT construct an `AmazonBedrockRuntimeClient` or register any AI service

2.3 WHEN the Scheduler process starts THEN it SHALL NOT register a `WebCallbackClient` or any HttpClient targeting the Web host

2.4 WHEN the Scheduler process starts THEN it SHALL NOT register the `HtmlSanitizer`

2.5 WHEN the Scheduler process starts THEN it SHALL NOT register ASP.NET Core Identity or Data Protection, because no registered dependency requires them

2.6 WHEN the Scheduler purge job executes THEN `AuditLogService` SHALL be constructable and run `PurgeOldEntriesAsync()` without any `UserManager<ApplicationUser>` / Identity dependency, via decoupling the display-name resolution behind an abstraction whose Identity-backed implementation is not required on the purge path

2.7 WHEN the Scheduler process starts THEN it SHALL NOT register `SystemCurrentUserAccessor`, and that class SHALL be removed, with guidance documented for reintroducing a system principal only when a future job performs an auditable write

2.8 WHEN the Scheduler registers its focused dependency set THEN it SHALL do so through an Infrastructure-owned registration seam (e.g., an `AddSchedulerInfrastructure()` extension added alongside `AddInfrastructureServices()`) so Infrastructure continues to own its DI composition, without weakening or forking `AddInfrastructureServices()`

2.9 WHEN the Scheduler `Program.cs` is reorganized THEN it SHALL expose an explicit `Main` entry point showing the flow (build/configure → resolve job → execute → return exit code), with host/DI composition separated into a dedicated class and job-dispatch/listing helpers separated out

### Unchanged Behavior (Regression Prevention)

3.1 WHEN the API or Web host writes an audit entry via `AuditLogService.LogAsync` THEN the system SHALL CONTINUE TO resolve the user display name exactly as today — the existing `DisplayName` for a known user, the `userId` string for an unknown user, and an empty string when `userId` is null

3.2 WHEN the API or Web host registers services via `AddInfrastructureServices()` THEN the system SHALL CONTINUE TO register the full service graph (users, roles, email, notifications, AI, LDAP, announcements, page permissions, navigation, sanitizer, Web callback client, and the HTTP-backed `CurrentUserAccessor`) unchanged

3.3 WHEN the Scheduler runs `Scheduler.exe purge-audit-logs` THEN the system SHALL CONTINUE TO delete audit-log entries older than the `AuditLog:RetentionDays` cutoff and return the success exit code with the purged count reported

3.4 WHEN the Scheduler is invoked with no job name or an unknown job name THEN the system SHALL CONTINUE TO print the available-jobs listing and return the invalid-usage exit code

3.5 WHEN the Scheduler job is cancelled (Ctrl+C / SIGTERM) or throws THEN the system SHALL CONTINUE TO return the cancelled and job-failed exit codes respectively

3.6 WHEN the solution is built and the existing test suite is run THEN the system SHALL CONTINUE TO build with 0 errors and pass all 239 existing tests, including the audit-log unit and property tests

3.7 WHEN the Scheduler references other projects THEN it SHALL CONTINUE TO depend only on Application, Infrastructure, and ServiceDefaults — never on the Web project — preserving the Clean Architecture dependency direction

3.8 WHEN the Scheduler is removed from the solution THEN the Web and API hosts SHALL CONTINUE TO build and run unaffected, keeping the Scheduler an optional component

## Bug Condition

**Bug Condition Function** — identifies the defective state:

```pascal
FUNCTION isBugCondition(X)
  INPUT: X of type SchedulerServiceRegistration
  OUTPUT: boolean

  // True when the Scheduler registers/loads any service or infrastructure
  // that its registered job(s) do not transitively consume.
  RETURN X.registeredServices ⊋ servicesConsumedBy(X.registeredJobs)
END FUNCTION
```

Concretely, `isBugCondition` is true today because the registered set includes Identity, Data Protection, AiService (`AmazonBedrockRuntimeClient`), `WebCallbackClient`, `HtmlSanitizer`, LDAP/email/notification/user/role/announcement/page-permission/navigation services, and `SystemCurrentUserAccessor` — none consumed by `purge-audit-logs`.

**Property: Fix Checking** — after the fix, the registered set equals exactly what the jobs consume:

```pascal
FOR ALL X WHERE isBugCondition(X) DO
  registered ← resolveRegisteredServices(F'(X))
  consumed   ← servicesConsumedBy(X.registeredJobs)
  ASSERT registered = consumed
  ASSERT NOT registers(F'(X), Identity)
  ASSERT NOT registers(F'(X), DataProtection)
  ASSERT NOT registers(F'(X), AmazonBedrockRuntimeClient)
  ASSERT NOT registers(F'(X), WebCallbackClient)
  ASSERT NOT registers(F'(X), HtmlSanitizer)
  ASSERT purgeJobRuns(F'(X)) = success
END FOR
```

**Property: Preservation Checking** — for the API/Web hosts (not bug-condition inputs), behavior is identical:

```pascal
FOR ALL X WHERE NOT isBugCondition(X) DO
  ASSERT auditWriteDisplayNameResolution(F(X)) = auditWriteDisplayNameResolution(F'(X))
  ASSERT infrastructureServiceGraph(F(X))       = infrastructureServiceGraph(F'(X))
  ASSERT F(X) = F'(X)
END FOR
```

- **F**: the Scheduler/AuditLog wiring before the fix (imports the full graph + Identity).
- **F'**: the Scheduler/AuditLog wiring after the fix (focused registration seam + display-name resolution decoupled behind an abstraction).
- **Counterexample demonstrating the bug**: launching `Scheduler.exe purge-audit-logs` today instantiates an `AmazonBedrockRuntimeClient` and the full Identity stack before deleting a single audit row.
