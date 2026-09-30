# Requirements Document

## Introduction

This document specifies the requirements for migrating AspireWebAppTemplate from its current 7-project solution structure (AppHost, ServiceDefaults, Core, ApiService, Web, UI, Tests) to a 9-project Clean Architecture layout (AppHost, ServiceDefaults, Domain, Application, Infrastructure, ApiService, Web, UI, Tests). The migration is a structural reorganization — no business logic changes. The Core project is removed and replaced by Domain + Application. Most of ApiService's internals move to Infrastructure. ApiService becomes a thin HTTP host.

## Glossary

- **Solution**: The Visual Studio .sln file and its constituent projects
- **Domain_Project**: The new `AspireWebAppTemplate.Domain` class library containing enums, constants, attributes, value objects, and identity-free entities
- **Application_Project**: The new `AspireWebAppTemplate.Application` class library containing service interfaces, DTOs/Contracts, shared models, and extension methods
- **Infrastructure_Project**: The new `AspireWebAppTemplate.Infrastructure` class library containing EF Core entities, DbContext, configurations, migrations, seed data, service implementations, handlers, clients, and utilities
- **ApiService_Project**: The existing `AspireWebAppTemplate.ApiService` web project, slimmed to retain only Controllers, authentication handlers, and Program.cs
- **Core_Project**: The existing `AspireWebAppTemplate.Core` class library that will be removed after its contents are distributed
- **Web_Project**: The existing `AspireWebAppTemplate.Web` Blazor Server project
- **Tests_Project**: The existing `AspireWebAppTemplate.Tests` project
- **Identity_Entity**: An entity type that references `ApplicationUser` or `ApplicationRole` via foreign key or navigation property
- **Identity_Free_Entity**: An entity type with zero foreign keys or navigation properties to Identity types
- **Composition_Root**: The `Program.cs` file in ApiService_Project that wires DI registrations and middleware

## Requirements

### Requirement 1: Create Domain Project

**User Story:** As a developer, I want a Domain project that contains only framework-agnostic business vocabulary, so that the innermost layer has zero external dependencies and maximum stability.

#### Acceptance Criteria

1. THE Domain_Project SHALL be a .NET class library targeting net10.0 with zero NuGet package references and zero project references
2. WHEN the Domain_Project is created, THE Solution SHALL include the project with the name `AspireWebAppTemplate.Domain`
3. THE Domain_Project SHALL contain all enum types currently in `Core/Domain/Enums/` under a `Domain/Enums/` folder structure
4. THE Domain_Project SHALL contain all constant classes currently in `Core/Common/Defaults/` under a `Constants/` folder
5. THE Domain_Project SHALL contain all custom attribute classes currently in `Core/Utilities/Attributes/` under an `Attributes/` folder
6. THE Domain_Project SHALL contain a `ValueObjects/` folder for future value object types
7. THE Domain_Project SHALL contain all Identity_Free_Entity types (specifically `EmailTemplate`) under an `Entities/` folder
8. THE Domain_Project SHALL use the root namespace `AspireWebAppTemplate.Domain`

### Requirement 2: Create Application Project

**User Story:** As a developer, I want an Application project that defines use-case contracts and shared models, so that both the API backend and Web frontend can share a stable contract surface without depending on framework implementations.

#### Acceptance Criteria

1. THE Application_Project SHALL be a .NET class library targeting net10.0 with a single project reference to Domain_Project and zero NuGet package references
2. WHEN the Application_Project is created, THE Solution SHALL include the project with the name `AspireWebAppTemplate.Application`
3. THE Application_Project SHALL contain all service interfaces currently in `Core/Application/Abstractions/` and `ApiService/Abstractions/` under an `Abstractions/` folder
4. THE Application_Project SHALL contain all DTO and contract types currently in `Core/Contracts/` under a `Contracts/` folder preserving sub-folder structure by feature
5. THE Application_Project SHALL contain shared result types (`ApiResult`, `PagedResult`) and navigation models (`NavItem`) under a `Common/` folder
6. THE Application_Project SHALL contain extension methods currently in `Core/Extensions/` under an `Extensions/` folder
7. THE Application_Project SHALL use the root namespace `AspireWebAppTemplate.Application`

### Requirement 3: Create Infrastructure Project

