# Testing Strategy

## Framework Stack

| Tool | Purpose |
|------|---------|
| xUnit | Test runner and assertions |
| FsCheck 3.1.0 | Property-based testing (randomized input generation) |
| FsCheck.Xunit | xUnit integration for FsCheck properties |
| bUnit 2.0.33 | Blazor component testing |
| Moq 4.20.72 | Mocking dependencies |
| EF Core InMemory | Database test doubles |

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

### Component Tests (bUnit)
Use for:
- Verifying rendered output structure
- Component interaction (button clicks, state changes)
- Conditional rendering logic

### Integration Tests
Use for:
- Database constraint behavior (FK restrict delete)
- Multi-component workflows (auth → page → service → DB)
- Route authorization

## Test File Organization

```
BlazorWebAppTemplate.Tests/
├── Profile/                    (Profile page tests)
│   ├── CancelDiscardsModificationsPropertyTests.cs
│   ├── TimeZoneAutoSavePropertyTests.cs
│   └── ...
├── Preferences/                (Settings page tests)
│   ├── NullFieldDisplayDashPropertyTests.cs
│   ├── TimeZoneSearchFilteringPropertyTests.cs
│   └── ...
├── Theme/                      (Theme service tests)
│   └── ThemeStateServiceTests.cs
└── AuditLog/                   (Audit log tests — planned)
    ├── EntityPersistenceRoundTripPropertyTests.cs
    └── ...
```

## Correctness Properties

Each feature design document defines formal correctness properties. These are encoded as FsCheck property tests with the tag format:
```
Feature: {feature-name}, Property {number}: {title}
```

Properties serve as the bridge between human-readable specifications and machine-verifiable guarantees.
