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
(e.g., `AuditLogRetentionJob` calls `IAuditLogRetentionService.PurgeOldEntriesAsync`) and must not
contain business logic or access the `DbContext` directly. This keeps all business rules in the
service layer.

Because it does not reference Web, the template can also be used purely for scheduler/batch work
(Web can be removed without affecting the Scheduler).

## Structure

```
AspireWebAppTemplate.Scheduler/
├── Program.cs                  <- explicit Main: build host -> resolve job -> execute -> exit code
├── SchedulerHostBuilder.cs     <- host + config + DI composition (focused Scheduler graph)
├── JobRunner.cs                <- cancellation wiring, scope, job dispatch, exit-code mapping
├── JobConsole.cs               <- Spectre.Console helpers (available-jobs table)
├── ExitCodes.cs                <- exit-code contract (0 success, non-zero failure)
├── Jobs/
│   ├── IScheduledJob.cs        <- job contract: Name, Description, Task<int> RunAsync(CancellationToken)
│   └── AuditLogRetentionJob.cs <- the one shipped job (audit-log retention purge)
├── appsettings.json            <- connection string, retention, logging
└── appsettings.Development.json
```

## Composition

The entry point is an explicit `Main` that reads as a flow, delegating each stage to a dedicated type:

```
Program.Main(args) -> SchedulerHostBuilder.Build(args) -> JobRunner (dispatch + exit code)
```

- **`Program.cs`** — explicit `static async Task<int> Main(string[] args)`: build/configure the host,
  resolve the requested job, execute it, return the exit code.
- **`SchedulerHostBuilder.Build(args)`** — owns host + config + DI composition:
  - `AddServiceDefaults()` — Aspire telemetry/logging.
  - Config anchored to `AppContext.BaseDirectory` (Task Scheduler may launch with any working dir).
  - `AddSchedulerInfrastructure(builder.Configuration)` — the **focused** Infrastructure seam
    (see below).
  - One `AddScoped<IScheduledJob, ...>()` registration per job. Job registrations live here in the
    Scheduler, not in Infrastructure.
- **`JobRunner`** — cancellation wiring (`Console.CancelKeyPress` + `CancellationTokenSource`), creates
  the DI scope, resolves `IEnumerable<IScheduledJob>`, dispatches the requested job by name, and maps
  the outcome to an exit code (`Success` / `JobFailed` / `InvalidUsage` / `Cancelled`).
- **`JobConsole`** — Spectre.Console helpers, including the available-jobs table for the
  no/unknown job-name path.

Jobs are resolved from a **DI scope** (never the root provider) so scoped services (`DbContext`,
`IAuditLogRetentionService`) have correct lifetimes.

### Focused Infrastructure seam (`AddSchedulerInfrastructure`)

The Scheduler composes only what its registered job(s) transitively consume, through an
Infrastructure-owned `AddSchedulerInfrastructure(this IServiceCollection, IConfiguration)` extension
(a sibling of `AddInfrastructureServices`, in its own file). It registers **only**:

- `ApplicationDbContext` (`UseSqlServer` on the same `DefaultConnection` as the API; throws if the
  connection string is missing). The Scheduler **does not run migrations** — the API/deploy step
  owns schema.
- `IAuditLogRetentionService -> AuditLogRetentionService` (scoped) — the single service the purge job
  consumes; its own dependencies are just `ApplicationDbContext` + `IConfiguration`.

It deliberately does **not** register `IAuditLogService`, ASP.NET Core Identity, Data Protection,
the AI/Bedrock client, `WebCallbackClient`, `HtmlSanitizer`, `ICurrentUserAccessor`, or any other
feature service. The purge job never constructs `AuditLogService`, so no `UserManager`/display-name
resolution — and therefore no Identity stack — enters the Scheduler's graph. `AddInfrastructureServices()`
(the full API/Web graph) is left untouched; the focused seam is a separate composition, not a fork.

### Adding a job that performs an auditable write

The Scheduler intentionally does **not** register `ICurrentUserAccessor` — the purge job never reads
it. If a **future** job performs an auditable write (calling `IAuditLogService.LogAsync`), reintroduce
a system principal at that point: register a scoped `ICurrentUserAccessor` that returns a fixed
"System" identity, alongside `IAuditLogService` and the other audit-write dependencies that job needs.
Add these to the Scheduler's composition (`SchedulerHostBuilder` / the Infrastructure seam) only when
that need is concrete — not preemptively.

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
3. Register it in `SchedulerHostBuilder`: `builder.Services.AddScoped<IScheduledJob, YourJob>();`.
   If the job needs a service the focused seam does not yet register, add that registration to
   `AddSchedulerInfrastructure` (feature service) or `SchedulerHostBuilder` (job wiring) — keeping the
   graph limited to what the job actually consumes.
4. Create a Windows Task Scheduler task that runs `Scheduler.exe your-job-name`.

Each feature that needs cleanup/retention owns its **own** small service (interface in
`Features/{X}/`, implementation in `Infrastructure/Services/{X}/`) with only the dependencies that
work requires — `IAuditLogRetentionService` is the first example. The job is a thin `IScheduledJob`
adapter that delegates to it. Business apps add jobs here directly — do NOT build a generic job
framework or a centralized maintenance service.

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