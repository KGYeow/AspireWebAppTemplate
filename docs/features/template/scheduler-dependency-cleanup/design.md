# Scheduler Dependency Cleanup Bugfix Design

## Overview

The `AspireWebAppTemplate.Scheduler` console app is a short-lived, Windows-Task-Scheduler-triggered
batch runner whose only registered job (`AuditLogRetentionJob` / `purge-audit-logs`) deletes audit-log
entries older than the configured retention period. Today the Scheduler boots the *entire* API/Web
service graph via `AddInfrastructureServices()` plus a full ASP.NET Core Identity + Data Protection
stack. It does this solely because `AuditLogService`'s constructor requires `UserManager<ApplicationUser>` —
a dependency the purge path never touches. The `UserManager` is consumed only by the private
`ResolveDisplayNameAsync`, which is called only from `LogAsync` (the audit-*write* path), which the
purge job never invokes.

This bug fix implements the user-approved **root-cause fix "Option 1 — extract the retention responsibility
into a feature-owned service"**. Rather than working around `AuditLogService`'s Identity coupling by
registering a Scheduler-side stand-in resolver purely to satisfy construction, this approach removes the
coupling **structurally**: the purge/retention responsibility is lifted out of `AuditLogService` into a
dedicated, feature-owned `IAuditLogRetentionService` whose only dependencies are `ApplicationDbContext` and
`IConfiguration`. The Scheduler then depends on that small service and **never constructs `AuditLogService`
at all**, so no display-name resolver is needed in the Scheduler and `AuditLogService`'s own internal
`UserManager` usage is left untouched.

The strategy is:

1. **Extract the purge/retention responsibility into a feature-owned service.** Move
   `PurgeOldEntriesAsync()` (and its `GetValidatedRetentionDays` helper) out of `AuditLogService` into a new
   `IAuditLogRetentionService` (interface at `Application/Features/AuditLog`, implementation at
   `Infrastructure/Services/AuditLog`) that depends only on `ApplicationDbContext` + `IConfiguration`. The
   Scheduler depends solely on this service, so it never touches `AuditLogService` and therefore never pulls
   in Identity. Splitting one feature into multiple focused services is consistent with the template (the
   Authentication feature already ships `IAuthService`/`ILoginService`/`IRegisterService`/`ILdapAuthService`/
   `ILdapLoginService`).
2. **Add an Infrastructure-owned Scheduler registration seam** (`AddSchedulerInfrastructure`) that
   registers *only* what the purge job transitively consumes — `ApplicationDbContext` and
   `IAuditLogRetentionService` — never `IAuditLogService`, never a resolver, and never Identity,
   Data Protection, AI/Bedrock, `WebCallbackClient`, `HtmlSanitizer`, or any other feature service.
3. **Remove `SystemCurrentUserAccessor`** (only ever needed defensively to shadow the HTTP-backed
   accessor pulled in by the full graph).
4. **Reorganize `Program.cs` around an explicit `Main`**, delegating host/DI composition and job
   dispatch/listing to dedicated helper types.
5. **Remove the now-unused `Microsoft.AspNetCore.DataProtection` package** from the Scheduler `.csproj`.

The fix is minimal and targeted: it relocates *where* the purge responsibility lives (into a small
feature-owned service) and changes *what* the Scheduler composes — while keeping the API/Web audit-write
display-name behavior (including `AuditLogService`'s existing `UserManager`-backed helper), the
`AddInfrastructureServices()` graph, the purge behavior, the exit-code mapping, and all 239 existing tests
unchanged.

### Design Notes — Future maintenance-task growth (approved convention)

This fix establishes the convention for how maintenance/retention work scales in this template:

- **Feature ownership ("what").** Each feature that needs retention/cleanup/archival owns its **own** small
  service — interface in `Features/{X}/`, implementation in `Infrastructure/Services/{X}/` — with only the
  dependencies that work requires. `IAuditLogRetentionService` is the first such service; a future
  `IUserDataCleanupService` (or similar) would follow the same shape. Feature logic stays with the feature.
- **Execution/orchestration ("when + glue").** The Scheduler's **existing** `IScheduledJob` abstraction is
  the maintenance-task seam. Each feature's maintenance work gets a thin `IScheduledJob` adapter in the
  Scheduler that delegates to the feature service (e.g., `AuditLogRetentionJob → IAuditLogRetentionService`).
  The registered `IEnumerable<IScheduledJob>` **is** the task registry; Windows Task Scheduler owns timing.
- **Explicitly rejected/deferred.** (a) A centralized `MaintenanceService`/`RetentionService` that knows
  every feature is **rejected** — it becomes a god-service coupled to all features. (b) A separate
  `IMaintenanceTask`/`IRetentionTask` abstraction is **deferred now** — it is redundant with `IScheduledJob`,
  which already exposes `Name`/`Description`/`RunAsync(ct) -> Task<int>`, is resolved as an `IEnumerable`, and
  is dispatched by name.
- **When to evolve.** Revisit only when there are ~3+ maintenance tasks **and** a concrete need to "run all
  maintenance in one invocation" or to attach uniform cross-cutting behavior (metrics, per-task retry,
  dashboard). The cheapest evolution is then additive: add an optional category / `IsMaintenance` marker to
  `IScheduledJob` plus a run-all dispatch — still feature-owned logic, still no god-service.