**User Story:** As a developer, I want an Infrastructure project that contains all framework integrations (EF Core, Identity, SMTP, LDAP, AWS), so that implementation details are isolated in a single outer-layer project.

#### Acceptance Criteria

1. THE Infrastructure_Project SHALL be a .NET class library targeting net10.0 with a project reference to Application_Project and a `<FrameworkReference Include="Microsoft.AspNetCore.App" />` element
2. WHEN the Infrastructure_Project is created, THE Solution SHALL include the project with the name `AspireWebAppTemplate.Infrastructure`
3. THE Infrastructure_Project SHALL contain `ApplicationDbContext` under a `Data/` folder
4. THE Infrastructure_Project SHALL contain all Identity-referencing entity types (`Notification`, `Announcement`, `AuditLogEntry`, `PagePermission`, `NotificationPreference`, `AnnouncementDismissal`) under `Data/Entities/`
5. THE Infrastructure_Project SHALL contain `ApplicationUser` and `ApplicationRole` under `Identity/`
6. THE Infrastructure_Project SHALL contain all EF Core configuration classes under `Data/Configurations/`
7. THE Infrastructure_Project SHALL contain all EF Core migration files under `Data/Migrations/`
8. THE Infrastructure_Project SHALL contain all seed data files under `Data/SeedData/`
9. THE Infrastructure_Project SHALL contain all service implementations currently in `ApiService/Services/` under a `Services/` folder
10. THE Infrastructure_Project SHALL contain `CurrentUserAccessor` under `Utilities/`
11. THE Infrastructure_Project SHALL contain `WebCallbackClient` under `Clients/`
12. THE Infrastructure_Project SHALL contain `InternalApiKeyDelegatingHandler` under `Handlers/`
13. THE Infrastructure_Project SHALL contain `AuditChangeHelper` and `SecureConnectionString` under `Utilities/`
14. THE Infrastructure_Project SHALL expose a DI registration extension method `AddInfrastructureServices(this IServiceCollection)` under `Extensions/`
15. THE Infrastructure_Project SHALL contain all NuGet packages required for its implementations (EF Core, Identity, SQL Server, LDAP, AWS SDK, EPPlus, HtmlSanitizer)
16. THE Infrastructure_Project SHALL use the root namespace `AspireWebAppTemplate.Infrastructure`

### Requirement 4: Slim ApiService Project

**User Story:** As a developer, I want ApiService to be a thin HTTP host with only controllers and composition-root wiring, so that it contains no business logic or data access code directly.

#### Acceptance Criteria

1. WHEN the migration is complete, THE ApiService_Project SHALL contain only Controllers, BaseController, `Program.cs`, authentication handlers (`InternalAuthenticationHandler`), and appsettings files
2. WHEN the migration is complete, THE ApiService_Project SHALL have project references to Application_Project, Infrastructure_Project, and ServiceDefaults only
3. WHEN the migration is complete, THE ApiService_Project SHALL NOT contain service implementations, entity types, DbContext, EF configurations, migrations, seed data, or utility classes
4. THE ApiService_Project Composition_Root SHALL call `AddInfrastructureServices()` instead of `AddApplicationServices()` for DI registration
5. THE ApiService_Project SHALL NOT have direct NuGet references to EF Core, Identity, LDAP, AWS SDK, EPPlus, or HtmlSanitizer packages

### Requirement 5: Remove Core Project

**User Story:** As a developer, I want the Core project removed from the solution, so that there is no ambiguity about where shared types belong in the Clean Architecture structure.

#### Acceptance Criteria

1. WHEN all contents of Core_Project have been distributed to Domain_Project and Application_Project, THE Solution SHALL remove Core_Project from the .sln file
2. WHEN Core_Project is removed, THE Solution SHALL delete the `AspireWebAppTemplate.Core/` directory from disk
3. WHEN Core_Project is removed, THE Solution SHALL contain zero project references pointing to `AspireWebAppTemplate.Core`

### Requirement 6: Update Web Project References

**User Story:** As a developer, I want the Web project to reference Application instead of Core, so that it follows the Clean Architecture dependency direction.

#### Acceptance Criteria

