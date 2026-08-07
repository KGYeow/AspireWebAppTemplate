# Testing Strategy

## Framework Stack

| Tool | Purpose |
|------|---------|
| xUnit | Test runner and assertions |
| FsCheck 3.3.3 | Property-based testing (randomized input generation) |
| FsCheck.Xunit | xUnit integration for FsCheck properties |
| Moq 4.20.72 | Mocking dependencies |
| Microsoft.EntityFrameworkCore.Sqlite | SQLite in-memory for data layer tests |
| Aspire.Hosting.Testing | Integration test hosting |

## When to Use Each Approach

### Property-Based Testing (FsCheck)
Use for logic with meaningful input variation:
- Validation attributes (OptionalPhoneAttribute)
- Filtering/sorting logic (search text matching, date range filtering)
- State machines (ThemeStateService transitions)
- Data transformation (timezone formatting, CSV row generation)

Configuration: `[Property(MaxTest = 100)]` minimum.

### Example-Based Unit Tests (xUnit)
Use for:
- Specific edge cases not covered by PBT
- Configuration validation (enum values, attribute presence)
- Simple CRUD operations with known inputs/outputs
- Error handling paths

### Integration Tests
Use for:
- Database constraint behavior (FK restrict delete)
- Multi-component workflows (auth → page → service → DB)
- Route authorization
- End-to-end Aspire hosting scenarios

## Test File Organization

```
AspireWebAppTemplate.Tests/
├── Announcements/              ← Announcement feature tests
├── ControllerServiceRefactor/  ← Service layer tests
├── AuditLog/                   ← Audit log tests
├── Email/                      ← Email template/service tests
├── Notifications/              ← Notification feature tests
├── PagePermissions/            ← Page permission tests
├── Services/                   ← Service-level unit tests
└── Layout/                     ← Layout/component tests
```

## Correctness Properties

Each feature design document defines formal correctness properties. These are encoded as FsCheck property tests with the tag format:
```
Feature: {feature-name}, Property {number}: {title}
```

Properties serve as the bridge between human-readable specifications and machine-verifiable guarantees.