## Glossary

- **Bug_Condition (C)**: The Scheduler's registered service set is a strict superset of what its
  registered job(s) transitively consume (`registeredServices ⊋ servicesConsumedBy(registeredJobs)`).
  True today because Identity, Data Protection, `AmazonBedrockRuntimeClient`, `WebCallbackClient`,
  `HtmlSanitizer`, and the full feature service graph are registered but never used by `purge-audit-logs`.
- **Property (P)**: After the fix, the Scheduler's registered set equals exactly what its jobs consume,
  and the purge job runs successfully — with no Identity, Data Protection, Bedrock client, Web callback
  client, or sanitizer registered.
- **Preservation**: The API/Web audit-write display-name resolution and the `AddInfrastructureServices()`
  effective graph must remain equivalent (byte-for-byte behavior); the purge behavior, invalid-usage
  listing, cancellation/failure exit codes, and Clean Architecture dependency direction are all unchanged.
- **`AuditLogService`**: The `IAuditLogService` implementation in
  `Infrastructure/Services/AuditLog/AuditLogService.cs`. After the fix it retains `LogAsync` (which resolves
  a display name via its existing private `ResolveDisplayNameAsync` helper using `UserManager`) and the
  query methods (`SearchAsync`, `GetByIdAsync`, `GetForExportAsync`); only the purge responsibility is
  extracted into `IAuditLogRetentionService`. Its `UserManager<ApplicationUser>` field and
  `ResolveDisplayNameAsync` helper are left unchanged.
- **`IAuditLogRetentionService`**: New **AuditLog-feature** abstraction (`Application/Features/AuditLog`)
  with the single method `Task<int> PurgeOldEntriesAsync()`. Its implementation
  (`Infrastructure/Services/AuditLog/AuditLogRetentionService.cs`) depends only on `ApplicationDbContext`
  and `IConfiguration`: it reads `AuditLog:RetentionDays` (validated 1..3650, default 365), deletes entries
  older than the cutoff, returns the purged count, and **propagates** exceptions so a background caller can
  retry. This is the sole service the Scheduler depends on, and it carries **zero identity concerns**.
- **`ResolveDisplayNameAsync`**: The existing private helper on `AuditLogService` whose exact logic is:
  `userId is null → string.Empty`; known user → `user.DisplayName ?? string.Empty`; unknown user →
  the `userId` string. It stays **unchanged** and is still called by `LogAsync`; this is the behavior that
  must be preserved (regression 3.1).
- **`AddInfrastructureServices()`**: The existing full-graph registration in
  `InfrastructureServiceExtensions.cs`. Must remain unchanged in behavior/effective graph (regression 3.2);
  it gains one additional registration (`IAuditLogRetentionService`) that is an in-place extension, not a fork.
- **`AddSchedulerInfrastructure()`**: New Infrastructure-owned registration seam that composes only the
  focused dependency set the Scheduler's jobs consume — `ApplicationDbContext` and `IAuditLogRetentionService`.
- **F / F'**: The Scheduler+AuditLog wiring before (F) and after (F') the fix.

## Bug Details

### Bug Condition

The bug manifests whenever the Scheduler process registers or loads any service or infrastructure that
its registered job(s) do not transitively consume. The root enabler is `AuditLogService`'s constructor
dependency on `UserManager<ApplicationUser>`, which forces the Scheduler to register Identity + Data
Protection, and (via the reused `AddInfrastructureServices()`) the entire feature graph — including an
`AmazonBedrockRuntimeClient`, a `WebCallbackClient` pointed at the Web host, and `HtmlSanitizer` — none
of which the purge path exercises.

**Formal Specification:**
```
FUNCTION isBugCondition(X)
  INPUT: X of type SchedulerServiceRegistration
  OUTPUT: boolean

  // True when the Scheduler registers/loads any service or infrastructure
  // that its registered job(s) do not transitively consume.
  RETURN X.registeredServices ⊋ servicesConsumedBy(X.registeredJobs)
END FUNCTION
```

### Examples

- **Bedrock over-inheritance**: Launching `Scheduler.exe purge-audit-logs` today constructs an
  `AmazonBedrockRuntimeClient` (AWS Bedrock) during AI-service registration before deleting a single
  audit row. Expected: no AI service or Bedrock client is registered.
