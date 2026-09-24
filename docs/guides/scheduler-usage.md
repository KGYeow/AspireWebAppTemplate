# Scheduler Usage & Deployment Guide

Practical guide for **developers** and **system administrators** to run, configure, and deploy
`AspireWebAppTemplate.Scheduler` — the optional batch/scheduled-job runner.

> For the *why* behind the design (layering, DI composition, the focused infrastructure seam), see
> [`docs/architecture/scheduler.md`](../architecture/scheduler.md). This guide covers *how to use it*.

---

## TL;DR

- The Scheduler is a **one-shot job runner**: it runs **one named job per launch**, then exits.
- **It requires a job name argument.** Launching it with **no arguments does NOT run any job** — it
  prints the list of available jobs and exits with code `2`.
- **Windows Task Scheduler** owns *when* jobs run. You create **one scheduled task per job**, and each
  task passes the job name in its **"Add arguments"** field.
- The published executable is `AspireWebAppTemplate.Scheduler.exe`.

```
Windows Task Scheduler --> Scheduler.exe <job-name> --> run one job --> log --> exit code --> exit
```

---

## Prerequisites

- **.NET 10 SDK** (to build/run from source) or the **.NET 10 runtime** (to run a framework-dependent
  publish). All projects target `net10.0`.
- Access to the SQL Server database referenced by the `DefaultConnection` connection string
  (the Scheduler **does not run EF Core migrations** — it assumes the schema already exists; the
  API/deploy step owns migrations).
- On the target server: permission to create Windows Task Scheduler tasks and an account that can
  reach the database.

---

## 1. Basic Scheduler execution

The Scheduler runs a single job identified by its **job name** (a kebab-case string such as
`purge-audit-logs`). The shipped template contains one job:

| Job name | Class | What it does |
|----------|-------|--------------|
| `purge-audit-logs` | `AuditLogRetentionJob` | Deletes audit-log entries older than the configured retention period (`AuditLog:RetentionDays`, default 365). |

### What happens when the Scheduler starts

1. `Program.Main(args)` builds the host via `SchedulerHostBuilder.Build(args)` (Aspire service
   defaults, configuration anchored to the executable directory, the focused infrastructure seam, and
   the registered jobs).
2. `JobRunner.RunAsync(host, args)` runs:
   - Reads the **first non-empty argument** as the requested job name.
   - Creates a DI scope and resolves the set of registered `IScheduledJob` implementations.
   - **No job name** -> prints "No job specified", lists available jobs, returns exit code `2`.
   - **Unknown job name** -> prints "Unknown job", lists available jobs, returns exit code `2`.
   - **Match found** (case-insensitive) -> runs that single job's `RunAsync`, then maps the outcome to
     an exit code.