1. WHEN the migration is complete, THE Web_Project SHALL have a project reference to Application_Project replacing its former reference to Core_Project
2. WHEN the migration is complete, THE Web_Project SHALL NOT have project references to Infrastructure_Project or Domain_Project directly
3. THE Web_Project SHALL retain its existing project references to UI and ServiceDefaults unchanged

### Requirement 7: Update Tests Project References

**User Story:** As a developer, I want the Tests project to reference the new projects correctly, so that all test code compiles against the reorganized structure.

#### Acceptance Criteria

1. WHEN the migration is complete, THE Tests_Project SHALL have project references to Application_Project, Infrastructure_Project, ApiService_Project, Web_Project, and AppHost
2. WHEN the migration is complete, THE Tests_Project SHALL NOT have a project reference to Core_Project
3. WHEN the migration is complete, THE Tests_Project SHALL compile successfully with updated `using` statements reflecting new namespaces

### Requirement 8: Enforce Dependency Direction

**User Story:** As a developer, I want the project references to enforce the Clean Architecture dependency rule at compile time, so that inner layers cannot depend on outer layers.

#### Acceptance Criteria

1. THE Domain_Project SHALL have zero project references (depends on nothing)
2. THE Application_Project SHALL reference only Domain_Project
3. THE Infrastructure_Project SHALL reference only Application_Project (transitively accessing Domain)
4. THE ApiService_Project SHALL reference Application_Project, Infrastructure_Project, and ServiceDefaults
5. THE Web_Project SHALL reference Application_Project, UI, and ServiceDefaults
6. IF any project attempts to add a reference violating the dependency direction, THEN THE compiler SHALL produce an error due to missing project reference

### Requirement 9: Maintain EF Core Migration Compatibility

**User Story:** As a developer, I want EF Core migrations to continue working after DbContext moves to Infrastructure, so that database schema management is uninterrupted.

#### Acceptance Criteria

1. WHEN ApplicationDbContext moves to Infrastructure_Project, THE EF Core configuration SHALL specify `MigrationsAssembly("AspireWebAppTemplate.Infrastructure")` in the `UseSqlServer` call
2. THE Infrastructure_Project SHALL contain all existing migration files with updated namespaces
3. WHEN `dotnet ef migrations add` is run targeting Infrastructure_Project, THE tool SHALL generate new migrations correctly

### Requirement 10: Maintain Build Compilability

**User Story:** As a developer, I want the solution to compile successfully after the migration, so that no functionality is broken by the structural reorganization.

#### Acceptance Criteria

1. WHEN the migration is complete, THE Solution SHALL compile with zero errors using `dotnet build`
2. WHEN the migration is complete, THE Solution SHALL produce no new compiler warnings related to missing types or namespace resolution
3. WHEN the migration is complete, THE Tests_Project SHALL pass all existing tests without logic changes (only `using` statement updates)

### Requirement 11: Update Namespace References

**User Story:** As a developer, I want all namespace references updated consistently across the solution, so that no dangling references to removed projects exist.

#### Acceptance Criteria

1. WHEN types move from Core_Project to Domain_Project, THE Solution SHALL update all `using AspireWebAppTemplate.Core.*` references to `using AspireWebAppTemplate.Domain.*` where types moved to Domain
2. WHEN types move from Core_Project to Application_Project, THE Solution SHALL update all `using AspireWebAppTemplate.Core.*` references to `using AspireWebAppTemplate.Application.*` where types moved to Application
3. WHEN types move from ApiService_Project to Infrastructure_Project, THE Solution SHALL update all `using AspireWebAppTemplate.ApiService.*` references to `using AspireWebAppTemplate.Infrastructure.*` where types moved to Infrastructure
4. WHEN the migration is complete, THE Solution SHALL contain zero references to the `AspireWebAppTemplate.Core` namespace

### Requirement 12: Update Solution File

**User Story:** As a developer, I want the .sln file to reflect the new project structure, so that the solution opens correctly in Visual Studio and builds via `dotnet build` at the solution level.

#### Acceptance Criteria

1. WHEN new projects are created, THE Solution file SHALL include entries for Domain_Project, Application_Project, and Infrastructure_Project
2. WHEN Core_Project is removed, THE Solution file SHALL not contain an entry for Core_Project
3. THE Solution file SHALL organize projects into appropriate solution folders if existing projects use solution folders
