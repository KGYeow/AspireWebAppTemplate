# Technology Stack

## Core Framework

| Technology | Version | Purpose |
|-----------|---------|---------|
| .NET | 10.0 | Runtime and SDK |
| C# | 13 | Primary language |
| Blazor Server | Interactive Server | UI rendering via SignalR |
| ASP.NET Core Identity | 10.0 | Authentication & authorization |

## UI & Components

| Package | Version | Purpose |
|---------|---------|---------|
| MudBlazor | 9.4.0 | Material Design component library |
| Custom Theme | — | ApplicationTheme with PaletteLight/PaletteDark |

## Data Access

| Technology | Version | Purpose |
|-----------|---------|---------|
| Entity Framework Core | 10.0.3 | ORM and migrations |
| SQL Server | — | Primary database |
| System.DirectoryServices | — | LDAP/Active Directory integration |

## Testing

| Package | Version | Purpose |
|---------|---------|---------|
| xUnit | Latest | Test framework |
| FsCheck | 3.1.0 | Property-based testing |
| FsCheck.Xunit | 3.1.0 | xUnit integration for FsCheck |
| bUnit | 2.0.33-preview | Blazor component testing |
| Moq | 4.20.72 | Mocking framework |

## JavaScript Interop

| Module | Location | Purpose |
|--------|----------|---------|
| `timezone.js` | `wwwroot/js/` | Browser timezone detection via `Intl.DateTimeFormat` |
| `theme.js` | `wwwroot/js/` | OS color scheme detection via `window.matchMedia` |

## Key NuGet Packages

```xml
<!-- Main Project -->
Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore
Microsoft.AspNetCore.Identity.EntityFrameworkCore
Microsoft.EntityFrameworkCore.SqlServer
Microsoft.EntityFrameworkCore.Tools
MudBlazor
System.DirectoryServices
System.DirectoryServices.Protocols

<!-- Core Project -->
(No external dependencies — keeps it portable)

<!-- UI Project -->
MudBlazor

<!-- Test Project -->
xunit
FsCheck.Xunit
bunit
Moq
Microsoft.EntityFrameworkCore.InMemory
```