- **Identity over-inheritance**: The Scheduler registers `AddIdentityCore<ApplicationUser>()...` +
  `AddDataProtection()` solely to satisfy `AuditLogService`'s `UserManager` constructor dependency.
  Expected: `PurgeOldEntriesAsync()` runs with no Identity/Data Protection registered.
- **Web-host coupling**: The Scheduler registers a `WebCallbackClient` typed HttpClient targeting
  `https+http://webfrontend`. Expected: no HttpClient targeting the Web host is registered.
- **Defensive accessor**: `SystemCurrentUserAccessor` is registered only to shadow the HTTP-backed
  `CurrentUserAccessor` the full graph pulls in. Expected: neither is registered; the purge job never
  reads `ICurrentUserAccessor`.
- **Edge case — purge still works**: `Scheduler.exe purge-audit-logs` must still delete entries older
  than the `AuditLog:RetentionDays` cutoff and return exit code `0` with the purged count reported — now via
  `IAuditLogRetentionService.PurgeOldEntriesAsync()`.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- The API/Web audit-write path (`AuditLogService.LogAsync`) SHALL resolve the user display name exactly
  as today: known user → `DisplayName` (empty string if null), unknown user → the `userId` string,
  null `userId` → empty string (regression 3.1).
- `AddInfrastructureServices()` SHALL continue to register the full service graph (users, roles, email,
  notifications, AI/Bedrock, LDAP, announcements, page permissions, navigation, sanitizer, Web callback
  client, and the HTTP-backed `CurrentUserAccessor`) with equivalent effective behavior (regression 3.2).
- `Scheduler.exe purge-audit-logs` SHALL continue to delete entries older than the retention cutoff and
  return the success exit code with the purged count reported (regression 3.3).
- Invoking the Scheduler with no/unknown job name SHALL continue to print the available-jobs listing and
  return `ExitCodes.InvalidUsage` (regression 3.4).
- Cancellation (Ctrl+C / SIGTERM) SHALL return `ExitCodes.Cancelled`; an unhandled job exception SHALL
  return `ExitCodes.JobFailed` (regression 3.5).
- The solution SHALL build with 0 errors and all 239 existing tests SHALL pass (regression 3.6).
- The Scheduler SHALL reference only Application, Infrastructure, and ServiceDefaults — never Web
  (regression 3.7).
- Removing the Scheduler from the solution SHALL leave the Web and API hosts building and running
  unaffected (regression 3.8).

**Scope:**
All inputs that are NOT bug-condition inputs (i.e., the API and Web hosts, and the audit-write path) are
completely unaffected. This includes:
- API/Web service registration via `AddInfrastructureServices()`.
- Audit-write display-name resolution for known/unknown/null users.
- The audit-log behavior surface: `LogAsync`, `SearchAsync`, `GetByIdAsync`, `GetForExportAsync` remain on
  `IAuditLogService` (unchanged); the purge behavior is preserved identically but now exposed via
  `IAuditLogRetentionService.PurgeOldEntriesAsync()`. The only caller of purge (the Scheduler's
  `AuditLogRetentionJob`) and the two purge test files retarget to the new service; no other caller exists
  (the API `AuditLogController` uses only `SearchAsync`/`GetByIdAsync`/`GetForExportAsync`).
- The Scheduler's runtime contract: job selection, cancellation, exit-code semantics, and job-listing output.

The actual expected *correct* behavior for the bug-condition inputs (the focused Scheduler graph and a
successful purge) is defined in **Correctness Properties** (Property 1). This section focuses on what must
NOT change.

## Hypothesized Root Cause

Based on the verified code, the root cause is well understood (this is a design/coupling defect, not an
unknown-behavior defect):

1. **Concrete Identity coupling in `AuditLogService`**: The constructor takes
   `UserManager<ApplicationUser>` directly. Only `ResolveDisplayNameAsync` (called only from `LogAsync`)
   uses it. Any host that needs `IAuditLogService` for *any* reason must therefore register the entire
   Identity stack, even on code paths (purge) that never resolve a display name.

2. **Full-graph reuse in the Scheduler**: The Scheduler calls the API's catch-all
   `AddInfrastructureServices()`, which registers every feature service (AI/Bedrock, LDAP, email,
   notifications, Web callback client, sanitizer, etc.). This is convenient but pulls in far more than
   the purge job consumes.

3. **Defensive shadowing**: Because the full graph registers the HTTP-backed `CurrentUserAccessor`
   (which fails without an `HttpContext`), the Scheduler must register `SystemCurrentUserAccessor` to
   shadow it — a symptom of over-inheritance rather than a genuine need (the purge job never reads
   `ICurrentUserAccessor`).

