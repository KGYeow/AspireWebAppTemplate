# Implementation Plan: Clean Architecture Migration

## Overview

This plan migrates the solution from 7 projects to 9 projects by creating Domain, Application, and Infrastructure, then moving files in dependency order (innermost first), updating references, and removing Core. The solution must compile after each major task group. The implementation language is C# (.NET 10).

## Tasks

- [x] 1. Create new project shells and add to solution
  - [x] 1.1 Create Domain project and add to solution
    - Create `AspireWebAppTemplate.Domain/AspireWebAppTemplate.Domain.csproj` as a class library targeting net10.0 with ImplicitUsings and Nullable enabled, zero PackageReferences, zero ProjectReferences
    - Create empty folder structure: `Entities/`, `Enums/`, `ValueObjects/`, `Constants/`, `Attributes/`
    - Add project to `AspireWebAppTemplate.slnx`
    - _Requirements: 1.1, 1.2, 1.6, 1.8_

  - [x] 1.2 Create Application project and add to solution
    - Create `AspireWebAppTemplate.Application/AspireWebAppTemplate.Application.csproj` as a class library targeting net10.0, with a single ProjectReference to Domain, zero PackageReferences
    - Create empty folder structure: `Abstractions/`, `Contracts/`, `Common/`, `Extensions/`
    - Add project to `AspireWebAppTemplate.slnx`
    - _Requirements: 2.1, 2.2, 2.7_

  - [x] 1.3 Create Infrastructure project and add to solution
    - Create `AspireWebAppTemplate.Infrastructure/AspireWebAppTemplate.Infrastructure.csproj` as a class library targeting net10.0, with ProjectReference to Application, `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, and all NuGet packages moved from ApiService (Identity.EntityFrameworkCore, EF Core SqlServer, EF Core Tools, AWSSDK.BedrockRuntime, EPPlus, HtmlSanitizer, System.DirectoryServices, System.DirectoryServices.Protocols) with same versions
    - Create empty folder structure: `Data/`, `Data/Entities/`, `Data/Configurations/`, `Data/Migrations/`, `Data/SeedData/`, `Identity/`, `Services/`, `Clients/`, `Handlers/`, `Utilities/`, `Extensions/`
    - Add project to `AspireWebAppTemplate.slnx`
    - _Requirements: 3.1, 3.2, 3.15, 3.16_

  - [x] 1.4 Update ApiService.csproj references (bridge phase)
    - Add ProjectReference to Application and Infrastructure
    - Keep existing Core reference temporarily (bridge — consumers still need it until files move)
    - Remove NuGet packages that moved to Infrastructure (EF Core, Identity, AWSSDK, EPPlus, HtmlSanitizer, System.DirectoryServices, System.DirectoryServices.Protocols). Keep Microsoft.AspNetCore.OpenApi.
    - _Requirements: 4.2, 4.5_

- [x] 2. Checkpoint - Verify solution compiles with empty new projects
  - Run `dotnet build` at solution level and verify zero errors (new projects are empty, no conflicts)

- [x] 3. Populate Domain project (move from Core + ApiService)
  - [x] 3.1 Move enum files from Core to Domain
    - Move all files from `Core/Domain/Enums/` to `Domain/Enums/`
    - Update namespace in each file from `AspireWebAppTemplate.Core.Domain.Enums` to `AspireWebAppTemplate.Domain.Enums`
    - _Requirements: 1.3_

  - [x] 3.2 Move constants from Core to Domain
    - Move `Core/Common/Defaults/SystemPageDefaults.cs`, `DateTimeFormatDefaults.cs`, `ExportDefaults.cs` to `Domain/Constants/`
    - Update namespace from `AspireWebAppTemplate.Core.Common.Defaults` to `AspireWebAppTemplate.Domain.Constants`
    - _Requirements: 1.4_

  - [x] 3.3 Move attributes from Core to Domain
    - Move `Core/Utilities/Attributes/ExportColumnAttribute.cs` and `OptionalPhoneAttribute.cs` to `Domain/Attributes/`
    - Update namespace from `AspireWebAppTemplate.Core.Utilities.Attributes` to `AspireWebAppTemplate.Domain.Attributes`
    - _Requirements: 1.5_

  - [x] 3.4 Move EmailTemplate entity from ApiService to Domain
    - Move `ApiService/Data/Entities/EmailTemplate.cs` to `Domain/Entities/`
    - Update namespace from `AspireWebAppTemplate.ApiService.Data.Entities` to `AspireWebAppTemplate.Domain.Entities`
    - Remove any `using` statements referencing Identity types (EmailTemplate is identity-free per architecture decisions)
    - _Requirements: 1.7_

  - [x] 3.5 Update using statements across solution for Domain types
    - In all `.cs` files across ApiService, Web, Tests, Core: replace `using AspireWebAppTemplate.Core.Domain.Enums` with `using AspireWebAppTemplate.Domain.Enums`
    - Replace `using AspireWebAppTemplate.Core.Common.Defaults` with `using AspireWebAppTemplate.Domain.Constants`
    - Replace `using AspireWebAppTemplate.Core.Utilities.Attributes` with `using AspireWebAppTemplate.Domain.Attributes`
    - Replace `using AspireWebAppTemplate.ApiService.Data.Entities` references to EmailTemplate with `using AspireWebAppTemplate.Domain.Entities` (only where EmailTemplate is the type used)
    - Add `<ProjectReference>` to Domain in Core.csproj temporarily (bridge: Core files still reference Domain enums)
    - _Requirements: 11.1_

- [x] 4. Checkpoint - Verify solution compiles with Domain populated
  - Run `dotnet build` and verify zero errors

- [x] 5. Populate Application project (move from Core + ApiService)
  - [x] 5.1 Move service interfaces from ApiService/Abstractions to Application
    - Move all files from `ApiService/Abstractions/` to `Application/Abstractions/`
    - Update namespace from `AspireWebAppTemplate.ApiService.Abstractions` to `AspireWebAppTemplate.Application.Abstractions`
    - _Requirements: 2.3_

  - [x] 5.2 Move shared abstractions from Core to Application
    - Move `Core/Application/Abstractions/INavigationProvider.cs` and `ITimeZoneService.cs` to `Application/Abstractions/`
    - Update namespace from `AspireWebAppTemplate.Core.Application.Abstractions` to `AspireWebAppTemplate.Application.Abstractions`
    - _Requirements: 2.3_

  - [x] 5.3 Move DTOs/Contracts from Core to Application
    - Move entire `Core/Contracts/` folder structure (Ai/, Announcements/, AuditLog/, Auth/, Email/, Notifications/, PagePermissions/, Roles/, Users/) to `Application/Contracts/`
    - Move `Core/Contracts/PagedResult.cs` to `Application/Common/PagedResult.cs`
    - Update all namespaces from `AspireWebAppTemplate.Core.Contracts.*` to `AspireWebAppTemplate.Application.Contracts.*`
    - Update `PagedResult` namespace to `AspireWebAppTemplate.Application.Common`
    - _Requirements: 2.4, 2.5_

  - [x] 5.4 Move Common types from Core to Application
    - Move `Core/Common/ApiResult.cs` to `Application/Common/ApiResult.cs`
    - Move `Core/Common/NavItem.cs` to `Application/Common/NavItem.cs`
    - Update namespaces from `AspireWebAppTemplate.Core.Common` to `AspireWebAppTemplate.Application.Common`
    - _Requirements: 2.5_

  - [x] 5.5 Move Extensions from Core to Application
    - Move `Core/Extensions/NavigationProviderExtensions.cs` and `QueryableExtensions.cs` to `Application/Extensions/`
    - Update namespace from `AspireWebAppTemplate.Core.Extensions` to `AspireWebAppTemplate.Application.Extensions`
    - _Requirements: 2.6_

  - [x] 5.6 Update using statements across solution for Application types
    - In all `.cs` files across ApiService, Web, Tests: replace `using AspireWebAppTemplate.ApiService.Abstractions` with `using AspireWebAppTemplate.Application.Abstractions`
    - Replace `using AspireWebAppTemplate.Core.Application.Abstractions` with `using AspireWebAppTemplate.Application.Abstractions`
    - Replace `using AspireWebAppTemplate.Core.Contracts.*` with `using AspireWebAppTemplate.Application.Contracts.*`
    - Replace `using AspireWebAppTemplate.Core.Common` with `using AspireWebAppTemplate.Application.Common`
    - Replace `using AspireWebAppTemplate.Core.Extensions` with `using AspireWebAppTemplate.Application.Extensions`
    - _Requirements: 11.1, 11.2_

- [x] 6. Checkpoint - Verify solution compiles with Application populated
  - Run `dotnet build` and verify zero errors

- [x] 7. Populate Infrastructure project (move from ApiService + Core)
  - [x] 7.1 Move Identity entities to Infrastructure
    - Move `ApiService/Data/Entities/ApplicationUser.cs` and `ApplicationRole.cs` to `Infrastructure/Identity/`
    - Update namespace from `AspireWebAppTemplate.ApiService.Data.Entities` to `AspireWebAppTemplate.Infrastructure.Identity`
    - _Requirements: 3.5_

  - [x] 7.2 Move data entities to Infrastructure
    - Move `ApiService/Data/Entities/Announcement.cs`, `AnnouncementDismissal.cs`, `AuditLogEntry.cs`, `Notification.cs`, `NotificationPreference.cs`, `PagePermission.cs` to `Infrastructure/Data/Entities/`
    - Update namespace from `AspireWebAppTemplate.ApiService.Data.Entities` to `AspireWebAppTemplate.Infrastructure.Data.Entities`
    - _Requirements: 3.4_

  - [x] 7.3 Move ApplicationDbContext to Infrastructure
    - Move `ApiService/Data/ApplicationDbContext.cs` to `Infrastructure/Data/`
    - Update namespace from `AspireWebAppTemplate.ApiService.Data` to `AspireWebAppTemplate.Infrastructure.Data`
    - Update entity `using` statements to reference new Identity and Data.Entities namespaces
    - Add `using AspireWebAppTemplate.Domain.Entities` for EmailTemplate DbSet
    - _Requirements: 3.3_

  - [x] 7.4 Move EF Core configurations to Infrastructure
    - Move all files from `ApiService/Data/Configurations/` to `Infrastructure/Data/Configurations/`
    - Update namespace from `AspireWebAppTemplate.ApiService.Data.Configurations` to `AspireWebAppTemplate.Infrastructure.Data.Configurations`
    - Update entity `using` statements to reference new namespaces (Domain.Entities for EmailTemplate, Infrastructure.Data.Entities for others, Infrastructure.Identity for ApplicationUser/Role)
    - _Requirements: 3.6_

  - [x] 7.5 Move EF Core migrations to Infrastructure
    - Move all files from `ApiService/Data/Migrations/` to `Infrastructure/Data/Migrations/`
    - Update namespace in each migration file from `AspireWebAppTemplate.ApiService.Data.Migrations` to `AspireWebAppTemplate.Infrastructure.Data.Migrations`
    - Update any `using` statements within migrations referencing old entity namespaces
    - _Requirements: 3.7, 9.2_

  - [x] 7.6 Move seed data to Infrastructure
    - Move all files from `ApiService/Data/SeedData/` to `Infrastructure/Data/SeedData/`
    - Update namespace from `AspireWebAppTemplate.ApiService.Data.SeedData` (or `AspireWebAppTemplate.ApiService.Data`) to `AspireWebAppTemplate.Infrastructure.Data.SeedData`
    - Update entity/enum `using` statements to reference new namespaces
    - _Requirements: 3.8_

  - [x] 7.7 Move service implementations to Infrastructure
    - Move all service `.cs` files from `ApiService/Services/` (AiService, AnnouncementService, AuditLogService, AuthService, EmailService, EmailTemplateService, ExcelExportService, LdapAuthService, LdapLoginService, LoginService, NavigationService, NotificationService, PagePermissionService, RegisterService, RoleService, UserService) to `Infrastructure/Services/`
    - Move `Core/Application/Services/DefaultNavigationProvider.cs` and `TimeZoneService.cs` to `Infrastructure/Services/`
    - Update namespace from `AspireWebAppTemplate.ApiService.Services` and `AspireWebAppTemplate.Core.Application.Services` to `AspireWebAppTemplate.Infrastructure.Services`
    - Update all `using` statements within service files to reference new namespaces (Application.Abstractions, Infrastructure.Data, Infrastructure.Identity, etc.)
    - _Requirements: 3.9_

  - [x] 7.8 Move clients, handlers, and utilities to Infrastructure
    - Move `ApiService/Services/Clients/WebCallbackClient.cs` to `Infrastructure/Clients/`
    - Move `ApiService/Services/Handlers/InternalApiKeyDelegatingHandler.cs` to `Infrastructure/Handlers/`
    - Move `ApiService/Services/Infrastructure/CurrentUserAccessor.cs` to `Infrastructure/Utilities/`
    - Move `ApiService/Utilities/AuditChangeHelper.cs` to `Infrastructure/Utilities/`
    - Move `Core/Utilities/SecureConnectionString.cs` to `Infrastructure/Utilities/`
    - Update namespaces to `AspireWebAppTemplate.Infrastructure.Clients`, `.Handlers`, `.Utilities` respectively
    - _Requirements: 3.10, 3.11, 3.12, 3.13_

  - [x] 7.9 Create InfrastructureServiceExtensions.cs
    - Move `ApiService/Extensions/ApplicationServiceExtensions.cs` to `Infrastructure/Extensions/InfrastructureServiceExtensions.cs`
    - Rename the extension method from `AddApplicationServices` to `AddInfrastructureServices`
    - Update namespace to `AspireWebAppTemplate.Infrastructure.Extensions`
    - Update all `using` statements within the file to reference new namespaces
    - _Requirements: 3.14_

  - [x] 7.10 Update using statements across solution for Infrastructure types
    - In ApiService controllers and Program.cs: replace `using AspireWebAppTemplate.ApiService.Data` with `using AspireWebAppTemplate.Infrastructure.Data`
    - Replace `using AspireWebAppTemplate.ApiService.Data.Entities` with appropriate Infrastructure namespaces
    - Replace `using AspireWebAppTemplate.ApiService.Services` with `using AspireWebAppTemplate.Infrastructure.Services`
    - In Tests project: update all using statements referencing moved types
    - _Requirements: 11.3_

- [x] 8. Checkpoint - Verify solution compiles with Infrastructure populated
  - Run `dotnet build` and verify zero errors

- [x] 9. Update ApiService composition root and slim project
  - [x] 9.1 Update Program.cs
    - Change `AddApplicationServices()` call to `AddInfrastructureServices()`
    - Add `using AspireWebAppTemplate.Infrastructure.Extensions`
    - Add `MigrationsAssembly("AspireWebAppTemplate.Infrastructure")` to the `UseSqlServer` configuration
    - Remove old `using` statements for moved namespaces
    - _Requirements: 4.4, 9.1_

  - [x] 9.2 Remove emptied directories from ApiService
    - Delete `ApiService/Abstractions/` directory (files moved to Application)
    - Delete `ApiService/Data/` directory (files moved to Infrastructure)
    - Delete `ApiService/Services/` directory (files moved to Infrastructure)
    - Delete `ApiService/Utilities/` directory (files moved to Infrastructure)
    - Delete `ApiService/Extensions/` directory (file moved to Infrastructure)
    - Verify only Controllers/, Authentication/, Program.cs, appsettings remain
    - _Requirements: 4.1, 4.3_

  - [x] 9.3 Finalize ApiService.csproj
    - Remove ProjectReference to Core (bridge no longer needed)
    - Verify remaining references: Application, Infrastructure, ServiceDefaults only
    - _Requirements: 4.2_

- [x] 10. Update Web project references
  - [x] 10.1 Update Web.csproj
    - Replace ProjectReference to Core with ProjectReference to Application
    - Verify no reference to Infrastructure or Domain (Web accesses Domain types transitively via Application)
    - Retain UI and ServiceDefaults references unchanged
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 10.2 Update Web using statements
    - Replace all `using AspireWebAppTemplate.Core.*` with corresponding `AspireWebAppTemplate.Application.*` or `AspireWebAppTemplate.Domain.*` namespaces throughout the Web project
    - _Requirements: 11.1, 11.2_

- [x] 11. Update Tests project references
  - [x] 11.1 Update Tests.csproj
    - Replace ProjectReference to Core with ProjectReferences to Application and Infrastructure
    - Keep existing references to ApiService, Web, AppHost, UI
    - _Requirements: 7.1, 7.2_

  - [x] 11.2 Update Tests using statements
    - Replace all `using AspireWebAppTemplate.Core.*` and old `using AspireWebAppTemplate.ApiService.Data.*` / `ApiService.Services.*` with corresponding new namespaces
    - _Requirements: 7.3, 11.1, 11.2, 11.3_

- [x] 12. Remove Core project from solution
  - [x] 12.1 Remove Core from solution and disk
    - Remove Core project entry from `AspireWebAppTemplate.slnx`
    - Delete the `AspireWebAppTemplate.Core/` directory entirely
    - _Requirements: 5.1, 5.2_

  - [x] 12.2 Verify no remaining Core references
    - Search all `.csproj` files for any ProjectReference to Core — should be zero
    - Search all `.cs` files for `using AspireWebAppTemplate.Core` — should be zero
    - _Requirements: 5.3, 11.4_

- [x] 13. Final checkpoint - Verify complete solution compilability
  - Run `dotnet build` at solution level — verify zero errors and no new warnings related to missing types
  - Run `dotnet test` in Tests project — verify all existing tests pass with only using statement changes
  - _Requirements: 10.1, 10.2, 10.3_

## Notes

- This is a structural reorganization — no business logic changes. Files move with namespace updates only.
- The bridge strategy (temporarily keeping old references while adding new ones) ensures compilability during transition.
- EF Core migrations require the `MigrationsAssembly` configuration change when DbContext moves to a different assembly.
- Infrastructure needs `<FrameworkReference Include="Microsoft.AspNetCore.App" />` because services use ASP.NET Core types (IHttpContextAccessor, ILogger, DelegatingHandler, etc.).
- The `dotnet-tools.json` and `Options/` directory in ApiService should be evaluated — if they contain EF Core tool config or options classes used by services, they may need to move to Infrastructure as well.
- After migration, `dotnet ef` commands must target the Infrastructure project: `dotnet ef migrations add <Name> --project AspireWebAppTemplate.Infrastructure --startup-project AspireWebAppTemplate.ApiService`

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["1.4"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "3.4"] },
    { "id": 3, "tasks": ["3.5"] },
    { "id": 4, "tasks": ["5.1", "5.2", "5.3", "5.4", "5.5"] },
    { "id": 5, "tasks": ["5.6"] },
    { "id": 6, "tasks": ["7.1", "7.2"] },
    { "id": 7, "tasks": ["7.3", "7.4", "7.5", "7.6"] },
    { "id": 8, "tasks": ["7.7", "7.8"] },
    { "id": 9, "tasks": ["7.9", "7.10"] },
    { "id": 10, "tasks": ["9.1", "9.2", "9.3", "10.1", "10.2", "11.1", "11.2"] },
    { "id": 11, "tasks": ["12.1"] },
    { "id": 12, "tasks": ["12.2"] }
  ]
}
```
