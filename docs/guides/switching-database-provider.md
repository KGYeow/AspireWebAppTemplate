# Switching the Database Provider (SQL Server → PostgreSQL)

This template ships with **SQL Server** as its database provider. The codebase is deliberately
provider-portable — it uses only standard EF Core LINQ (no raw SQL, no stored procedures, no
database-specific functions in application code), so switching the whole application to
**PostgreSQL** is a small, well-scoped change.

> **Scope of this guide.** This describes converting the template to run on PostgreSQL *instead of*
> SQL Server — the typical case where an organization picks one database and stays on it. It is
> **not** a guide to running both providers simultaneously from one deployment. See
> [Running both providers](#running-both-providers-advanced) at the end for that (rarely needed)
> escalation.

---

## Two levels of portability (read this first)

Be clear which goal you are pursuing — they require different amounts of work:

- **Provider switching** — make the app *build and run* on PostgreSQL (swap the provider + package +
  migrations). This is the small change set below.
- **Full behavioral portability** — guarantee the app *behaves identically* on PostgreSQL. This
  additionally requires integration tests against a real PostgreSQL instance to catch the
  behavioral differences noted in [Verify behavioral differences](#5-verify-behavioral-differences).

EF Core's provider abstraction covers the vast majority of the work; it does **not** make two
relational databases behave identically.

---

## What is (and isn't) coupled to SQL Server

**Provider-agnostic (no change needed):** all entities, DTOs, services, business logic, and LINQ
queries; `ExecuteDeleteAsync`; transaction/execution-strategy calls; the connection-string plumbing;
and the Aspire AppHost (it does **not** provision a database resource — the connection string is
supplied purely from configuration).

**SQL-Server-specific (must change):**

1. The EF Core provider package (`Microsoft.EntityFrameworkCore.SqlServer`).
2. The provider registration (`UseSqlServer`) in the two composition roots.
3. Two `HasDefaultValueSql("GETUTCDATE()")` calls in entity configurations.
4. The EF Core migration set (generated for SQL Server; PostgreSQL needs its own).

That's the entire surface. There are **no** stored procedures, views, database functions, raw SQL,
`FromSql`, `SqlConnection`/`SqlCommand`, or SQL-Server-specific data types in application code.

---

## Conversion steps

### 1. Swap the EF Core provider package

In `AspireWebAppTemplate.Infrastructure/AspireWebAppTemplate.Infrastructure.csproj`, replace the SQL
Server provider with the PostgreSQL provider:

```xml
<!-- remove -->
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.9" />
<!-- add (use the version matching your EF Core 10 line) -->
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.*" />
```

Keep `Microsoft.EntityFrameworkCore.Tools` (used for migrations).

### 2. Change the provider registration in the two composition roots

The provider is registered in exactly two places (the API host and the Scheduler host). Change
`UseSqlServer` to `UseNpgsql` in both:

- `AspireWebAppTemplate.ApiService/Program.cs`
- `AspireWebAppTemplate.Scheduler/Hosting/SchedulerHostBuilder.cs`

```csharp
// from
options.UseSqlServer(connectionString, b => b.MigrationsAssembly("AspireWebAppTemplate.Infrastructure"))
// to
options.UseNpgsql(connectionString, b => b.MigrationsAssembly("AspireWebAppTemplate.Infrastructure"))
```

> Tip: this is also a good moment to extract the duplicated registration into a single
> `AddApplicationDatabase(configuration)` extension in `Infrastructure/Extensions/` so there is one
> place to configure the provider. Optional, but it removes the current duplication across the two
> hosts.

### 3. Replace the SQL-Server-specific default-value SQL

Two entity configurations set a database-level UTC default using the T-SQL function `GETUTCDATE()`:

- `AspireWebAppTemplate.Infrastructure/Data/Configurations/NotificationConfiguration.cs`
- `AspireWebAppTemplate.Infrastructure/Data/Configurations/AuditLogEntryConfiguration.cs`

```csharp
// SQL Server
builder.Property(e => e.CreatedAtUtc).HasDefaultValueSql("GETUTCDATE()");
// PostgreSQL equivalent
builder.Property(e => e.CreatedAtUtc).HasDefaultValueSql("timezone('utc', now())");
```

These defaults are only a **safety net** — the application services already set these timestamps
explicitly on creation. You may instead simply remove the `HasDefaultValueSql(...)` calls and rely
on the application-set values, which keeps the configuration fully provider-neutral.

### 4. Regenerate migrations for PostgreSQL

EF Core migrations are **provider-specific** and cannot be reused across providers — the existing set
in `AspireWebAppTemplate.Infrastructure/Data/Migrations/` is typed for SQL Server (`nvarchar`,
`datetime2`, `uniqueidentifier`, `SqlServer:Identity`). For a clean single-provider conversion:

1. Delete the existing `Data/Migrations/` contents (they target SQL Server).
2. Scaffold a fresh initial migration against PostgreSQL:

   ```bash
   dotnet ef migrations add InitialCreate \
     --project AspireWebAppTemplate.Infrastructure \
     --startup-project AspireWebAppTemplate.ApiService
   ```

3. Apply it (or let the app auto-migrate on startup in Development, as it does today):

   ```bash
   dotnet ef database update \
     --project AspireWebAppTemplate.Infrastructure \
     --startup-project AspireWebAppTemplate.ApiService
   ```

> If you must preserve existing SQL Server data/history, do a data migration rather than regenerating
> — that is a separate, larger exercise outside this template's scope.

### 5. Update connection strings

Set PostgreSQL connection strings in `ConnectionStrings:DefaultConnection` for both hosts
(`AspireWebAppTemplate.ApiService/appsettings.json` and
`AspireWebAppTemplate.Scheduler/appsettings.json`), for example:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=AspireWebAppTemplateDB;Username=postgres;Password=postgres"
  }
}
```

The `SecureConnectionString` utility and configuration plumbing are provider-neutral and need no
change.

---

## 5. Verify behavioral differences

These are the places SQL Server and PostgreSQL differ in ways EF Core does **not** paper over. They
will not show up in the unit/property tests (which run on SQLite in-memory) — verify them against a
real PostgreSQL instance:

| Area | Difference to verify |
|------|----------------------|
| **String case sensitivity** | SQL Server comparisons/`LIKE` are case-insensitive by default; PostgreSQL is case-sensitive. The audit-log search uses `.ToLower().Contains(...)`, which is safe, but confirm any equality-based lookups behave as intended. |
| **`DateTime` / UTC kind** | Npgsql maps `DateTime` to `timestamp`/`timestamptz` with stricter `DateTimeKind` rules than SQL Server. The template's "all UTC" convention helps; still verify reads/writes round-trip correctly. |
| **Execution-strategy retry** | `PagePermissionService` uses `CreateExecutionStrategy()` for transient-fault retry. The default Npgsql strategy differs from SQL Server's; configure Npgsql's retry if you rely on it. |
| **Identity / GUID / bool mapping** | `int` identity → `GENERATED ... AS IDENTITY`, `Guid` → `uuid`, `bool` → `boolean`. EF handles these, but the generated schema differs (hence the fresh migration). |

---

## Aspire impact

**None for the current setup.** The AppHost does not provision a database resource — the connection
string is entirely configuration-driven — so switching providers requires no AppHost change. If you
later want Aspire to *provision* PostgreSQL for local development, Aspire offers an `AddPostgres(...)`
resource integration (the SQL Server equivalent is `AddSqlServer(...)`); wiring that is an optional
addition, not a requirement of this conversion.

---

## Testing

- **Unit / property tests** — provider-independent; they run on **SQLite in-memory** today and need
  no change. They validate model shape and LINQ, not provider-specific behavior.
- **Integration tests** — add a suite that runs against a **real PostgreSQL** instance if you want
  confidence in full behavioral portability (the differences in the table above). SQLite in-memory is
  **not** sufficient to catch PostgreSQL-specific behavior.

---

## Running both providers (advanced)

Only if a single codebase must run on **both** SQL Server and PostgreSQL across environments (e.g.,
Dev on PostgreSQL, Prod on SQL Server) — which is uncommon for a per-organization template:

1. Reference **both** provider packages.
2. Add a `Database:Provider` configuration key and a single `AddApplicationDatabase(configuration)`
   extension that branches `UseSqlServer` / `UseNpgsql` on that key.
3. Maintain **two migration sets** (e.g., `Data/Migrations/SqlServer/` and `Data/Migrations/PostgreSql/`)
   selected by the active provider — every schema change must be scaffolded twice.
4. Run integration tests against **both** providers.

This is a real, ongoing maintenance cost (chiefly the dual migrations). Prefer the single-provider
conversion above unless simultaneous multi-provider support is a hard requirement.