3. The process exits with the resulting code (see [Exit codes](#exit-codes)).

> **The Scheduler runs exactly one job per launch** — the one named on the command line. It does not
> discover-and-run everything. To run several jobs, launch it several times (or configure several
> Task Scheduler tasks — see [section 7](#7-different-trigger-times-for-different-jobs)).

### What a successful run looks like

A run produces a structured console envelope: a header (app name, UTC start time, environment, job,
and a correlation **run id**), timestamped status/phase lines, and a result footer with the overall
status, exit code, and total duration. For example:

```
============================================================
 AspireWebAppTemplate.Scheduler
============================================================
 Start       : 2026-09-23 02:00:01 UTC
 Environment : Production
 Job         : purge-audit-logs
 Run Id      : a58c939c-6cdb-42f4-bec8-c5ee33346b06
============================================================

[02:00:01] STARTING  purge-audit-logs

[02:00:01]   PHASE    Purge old audit-log entries...
[02:00:06]   PHASE    Purge old audit-log entries — done (00:00:05.241)

[02:00:06]   INFO     Purged 1,245 audit-log entrie(s).

[02:00:06] SUCCESS   purge-audit-logs  (00:00:06.241)
------------------------------------------------------------
 Result    : SUCCESS
 Exit code : 0
 Duration  : 00:00:06.241
============================================================
```

On failure the status line shows `FAILED`, followed by the error message, the exception type, and the
run id; the **full stack trace is written to the structured logs** (not the console). The result
footer then shows `FAILED`, exit code `1`, and the duration. Structured `ILogger` output is emitted
alongside these lines and is the durable record when the Scheduler runs headless under Task Scheduler
(where the console is not captured). The run id ties a console snippet back to the logs.

### Running from Visual Studio

The project ships launch profiles (see [section 2](#2-launchsettingsjson)). Pressing **F5** uses the
selected profile from the Visual Studio launch-profile dropdown:

- **`purge-audit-logs`** (default) — runs the shipped job.
- **`no job (usage)`** — runs with no arguments to exercise the usage output / exit code `2`.

Add a profile per job (with its `commandLineArgs` set to the job name) to debug other jobs.

### Running with `dotnet run`

From the repository root, pass the job name **after `--`** so it goes to the app (not the `dotnet`
CLI):

```powershell
dotnet run --project AspireWebAppTemplate.Scheduler -- purge-audit-logs
```

Without `-- purge-audit-logs` you will get the "No job specified" output.

### Running the published `.exe` directly

After publishing (see [Publishing](#publishing)), run the executable with the job name:

```powershell
.\AspireWebAppTemplate.Scheduler.exe purge-audit-logs
```

### Running the published `.exe` from a command prompt / PowerShell

```powershell
# PowerShell
cd "C:\Apps\AspireWebAppTemplate.Scheduler"
.\AspireWebAppTemplate.Scheduler.exe purge-audit-logs
echo "Exit code: $LASTEXITCODE"
```

```bat
:: Command Prompt (cmd.exe)
cd /d "C:\Apps\AspireWebAppTemplate.Scheduler"
AspireWebAppTemplate.Scheduler.exe purge-audit-logs
echo Exit code: %ERRORLEVEL%
```

### After all applicable jobs complete / process exit behavior

The Scheduler runs the one selected job, logs the result (structured `ILogger` output plus a
Spectre.Console summary line), and **exits immediately** with a process exit code. There is no loop
and no lingering process — Windows Task Scheduler records the exit code as the task's
**"Last Run Result"**.

---

## Publishing

Framework-dependent publish (requires the .NET 10 runtime on the target):

```powershell
dotnet publish AspireWebAppTemplate.Scheduler -c Release -o C:\Apps\AspireWebAppTemplate.Scheduler
```

The publish output contains `AspireWebAppTemplate.Scheduler.exe` and its `appsettings.json` /
`appsettings.Development.json` (these are copied to the output via `CopyToOutputDirectory`). Self-
contained or single-file publishes are also possible with the usual `dotnet publish` switches
(`--self-contained`, `-r win-x64`, `/p:PublishSingleFile=true`) if you do not want a runtime
dependency on the server.

---

## 2. `launchSettings.json`

`Properties/launchSettings.json` is a **development-only** launch-profile file used by Visual Studio
and `dotnet run`. It is **not** part of the deployed application.

| Question | Answer |
|----------|--------|
| When is it used? | Only during local development, by the SDK/IDE launch tooling. |
| What is it for? | Storing launch profiles — command-line args, environment name, env vars for F5 / `dotnet run`. |
| Does Visual Studio / `dotnet run` use it? | **Yes.** |
| Does the published `.exe` use it? | **No.** The runtime host does not read it; it is typically not even copied to publish output. |
| Does Windows Task Scheduler use it? | **No.** Task Scheduler supplies arguments via the task's "Add arguments" field. |

**Do not rely on `launchSettings.json` for deployment.** Anything a job needs at runtime
(connection strings, retention days, log levels, environment name) must come from
`appsettings*.json`, environment variables, or the Task Scheduler task itself — never from a launch
profile. Launch profiles configure **how a developer starts the app locally**, not how it runs in
production.

This project ships `AspireWebAppTemplate.Scheduler/Properties/launchSettings.json` with two profiles:

```jsonc
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "purge-audit-logs": {                       // F5 / dotnet run runs the shipped job
      "commandName": "Project",
      "dotnetRunMessages": true,
      "commandLineArgs": "purge-audit-logs",
      "environmentVariables": { "DOTNET_ENVIRONMENT": "Development" }
    },
    "no job (usage)": {                          // runs with no args (prints usage, exit code 2)
      "commandName": "Project",
      "dotnetRunMessages": true,
      "environmentVariables": { "DOTNET_ENVIRONMENT": "Development" }
    }
  }
}
```

- Pick the profile from the Visual Studio launch-profile dropdown (or `dotnet run --launch-profile "<name>"`).
- The Scheduler is a generic-host console app, so the environment is set via **`DOTNET_ENVIRONMENT`**
  (not `ASPNETCORE_ENVIRONMENT`, which applies to web hosts).
- **Add one profile per job** as you introduce new jobs (set its `commandLineArgs` to the job name).
- This affects **local debugging only** — it is not deployed and has no effect on the published exe or
  Task Scheduler.

---

## 3. Behavior of the published EXE without arguments

Running the executable with no arguments:

```powershell
.\AspireWebAppTemplate.Scheduler.exe
```

produces (approximately):

```
No job specified. Usage: Scheduler.exe <job-name>
Available jobs:
+-------------------+-------------------------------------------------------------+
| Job               | Description                                                 |
+-------------------+-------------------------------------------------------------+
| purge-audit-logs  | Deletes audit-log entries older than the configured ...     |
+-------------------+-------------------------------------------------------------+
warn: Scheduler[0] Scheduler invoked with no job name.
```

and exits with code **`2` (Invalid usage)**.

**Explicitly:**

- The Scheduler does **NOT** automatically execute registered jobs when launched without arguments.
- It **requires a job name**.
- It does **NOT** determine which jobs to run from configuration or metadata — job selection is by the
  command-line job name only.

This is by design: *which* job runs (and therefore its effective schedule) is decided by the caller
(you, or a Task Scheduler task), not by the application. See
[section 7](#7-different-trigger-times-for-different-jobs).

---

## 4. Command-line arguments

The Scheduler accepts **one argument: the job name**.

```
AspireWebAppTemplate.Scheduler.exe <job-name>
```

| Aspect | Detail |
|--------|--------|
| Available arguments | The `Name` of any registered job. Shipped: `purge-audit-logs`. |
| Syntax | The **first non-empty** argument is treated as the job name. |
| Matching | **Case-insensitive** (`purge-audit-logs` == `PURGE-AUDIT-LOGS`). |
| Required / optional | **Required.** Omitting it yields exit code `2`. |
| Intended for | Manual execution, testing, and **Task Scheduler** (the task's "Add arguments" field). |
| Extra arguments | Only the first non-empty argument is used to select the job; additional arguments are ignored. |

Examples:

```powershell
.\AspireWebAppTemplate.Scheduler.exe purge-audit-logs   # runs the audit-log retention job
.\AspireWebAppTemplate.Scheduler.exe                     # no job -> usage + exit code 2
.\AspireWebAppTemplate.Scheduler.exe made-up-job         # unknown job -> usage + exit code 2
```

> There are **no other arguments or flags** (no `--all`, no config-key overrides) in the current
> implementation. Do not assume any that are not listed here.

### Exit codes

Windows Task Scheduler records the process exit code as the task's **"Last Run Result"**:

| Code | Meaning |
|------|---------|
| `0` | Success — the job completed. |
| `1` | Job failed — the job threw an unhandled exception. |
| `2` | Invalid usage — no job name, or an unknown job name. |
| `3` | Cancelled — the run was cancelled (Ctrl+C / SIGTERM) before completion. |

---

## 5. Windows Task Scheduler deployment

Create a scheduled task that launches the published executable and passes the job name as an argument.

### Action ("Start a program")

Using the **New Action** dialog (Action = *Start a program*):

| Field | Value | Notes |
|-------|-------|-------|
| **Program/script** | `C:\Apps\AspireWebAppTemplate.Scheduler\AspireWebAppTemplate.Scheduler.exe` | Full path to the published exe. Use the full path (Task Scheduler does not resolve `PATH` reliably). |
| **Add arguments (optional)** | `purge-audit-logs` | **Required for this app** even though the field is labelled "optional". This is the job name. Without it the exe exits with code `2` and runs nothing. |
| **Start in (optional)** | `C:\Apps\AspireWebAppTemplate.Scheduler` | The folder containing the exe. Recommended so relative paths resolve predictably. |

> **Working directory:** the app anchors its configuration to the **executable directory**
> (`AppContext.BaseDirectory`), not the current working directory, so `appsettings.json` is found even
> if "Start in" is blank. Setting "Start in" to the exe folder is still recommended for clarity and for
> any relative paths a future job might use.

### Trigger

Configure the trigger for the job's cadence (e.g., Daily at 02:00). Task Scheduler owns all timing.

### Execution account & "run whether logged on or not"

- Run the task under a **service/least-privilege account** that can reach the database — not an
  interactive admin account.
- Select **"Run whether user is logged on or not"** so the job runs unattended on a server. This runs
  the process in a non-interactive session (no visible console window). Spectre.Console output degrades
  cleanly in that case; the **structured `ILogger` output** is the source of truth for diagnostics.
- Consider **"Do not store password"** with a group-managed service account (gMSA) where available.

### Overlap & retry settings (Settings tab)

- **"Do not start a new instance"** (under *If the task is already running*) to prevent overlapping
  runs of the same job.
- Optionally **"Restart the task if it fails"** for transient failures. Because the app propagates a
  non-zero exit code on failure, Task Scheduler can detect and retry.

### Environment / configuration considerations

- The environment (Development / Staging / Production) is selected via the `DOTNET_ENVIRONMENT`
  environment variable, which controls which `appsettings.{Environment}.json` overlay loads. Set it at
  the machine/user level or leave it unset for the base `appsettings.json`.
- Ensure the execution account's environment can see any environment variables the configuration
  relies on (e.g., a connection string provided via env var). See [section 8](#8-configuration).

### How to test the task manually

1. In Task Scheduler, right-click the task -> **Run**.
2. Watch **"Last Run Result"** — `0x0` means success (exit code `0`). `0x1`/`0x2`/`0x3` map to the
   [exit codes](#exit-codes) above.
3. Or run the exe directly from a terminal with the same account and arguments and inspect
   `$LASTEXITCODE`.

### How to verify execution

- On the **console**, confirm the run envelope: the job's `INFO` line(s) (e.g. `Purged N audit-log
  entrie(s).`) and the `SUCCESS` status + `Result` footer.
- In the **structured logs** (the OpenTelemetry sink wired by `AddServiceDefaults`), confirm the same
  events with full detail. `Information`-level logs go to the sink, **not** the console — the console
  shows only the envelope plus any `Warning`/`Error`. Correlate by the **Run Id** from the header.
- Verify the domain effect (e.g., that old audit-log rows were removed).
- Check **Task Scheduler -> History** for the run and its result.

### Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| Last Run Result `0x2` | No / unknown job name in **Add arguments** | Put the exact job name (`purge-audit-logs`) in the arguments field. |
| Last Run Result `0x1` | Job threw (e.g., DB unreachable) | Check logs; verify `DefaultConnection` and DB connectivity from the task's account. |
| "Connection string 'DefaultConnection' not found." | Missing/incorrect configuration in the deployed `appsettings.json` or env vars | Ensure the deployed `appsettings.json` (or an env var) provides `ConnectionStrings:DefaultConnection`. |
| Task shows success but nothing happened | Wrong account / wrong environment / stale binaries | Confirm account permissions, `DOTNET_ENVIRONMENT`, and the deployed path. |
| Nothing in logs | Non-interactive session; console output not captured | Rely on the structured logging sink, not the console. |

---

## 6. Multiple jobs in one Scheduler

As you add jobs (e.g., `AuditLogRetentionJob`, `UserCleanupJob`, `TemporaryDataCleanupJob`,
`DataArchivalJob`), each is registered as an `IScheduledJob` and selectable by its own `Name`.

**How the current implementation behaves:**

- **Each launch runs exactly one job** — the one named on the command line. Launching the exe does
  **not** run all registered jobs.
- Because only one job runs per launch, there is **no in-process sequencing or concurrency** across
  jobs, and **no cross-job failure interaction**: one job's failure cannot affect another, since they
  run in separate process launches.
- **Completion & exit code:** the single job maps to one exit code (`0` success / `1` failed /
  `3` cancelled). There is no aggregate/batch result because there is no batch — one launch, one job,
  one result.

> If you ever need "run several jobs from a single launch" (with continue-on-failure and an aggregate
> exit code), that would be a deliberate enhancement to `JobRunner` — it is **not** how the current
> implementation works. Do not assume it.

---

## 7. Different trigger times for different jobs

**Different jobs run on different schedules by creating one Windows Task Scheduler task per job**,
each launching the **same** executable but with a **different job name** in its arguments and its own
trigger.

> **Important:** launching the same exe at different times does **not** automatically cause different
> jobs to run. The exe runs whatever **job name argument** it is given. The schedule difference lives
> in the Task Scheduler tasks (their triggers + arguments), not in the application.

Example mapping the scenario to tasks:

| Task Scheduler task | Program/script | Add arguments | Trigger |
|---------------------|----------------|---------------|---------|
| Audit Log Retention | `...\Scheduler.exe` | `purge-audit-logs` | Daily 02:00 |
| User Cleanup | `...\Scheduler.exe` | `user-cleanup` | Daily 03:00 |
| Data Archival | `...\Scheduler.exe` | `data-archival` | Weekly, Sunday 02:00 |
| Temporary Data Cleanup | `...\Scheduler.exe` | `temp-data-cleanup` | Every 6 hours |

```
Windows Task Scheduler
|
+-- Audit Log Retention task   --> Scheduler.exe purge-audit-logs     (daily 02:00)
+-- User Cleanup task          --> Scheduler.exe user-cleanup         (daily 03:00)
+-- Data Archival task         --> Scheduler.exe data-archival        (weekly, Sun 02:00)
+-- Temp Data Cleanup task     --> Scheduler.exe temp-data-cleanup    (every 6 hours)
```

**Responsibility split:**

- **Windows Task Scheduler** decides *when* to launch the exe (triggers) and *which job name to pass*
  (the task's arguments).
- **The Scheduler application** decides *which registered job to run* based on that job-name argument,
  and runs it once.
- **The individual job** contains the actual business/maintenance logic (delegating to Application
  services).

The `user-cleanup`, `data-archival`, and `temp-data-cleanup` jobs above are **illustrative** — only
`purge-audit-logs` ships in the template. Add real jobs as described in
[Adding a new job](../architecture/scheduler.md#adding-a-new-job).

---

## 8. Configuration

| Layer | Source | Responsible for |
|-------|--------|-----------------|
| **Development launch** | `Properties/launchSettings.json` | How a developer starts the app locally (args, env name). **Dev only; never deployed.** |
| **Application configuration** | `appsettings.json`, `appsettings.{Environment}.json`, environment variables, other configured providers | Runtime settings: `ConnectionStrings:DefaultConnection`, `AuditLog:RetentionDays`, logging levels. Anchored to the **executable directory**. |
| **Execution / trigger** | Windows Task Scheduler task | *When* the process launches, *which job name* is passed, the execution account, and overlap/retry policy. |

Notes:

- Configuration is loaded from `AppContext.BaseDirectory` (the exe folder), then
  `appsettings.{DOTNET_ENVIRONMENT}.json`, then environment variables. Later sources override earlier
  ones.
- `ConnectionStrings:DefaultConnection` is **required**; if it is missing the app throws
  `InvalidOperationException: Connection string 'DefaultConnection' not found.` at startup.
- `AuditLog:RetentionDays` controls the purge cutoff (validated to 1-3650, default 365).

---

## 9. Recommended operational workflow

**Development**

```
Developer
  -> dotnet run --project AspireWebAppTemplate.Scheduler -- purge-audit-logs
     (or F5 with a launch profile that sets the job-name argument)
  -> verify the job's log output and its effect
```

**Manual production/staging test**

```
Operator
  -> cd C:\Apps\AspireWebAppTemplate.Scheduler
  -> .\AspireWebAppTemplate.Scheduler.exe purge-audit-logs
  -> check $LASTEXITCODE (0 = success) and the logs
```

**Scheduled production/staging execution**

```
Windows Task Scheduler (per-job task, with job name in "Add arguments")
  -> launches Scheduler.exe <job-name> on its trigger
  -> Scheduler builds host, resolves the named job, runs it once
  -> job completes, process exits with a status code
  -> Task Scheduler records the exit code as "Last Run Result"
```

---

## Common mistakes

- **Expecting the exe to run everything with no arguments.** It requires a job name; no-arg exits with
  code `2` and runs nothing.
- **Assuming multiple triggers on one task run different jobs.** A task always passes the same
  arguments — create **one task per job**.
- **Relying on `launchSettings.json` in production.** It is dev-only and not deployed.
- **Forgetting the job-name argument in the Task Scheduler action.** The "Add arguments" field is
  labelled optional by Windows, but this app requires it.
- **Missing `DefaultConnection` in the deployed config.** The app throws at startup without it.
- **Running the task under an account that cannot reach the database.** Use a least-privilege account
  with DB access and "Run whether user is logged on or not".

---

## See also

- [`docs/architecture/scheduler.md`](../architecture/scheduler.md) — architecture, layering, DI
  composition, and how to add a new job.
- [`docs/guides/deployment.md`](./deployment.md) — overall deployment guidance for the template.