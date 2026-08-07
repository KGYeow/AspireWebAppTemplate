# Technology Stack

## Core Framework

| Technology | Version | Purpose |
|-----------|---------|---------|
| .NET | 10.0 | Runtime and SDK |
| C# | 13 | Primary language |
| Blazor Server | Interactive Server | UI rendering via SignalR |
| ASP.NET Core Identity | 10.0 | Authentication & authorization |
| .NET Aspire | — | Service orchestration, discovery, telemetry, health checks |

## UI & Components

| Package | Version | Purpose |
|---------|---------|---------|
| MudBlazor | 9.5.0 | Material Design component library |
| Radzen.Blazor | — | HtmlEditor component (WYSIWYG rich text editing) |
| DefaultTheme / JabilTheme | — | Custom themes with PaletteLight/PaletteDark |

## Data Access

| Technology | Version | Purpose |
|-----------|---------|---------|
| Entity Framework Core | 10.0.9 | ORM and migrations |
| SQL Server | — | Primary database |
| System.DirectoryServices | — | LDAP/Active Directory integration |

## Testing

| Package | Version | Purpose |
|---------|---------|---------|
| xUnit | Latest | Test framework |
| FsCheck | 3.3.3 | Property-based testing |
| FsCheck.Xunit | 3.3.3 | xUnit integration for FsCheck |
| Moq | 4.20.72 | Mocking framework |
| Aspire.Hosting.Testing | — | Integration test hosting |
| Microsoft.EntityFrameworkCore.Sqlite | — | SQLite in-memory for data layer tests |

## JavaScript Interop

| Module | Location | Purpose |
|--------|----------|---------|
| `timezone.js` | `wwwroot/js/` | Browser timezone detection via `Intl.DateTimeFormat` |
| `theme.js` | `wwwroot/js/` | OS color scheme detection via `window.matchMedia` |

## Key Libraries

| Package | Project | Purpose |
|---------|---------|---------|
| EPPlus | Infrastructure | Excel export (audit log, reports) |
| AWSSDK.BedrockRuntime | Infrastructure | Amazon Bedrock AI model invocation (Converse API) |
| Ganss.Xss.HtmlSanitizer | Infrastructure | Server-side HTML content sanitization |
| System.DirectoryServices | Infrastructure | LDAP/Active Directory integration |

## NuGet Packages by Project

```xml
<!-- Domain -->
(No external dependencies — keeps it portable)

<!-- Application -->
(No external dependencies — depends on Domain only)

<!-- Infrastructure -->
Microsoft.AspNetCore.Identity.EntityFrameworkCore
Microsoft.EntityFrameworkCore.SqlServer
Microsoft.EntityFrameworkCore.Tools
AWSSDK.BedrockRuntime
EPPlus
Ganss.Xss.HtmlSanitizer
System.DirectoryServices
System.DirectoryServices.Protocols

<!-- ApiService -->
Microsoft.AspNetCore.OpenApi

<!-- Web -->
MudBlazor
Radzen.Blazor
Microsoft.AspNetCore.SignalR.Client

<!-- UI (Razor Class Library) -->
MudBlazor

<!-- Tests -->
xunit
FsCheck.Xunit
Moq
Aspire.Hosting.Testing
Microsoft.EntityFrameworkCore.Sqlite
```