**Fix Approach — structural removal of the coupling (decision):**
The chosen fix removes the coupling at its source rather than working around `AuditLogService`'s
constructor. Because `PurgeOldEntriesAsync()` uses **only** `ApplicationDbContext` + `IConfiguration` (it
never touches `UserManager` or the display-name helper), the purge/retention responsibility is extracted
into a dedicated, feature-owned `IAuditLogRetentionService` (deps: `ApplicationDbContext` + `IConfiguration`
only). The Scheduler depends on that small service and **never constructs `AuditLogService`**, so there is
**no display-name resolution concern in the Scheduler at all** — Identity, Data Protection, and every other
feature dependency simply never enter the Scheduler's graph.

This is a cleaner, structural fix: the earlier direction (keep a single `AuditLogService` and register a
Scheduler-side non-Identity stand-in resolver purely to satisfy construction) is **superseded**, because
that stand-in was only ever a construction workaround for a service the Scheduler doesn't actually need.
Splitting the retention responsibility out eliminates the need for the workaround entirely.

This bugfix intentionally does **NOT** alter `AuditLogService`'s internal `UserManager` usage. The
retention split alone removes the Scheduler coupling, so `AuditLogService` keeps its
`UserManager<ApplicationUser>` field and its private `ResolveDisplayNameAsync` helper exactly as they are
today, and `LogAsync` continues to call that helper. Decoupling `LogAsync`'s display-name lookup behind a
dedicated abstraction is a **separate, out-of-scope refactor** deliberately NOT included here — it would
touch the working API/Web audit-write path, whereas this fix stays focused on the Scheduler.

### Alternatives Considered

**Overall approach — single `AuditLogService` + Scheduler-side no-Identity resolver (superseded).** An
earlier direction kept a single `AuditLogService` and registered a lightweight, non-Identity
`SystemUserDisplayNameResolver` in the Scheduler purely to satisfy `AuditLogService`'s constructor. This is
**superseded** by the retention-service split: extracting `PurgeOldEntriesAsync` into
`IAuditLogRetentionService` removes the coupling **structurally** (the Scheduler never constructs
`AuditLogService`), so the stand-in resolver is no longer needed at all. The split is preferred because it
eliminates a construction workaround rather than encoding one.

**Maintenance orchestration alternatives (evaluated for the retention split):**

- **Centralized `MaintenanceService`/`RetentionService` that knows every feature** — rejected: it becomes a
  god-service coupled to all features, contradicting feature ownership. Each feature owns its own small
  retention/cleanup service instead (see *Design Notes — Future maintenance-task growth*).
- **A separate `IMaintenanceTask`/`IRetentionTask` abstraction now** — deferred: redundant with the existing
  `IScheduledJob` seam (which already exposes `Name`/`Description`/`RunAsync(ct) -> Task<int>`, is resolved
  as an `IEnumerable`, and is dispatched by name). Introduce only if/when ~3+ maintenance tasks and a
  concrete run-all / cross-cutting need emerge.

**Display-name resolver extraction (analyzed, then DEFERRED — out of scope).** Decoupling
`AuditLogService.LogAsync`'s display-name lookup behind a dedicated abstraction (owned by the **Users
feature**, `Application/Features/Users`) was analyzed and is a reasonable **future** improvement, but it is
intentionally **OUT OF SCOPE** for this Scheduler bugfix: the retention split already removes the Scheduler
coupling, and extracting the resolver would touch the working API/Web audit-write path. It is therefore
deferred, not adopted — `AuditLogService` keeps its direct `UserManager` usage in this spec. For the record,
the ownership reasoning that would apply *if* this refactor were undertaken later: the abstraction belongs to
the Users feature because resolving a display name by `userId` yields `ApplicationUser.DisplayName` (a
user-domain attribute), so AuditLog would consume it as a cross-feature dependency (consumer → owner). The
following alternative placements were considered and rejected in that analysis:

- **Fold into an `IUserService` method** — rejected: `UserService` already depends on `IAuditLogService`,
  so having `AuditLogService` depend on `IUserService` creates a dependency cycle
  (`AuditLogService → UserService → AuditLogService`); it also drags `UserService`'s 7 dependencies onto
  the audit path, and carries the wrong throw-semantics (`IUserService` throws `KeyNotFoundException` for
  unknown ids, whereas the resolver must NOT throw — it returns the `userId` string as a fallback).
- **Put it on `IAuthService` / introduce a new `AccountService`** — rejected: the Authentication feature
  (`IAuthService`/`ILoginService`/`IRegisterService`/`ILdapAuthService`/`ILdapLoginService`) operates only
  on the *current authenticated principal* (credentials, sessions, password, 2FA, passkeys, external
  logins). The resolver looks up *arbitrary* users by id (possibly unknown/deleted) — that is the Users
  feature's by-id mode, not Auth's current-principal mode. "Account self-management" is already realized by
  `IAuthService`, so a new `AccountService` would overlap it and, holding only this one method, would be
  premature abstraction.
