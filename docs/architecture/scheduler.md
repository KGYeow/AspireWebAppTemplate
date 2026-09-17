# Scheduler (Optional Batch/Scheduled-Job Component)

`AspireWebAppTemplate.Scheduler` is an **optional** console application for batch/scheduled jobs
(data cleanup, file imports, data processing/synchronization, etc.). It is triggered by
**Windows Task Scheduler**, runs one job, logs, and exits with a process code.

```
Windows Task Scheduler -> Scheduler.exe <job-name> -> run one job -> log -> exit code -> exit
```

It is intentionally a **console app**, NOT a Worker Service: Task Scheduler owns timing, retries,
and run history, so there is no continuous loop or in-process scheduler.

## Why a separate project

- A short-lived batch process has a different lifecycle from the web API/frontend.
- It is trivially removable if an app needs no scheduled jobs (see "Removing").
- It proves the Application/Infrastructure layers are host-agnostic (usable by API and batch alike).

## Architecture & dependencies

The Scheduler is a **host / composition root**, a peer of `ApiService`, referencing inward only:

```
Scheduler -> Application       (job calls service interfaces)
          -> Infrastructure    (service implementations + DbContext registration)
          -> ServiceDefaults   (telemetry / logging)
          -> Domain            (transitively)
          -X Web               (MUST NOT reference)
```

Jobs are **triggers**, not logic: a job calls existing Application service interfaces
(e.g., `IAuditLogService.PurgeOldEntriesAsync`) and must not contain business logic or access the
`DbContext` directly. This keeps all business rules in the service layer.

Because it does not reference Web, the template can also be used purely for scheduler/batch work
(Web can be removed without affecting the Scheduler).

## Structure

```
AspireWebAppTemplate.Scheduler/
├── Program.cs                  <- generic host, config, DI, arg -> job dispatch, exit codes
├── ExitCodes.cs                <- exit-code contract (0 success, non-zero failure)
├── Jobs/
│   ├── IScheduledJob.cs        <- job contract: Name, Description, Task<int> RunAsync(CancellationToken)
│   └── AuditLogRetentionJob.cs <- the one shipped job (audit-log retention purge)
├── Infrastructure/
│   └── SystemCurrentUserAccessor.cs <- fixed "System" principal (no HTTP context in batch runs)
├── appsettings.json            <- connection string, retention, logging
└── appsettings.Development.json
```

## Composition (Program.cs)

`Program.cs` mirrors the API's composition so jobs reuse the same services:

- `AddServiceDefaults()` — Aspire telemetry/logging.
- Config anchored to `AppContext.BaseDirectory` (Task Scheduler may launch with any working dir).
- `AddDbContext<ApplicationDbContext>(...)` — same `DefaultConnection` as the API. The Scheduler
  **does not run migrations**; the API/deploy step owns schema.
- `AddDataProtection()` + `AddIdentityCore<ApplicationUser>().AddRoles().AddEntityFrameworkStores()`
  — Identity managers that several Infrastructure services depend on (no web host to provide them).
- `AddInfrastructureServices()` — the same Application/Infrastructure graph as the API.
- `SystemCurrentUserAccessor` in place of the HTTP-backed accessor (batch jobs have no HTTP request).
- One `AddScoped<IScheduledJob, ...>()` registration per job.

Jobs are resolved from a **DI scope** (never the root provider) so scoped services (`DbContext`,
`UserManager`, feature services) have correct lifetimes.

## Exit codes (read by Task Scheduler as "Last Run Result")

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Job failed (unhandled exception) |
| 2 | Invalid usage (missing/unknown job name) |
| 3 | Cancelled (Ctrl+C / SIGTERM) |

## Console output

Uses **Spectre.Console** for human-friendly output (tables, colored status) on interactive/manual
runs, alongside structured `ILogger` output that is the source of truth for diagnosing Task
Scheduler runs. Spectre degrades gracefully when output is redirected (non-interactive).

## Adding a new job

1. Create a class in `Jobs/` implementing `IScheduledJob` (unique kebab-case `Name`).
2. In `RunAsync`, call the relevant **Application service interface**; return `ExitCodes.Success`
   or a non-zero code on failure.
3. Register it in `Program.cs`: `builder.Services.AddScoped<IScheduledJob, YourJob>();`
4. Create a Windows Task Scheduler task that runs `Scheduler.exe your-job-name`.

Business apps add jobs here directly — do NOT build a generic job framework.

## Windows Task Scheduler setup

- **Program/script:** path to `AspireWebAppTemplate.Scheduler.exe`
- **Add arguments:** the job name (e.g., `purge-audit-logs`)
- **Start in:** the folder containing the exe (recommended; config is also anchored to the exe dir)
- Configure retry/overlap in the task ("Do not start a new instance" to avoid overlap; restart on
  failure for retries). Alert on non-zero "Last Run Result".

## Local development / Aspire

The Scheduler is **not** registered in the Aspire AppHost by default — it is a run-once process, not
a supervised long-lived resource, and in production it is launched by Task Scheduler. Run/debug it
directly (set the job name as a launch argument). `AddServiceDefaults()` still routes its telemetry
to the same backends. Registering it in AppHost is possible for dev telemetry but not recommended
by default (it exits immediately, which looks like a crashed resource).

## Removing the Scheduler (if not needed)

1. Delete the `AspireWebAppTemplate.Scheduler/` project folder.
2. Remove its line from `AspireWebAppTemplate.slnx`.
3. Done — nothing references the Scheduler, so the Web/API app is unaffected.