- **Keep the resolver under `Features/AuditLog`** (next to its first consumer) — rejected:
  this locates a reusable user-domain capability next to its first consumer, forcing future consumers
  (notifications, reporting) to depend on AuditLog. Owning it under Users points the dependency correctly
  (consumer → owner) and lets any feature reuse it without an AuditLog dependency.

## Correctness Properties

Property 1: Bug Condition - Scheduler registers exactly what its jobs consume

_For any_ Scheduler service registration where the bug condition holds (`isBugCondition` returns true —
the registered set is a strict superset of what the jobs consume), the fixed composition SHALL register a
set equal to exactly what the registered job(s) transitively consume: `ApplicationDbContext`,
`IConfiguration`, and `IAuditLogRetentionService` (and their actual transitive dependencies) — and SHALL
NOT register ASP.NET Core Identity / `UserManager`, Data Protection, `AmazonBedrockRuntimeClient`,
`WebCallbackClient`, `HtmlSanitizer`, `ICurrentUserAccessor`, **or** `IAuditLogService`, while
`PurgeOldEntriesAsync()` runs to success.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8**

Property 2: Preservation - API/Web audit-write and infrastructure graph unchanged

_For any_ input where the bug condition does NOT hold (the API/Web hosts and the audit-write path), the
fixed code SHALL produce the same result as the original: `AuditLogService.LogAsync` SHALL resolve the
display name identically (known user → `DisplayName` or empty; unknown user → `userId`; null → empty
string) — unchanged because `AuditLogService.LogAsync` and its private `ResolveDisplayNameAsync` helper are
untouched — and `AddInfrastructureServices()` SHALL register an equivalent effective service graph,
preserving all existing API/Web behavior. The purge behavior is preserved even though it moved services:
`IAuditLogRetentionService.PurgeOldEntriesAsync()` still deletes entries older than the configured cutoff
and returns the purged count exactly as before (the two existing purge test files retarget to
`IAuditLogRetentionService`/`AuditLogRetentionService`).

**Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8**

## Fix Implementation

### Changes Required

Assuming our root-cause analysis is correct (it is verified against current code):

#### Change 1 — Extract `IAuditLogRetentionService` (Application layer, AuditLog feature)

**File**: `AspireWebAppTemplate.Application/Features/AuditLog/IAuditLogRetentionService.cs` (new)

- Define `IAuditLogRetentionService` at the **AuditLog** feature root (namespace
  `AspireWebAppTemplate.Application.Features.AuditLog`, alongside `IAuditLogService`). Splitting a feature
  into multiple focused services is an established template pattern (the Authentication feature ships
  `IAuthService`/`ILoginService`/`IRegisterService`/`ILdapAuthService`/`ILdapLoginService`).
- Method: `Task<int> PurgeOldEntriesAsync()` (name preserved).
- XML docs MUST state the contract: reads `AuditLog:RetentionDays` (validated 1..3650, default 365), deletes
  entries whose timestamp is older than the cutoff, returns the purged count, and **propagates** exceptions
  (unlike `LogAsync`, which swallows them) so a background caller can retry.

**File**: `AspireWebAppTemplate.Infrastructure/Services/AuditLog/AuditLogRetentionService.cs` (new)

- Implements `IAuditLogRetentionService`. Dependencies are **only**: `ApplicationDbContext`,
  `IConfiguration`, and `ILogger<AuditLogRetentionService>` — **no** `UserManager`, Identity, or resolver.
- `PurgeOldEntriesAsync()` moves verbatim from `AuditLogService` (computes cutoff from the validated
  retention days, `ExecuteDeleteAsync` on `Timestamp < cutoff`, returns the deleted count, propagates
  exceptions).
- Move `GetValidatedRetentionDays` (reads `AuditLog:RetentionDays`, validates 1..3650, default 365) into
  this service as a private helper.
- Traditional constructor with explicit field assignment; `#region Constructor` then a domain region (e.g.,
  `#region Retention`) then `#region Private Helpers`; XML docs on the class, constructor, fields, and
  methods per coding standards.

#### Change 2 — Refactor `AuditLogService` (remove purge only)

**File**: `AspireWebAppTemplate.Infrastructure/Services/AuditLog/AuditLogService.cs`

- **Remove `PurgeOldEntriesAsync` and its `GetValidatedRetentionDays` helper** — they move to
  `AuditLogRetentionService` (Change 1). This is the **only** change to `AuditLogService`.
- The `UserManager<ApplicationUser> _userManager` field, the private `ResolveDisplayNameAsync` helper, and
  `LogAsync` are **UNCHANGED**. `LogAsync` continues to call the private `ResolveDisplayNameAsync` helper.
  No resolver abstraction is introduced and no `Features.Users` `using` is added.
- `SearchAsync`, `GetByIdAsync`, `GetForExportAsync`, and `ApplyFilters` are **unchanged**.

**File**: `AspireWebAppTemplate.Application/Features/AuditLog/IAuditLogService.cs`

- **Remove `PurgeOldEntriesAsync` from `IAuditLogService`.** The interface keeps `LogAsync` (Write) and
  `SearchAsync`/`GetByIdAsync`/`GetForExportAsync` (Query). The `#region Write Operations` now contains only
  `LogAsync`; update the region naming/grouping accordingly (the purge signature now lives on
  `IAuditLogRetentionService`).

#### Change 3 — Register the retention service in the full graph (in-place extension)

**File**: `AspireWebAppTemplate.Infrastructure/Extensions/InfrastructureServiceExtensions.cs`

- Add `services.AddScoped<IAuditLogRetentionService, AuditLogRetentionService>();` inside the existing
  `#region Template` of `AddInfrastructureServices()`, alongside the existing `IAuditLogService`
  registration, so the API/Web hosts continue to expose the retention capability (even though only the
  Scheduler currently invokes it).
- This is an **in-place extension** of the AuditLog template capability — NOT a fork or weakening. The
  API/Web effective graph and audit-write behavior remain equivalent (regression 3.2): display-name
  resolution is untouched (still via `AuditLogService.LogAsync`'s existing helper), and the purge behavior
  is identical, now behind `IAuditLogRetentionService`.

#### Change 4 — Infrastructure-owned Scheduler registration seam

**File**: `AspireWebAppTemplate.Infrastructure/Extensions/SchedulerInfrastructureServiceExtensions.cs` (new)

- Rationale for a **separate file/method** (not modifying `AddInfrastructureServices`): keeps the full-graph
  method untouched (regression 3.2) while Infrastructure continues to own DI composition (template
  convention). A distinct file makes the focused seam discoverable and independently testable.
- Signature: `public static IServiceCollection AddSchedulerInfrastructure(this IServiceCollection services, IConfiguration configuration)`.
- Registers **only**:
  - `ApplicationDbContext` via `AddDbContext` → `UseSqlServer(configuration.GetConnectionString("DefaultConnection"), b => b.MigrationsAssembly("AspireWebAppTemplate.Infrastructure"))`.
  - `IAuditLogRetentionService → AuditLogRetentionService` (scoped) — the single service the purge job
    consumes (deps `ApplicationDbContext` + `IConfiguration` only).
- MUST NOT register `IAuditLogService`, Identity, Data Protection, AI/Bedrock,
  `WebCallbackClient`, `HtmlSanitizer`, `ICurrentUserAccessor`, or any other feature service. Because the
  Scheduler no longer constructs `AuditLogService`, no display-name resolution is involved at all.
- Reads the connection string identically to the current Scheduler `Program.cs` (throwing
  `InvalidOperationException` if `DefaultConnection` is missing).

#### Change 5 — Update `AuditLogRetentionJob` to depend on `IAuditLogRetentionService`

**File**: `AspireWebAppTemplate.Scheduler/Jobs/AuditLogRetentionJob.cs`

- The job now depends on `IAuditLogRetentionService` (from
  `AspireWebAppTemplate.Application.Features.AuditLog`) instead of `IAuditLogService`. Replace the injected
  field/constructor parameter accordingly.
- `RunAsync` calls `IAuditLogRetentionService.PurgeOldEntriesAsync()` (previously
  `IAuditLogService.PurgeOldEntriesAsync()`).
- Everything else about the job — its `Name`/`Description`, logging, and returned purged count — is
  unchanged. Exit-code mapping is owned by `JobRunner` and is unaffected.

#### Change 6 — Remove `SystemCurrentUserAccessor`

**File**: `AspireWebAppTemplate.Scheduler/Infrastructure/SystemCurrentUserAccessor.cs` (delete)

- Delete the class and its `AddScoped<ICurrentUserAccessor, SystemCurrentUserAccessor>()` registration.
- Document (in design and `docs/architecture/scheduler.md`) that a system `ICurrentUserAccessor` should be
  reintroduced only when a FUTURE job performs an auditable write: register a scoped `ICurrentUserAccessor`
  with a fixed system principal in the Scheduler's composition, alongside whatever audit-write dependencies
  that future job needs.

#### Change 7 — Reorganize `Program.cs` around an explicit `Main`

**Files** (all in `AspireWebAppTemplate.Scheduler`):
- `Program.cs` — converted to an explicit `static async Task<int> Main(string[] args)`. Shows the flow:
  build/configure host → resolve requested job → execute → return exit code. Keeps the top-of-file
  explanatory comment.
- `SchedulerHostBuilder.cs` (new) — `public static IHost Build(string[] args)`:
  - `Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory })`
  - `builder.AddServiceDefaults()`
  - the explicit `Configuration.SetBasePath(...).AddJsonFile(appsettings...).AddJsonFile(env...).AddEnvironmentVariables()` block
  - `builder.Services.AddSchedulerInfrastructure(builder.Configuration)`
  - job registrations: `builder.Services.AddScoped<IScheduledJob, AuditLogRetentionJob>()`
  - returns `builder.Build()`.
- `JobRunner.cs` (new) — encapsulates: CTS + `Console.CancelKeyPress` wiring, resolving the requested job
  name from args, `CreateScope()`, `GetServices<IScheduledJob>()`, dispatch, and exit-code mapping using
  `ExitCodes` constants (Success/JobFailed/InvalidUsage/Cancelled). Behavior 3.3/3.4/3.5 preserved exactly.
- `JobConsole.cs` (new) — the Spectre `PrintJobTable` helper (available-jobs listing).
- Job registrations remain in the Scheduler composition (`SchedulerHostBuilder`), not in Infrastructure.

#### Change 8 — Remove the unused Data Protection package

**File**: `AspireWebAppTemplate.Scheduler/AspireWebAppTemplate.Scheduler.csproj`

- Remove the `Microsoft.AspNetCore.DataProtection` `PackageReference` (no longer needed once Identity is gone).
- Keep `Microsoft.Extensions.Hosting` and `Spectre.Console`.
- Keep ProjectReferences to Application, Infrastructure, ServiceDefaults (never Web) (regression 3.7).

#### Change 9 — Documentation update

**File**: `docs/architecture/scheduler.md`

- Update to describe the focused `AddSchedulerInfrastructure` seam, the removal of `SystemCurrentUserAccessor`,
  and the guidance for reintroducing a system principal (plus whatever audit-write dependencies are needed)
  when a future job performs an auditable write.

## Testing Strategy

### Validation Approach

Two-phase approach: first surface counterexamples that demonstrate the over-inheritance on the pre-fix
wiring, then verify the focused registration works and that the API/Web audit-write behavior and
infrastructure graph are preserved. Property-based tests use FsCheck.Xunit 3.x with `[Property(MaxTest = 2)]`;
unit tests use xUnit + Moq. Test tag format: `// Bugfix: scheduler-dependency-cleanup, Property N: title`.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix, and confirm the
root-cause analysis (concrete Identity coupling + full-graph reuse). If refuted, re-hypothesize.

**Test Plan**: Build the Scheduler's *current* service collection (full graph + Identity) and enumerate
its registrations to show the excluded types are present. Assert `AuditLogService` currently cannot be
constructed without `UserManager<ApplicationUser>`. Run on UNFIXED code to observe the over-inheritance.

**Test Cases**:
1. **Identity-coupling test**: Attempt to construct `AuditLogService` with only `DbContext` + `IConfiguration`
   (no `UserManager`) — fails on unfixed code (constructor requires `UserManager`).
2. **Bedrock-registration test**: Assert the Scheduler's pre-fix service collection contains
   `AmazonBedrockRuntimeClient` (demonstrates AI over-inheritance) — present on unfixed code.
3. **Web-callback-registration test**: Assert a `WebCallbackClient`/HttpClient targeting the Web host is
   registered pre-fix — present on unfixed code.
4. **Identity/Data-Protection test**: Assert Identity + Data Protection services are registered pre-fix —
   present on unfixed code.

**Expected Counterexamples**:
- `AuditLogService` construction requires `UserManager<ApplicationUser>`.
- The Scheduler's registered set includes `AmazonBedrockRuntimeClient`, `WebCallbackClient`, `HtmlSanitizer`,
  Identity, and Data Protection.
- Possible causes: concrete Identity coupling in `AuditLogService`; reuse of the catch-all
  `AddInfrastructureServices()`.

### Fix Checking

**Goal**: Verify that for all bug-condition inputs, the fixed composition registers exactly what the jobs
consume and the purge job runs successfully.

**Pseudocode:**
```
FOR ALL X WHERE isBugCondition(X) DO
  registered ← resolveRegisteredServices(F'(X))     // AddSchedulerInfrastructure + job registrations
  consumed   ← servicesConsumedBy(X.registeredJobs)
  ASSERT registered = consumed
  ASSERT NOT registers(F'(X), Identity)
  ASSERT NOT registers(F'(X), DataProtection)
  ASSERT NOT registers(F'(X), AmazonBedrockRuntimeClient)
  ASSERT NOT registers(F'(X), WebCallbackClient)
  ASSERT NOT registers(F'(X), HtmlSanitizer)
  ASSERT NOT registers(F'(X), IAuditLogService)
  ASSERT purgeJobRuns(F'(X)) = success
END FOR
```

**Test Plan**: Build a `ServiceCollection`, call `AddSchedulerInfrastructure(configuration)` plus the job
registration (`AddScoped<IScheduledJob, AuditLogRetentionJob>`), build the provider, and:
- Assert `IAuditLogRetentionService` resolves and `PurgeOldEntriesAsync()` runs (against a SQLite in-memory
  `ApplicationDbContext`) returning a purged count / success.
- Assert the excluded types are NOT registered: no `IAuditLogService`, no
  `UserManager<ApplicationUser>`/Identity marker service, no Data Protection marker, no
  `AmazonBedrockRuntimeClient`, no `WebCallbackClient`, no `HtmlSanitizer`, no `ICurrentUserAccessor`.

### Preservation Checking

**Goal**: Verify that for all non-bug-condition inputs (API/Web + audit-write path), the fixed code
produces the same result as the original.

**Pseudocode:**
```
FOR ALL X WHERE NOT isBugCondition(X) DO
  ASSERT auditWriteDisplayNameResolution(F(X)) = auditWriteDisplayNameResolution(F'(X))
  ASSERT infrastructureServiceGraph(F(X))       = infrastructureServiceGraph(F'(X))
  ASSERT F(X) = F'(X)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because it generates
many `userId` inputs across the domain (null, known-user IDs, unknown-user IDs) and asserts the display-name
resolution is unchanged, catching edge cases manual tests might miss.

**Test-adjustment note**: Existing `AuditLogService` `LogAsync`/display-name unit/property tests are
**UNCHANGED** — `AuditLogService` keeps its `UserManager<ApplicationUser>` field and its private
`ResolveDisplayNameAsync` helper, so the tests that mock `UserManager` for the display-name behavior
(null → empty, known → `DisplayName`/empty, unknown → `userId`) continue to apply as-is. The two existing
purge test files (`Tests/AuditLog/PurgeCorrectnessPropertyTests.cs` and
`Tests/AuditLog/RetentionConfigPropertyTests.cs`) retarget from `AuditLogService` to
`AuditLogRetentionService`: their `CreateService` helper now builds the retention service with only
`ApplicationDbContext` + `IConfiguration` (simpler — no `UserManager` to fake), and the asserted
purge/retention behavior is identical. All 239 existing tests must continue to pass (regression 3.6).

**Test Cases**:
1. **`LogAsync` behavior preservation**: Assert `AuditLogService.LogAsync` still persists an entry with the
   resolved `UserDisplayName` unchanged — covered by the EXISTING, unchanged `AuditLogService.LogAsync`
   tests that exercise its `UserManager`-backed `ResolveDisplayNameAsync` helper (null → empty, known →
   `DisplayName`/empty, unknown → `userId`).
2. **Purge behavior preservation**: Assert `AuditLogRetentionService.PurgeOldEntriesAsync()` deletes entries
   older than the cutoff and returns the same purged count the old `AuditLogService.PurgeOldEntriesAsync()`
   produced for the same seeded data and `AuditLog:RetentionDays`.
3. **`AddInfrastructureServices` graph preservation**: Assert the full-graph registration still contains all
   feature services (users, roles, email, notifications, AI, LDAP, announcements, page permissions,
   navigation, sanitizer, Web callback client, HTTP-backed `CurrentUserAccessor`) plus the newly added
   `IAuditLogRetentionService`.

### Unit Tests

- `AuditLogService.LogAsync`: persists entry with resolved display name via its existing `UserManager`-backed
  `ResolveDisplayNameAsync` helper (unchanged; null → empty, known → `DisplayName`/empty, unknown → `userId`).
- `AuditLogRetentionService.PurgeOldEntriesAsync`: unchanged retention/cutoff behavior (uses only DbContext +
  configuration; constructable without Identity or any resolver; propagates exceptions).
- Exit-code mapping in `JobRunner`: unknown/empty job → `InvalidUsage`; cancellation → `Cancelled`;
  thrown exception → `JobFailed`; success passthrough.

### Property-Based Tests

- The retargeted `PurgeCorrectnessPropertyTests` and `RetentionConfigPropertyTests` assert the identical
  purge/retention behavior against `AuditLogRetentionService` across generated seed data and
  `AuditLog:RetentionDays` values (preservation, Property 2).
- Generate Scheduler `ServiceCollection` compositions and assert the registered set never contains the
  excluded infrastructure types (including `IAuditLogService`) while `IAuditLogRetentionService` always
  resolves (fix checking, Property 1).

### Integration Tests

- Full Scheduler run of `purge-audit-logs` against a SQLite in-memory `ApplicationDbContext` seeded with
  old + recent entries: asserts old entries are deleted, recent ones retained, and exit code `Success`.
- Scheduler invoked with no job name and with an unknown job name: asserts the available-jobs listing is
  printed and `InvalidUsage` is returned.
- Cancellation path: asserts a cancelled run returns `Cancelled`.
