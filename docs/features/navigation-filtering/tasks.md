# Implementation Plan: API Navigation Filtering

## Overview

Move the navigation filtering pipeline from the Blazor Server frontend (`NavMenu.ComputeVisibleNavItems`) to the backend API (`NavigationService`). Implement a new `GET /api/navigation` endpoint that returns a pre-filtered navigation tree, a typed HttpClient to consume it, and simplify `NavMenu` to a pure renderer. Property-based tests verify filtering equivalence between old and new implementations.

## Tasks

- [x] 1. Create API service interface and implementation
  - [x] 1.1 Create INavigationService interface
    - Create `ApiService/Abstractions/INavigationService.cs` with `GetFilteredNavigationAsync()` method
    - Include full XML documentation describing the filtering pipeline (auth filter → permission filter → group visibility → orphan decoration removal)
    - _Requirements: 1.1, 1.4, 7.1_

  - [x] 1.2 Implement NavigationService with full filtering pipeline
    - Create `ApiService/Services/NavigationService.cs` implementing `INavigationService`
    - Inject `INavigationProvider`, `IPagePermissionService`, `ICurrentUserAccessor`
    - Implement `GetFilteredNavigationAsync`: get full nav tree, resolve auth state, load permitted paths
    - Implement `FilterByAccessibility`: apply auth truth table (AuthorizedOnly/NotAuthorizedOnly × authenticated/unauthenticated) and permission filtering (null Href always visible, System_Pages bypass, whitelist check)
    - Implement `NormalizePath`: prepend `/` if missing, strip trailing `/`, handle empty string → `/`
    - Implement `RemoveOrphanedDecorations`: remove Headers without following content, Dividers without content on both sides, apply at each tree level
    - Implement group visibility: recursively filter children, exclude groups with zero content children
    - Use `StringComparer.OrdinalIgnoreCase` for all path comparisons
    - Use `#region` grouping: Constructor, Filtering Pipeline, Private Helpers
    - _Requirements: 1.1, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 5.4, 5.5, 7.1, 7.2, 7.3, 7.4, 8.1, 8.2, 8.3, 8.4, 8.5_

  - [x] 1.3 Register NavigationService in API Program.cs
    - Add `builder.Services.AddScoped<INavigationService, NavigationService>()` to `Program.cs`
    - _Requirements: 1.1_

- [x] 2. Create NavigationController endpoint
  - [x] 2.1 Create NavigationController with GET /api/navigation
    - Create `ApiService/Controllers/NavigationController.cs` extending `BaseController`
    - Route: `[Route("api/navigation")]`, `[Authorize]` at class level
    - Single `[HttpGet]` endpoint delegating to `INavigationService.GetFilteredNavigationAsync()`
    - Return `Ok(result)` — thin controller with no business logic
    - Include `[ProducesResponseType]` attributes for 200 and 401
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.6_

- [x] 3. Implement Web project API client
  - [x] 3.1 Create ApiNavigationService typed HttpClient
    - Create `Web/Services/ApiClients/ApiNavigationService.cs` with primary constructor accepting `HttpClient`
    - Implement `GetFilteredNavigationAsync()` returning `ApiResult<List<NavItem>>`
    - On HTTP 200: deserialize JSON array and return `ApiResult.Success(data)`
    - On any error (401, 5xx, network, deserialization): return `ApiResult.Failure(errorMessage)`
    - _Requirements: 6.1, 6.4, 6.5_

  - [x] 3.2 Register ApiNavigationService HttpClient in Web Program.cs
    - Register typed HttpClient with Aspire service discovery base address (`"https+http://apiservice"`)
    - _Requirements: 6.1, 6.5_

- [x] 4. Simplify NavMenu to a pure renderer
  - [x] 4.1 Refactor NavMenu to consume API-filtered tree
    - Modify `Web/Components/Layout/Sidebar/NavMenu.razor.cs`:
      - Remove `ComputeVisibleNavItems`, `FilterByAccessibility`, `RemoveOrphanedDecorations`, `IsPageAccessible`, `IsAuthVisible` and all helper filtering methods
      - Inject `ApiNavigationService` via `[Inject]`
      - Add loading state: call `ApiNavigationService.GetFilteredNavigationAsync()` in `OnInitializedAsync`
      - Store result in a `List<NavItem>` field, render directly
      - On failure: render empty navigation (zero items)
    - Modify `Web/Components/Layout/Sidebar/NavMenu.razor`:
      - Remove `PagePermissionContext.IsLoaded` check
      - Add loading skeleton (5 `MudSkeleton` elements) while API call is in-flight
      - Render the received tree directly without any filtering
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [x] 5. Checkpoint - Ensure application builds and runs correctly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Write property-based tests for filtering pipeline
  - [x] 6.1 Create NavItem generators for property tests
    - Create `Tests/Navigation/Generators/NavItemGenerators.cs`
    - Implement `GenNavItem(maxDepth)`: generates random NavItem of any type with recursive children for Groups
    - Implement `GenNavTree(maxDepth, maxWidth)`: generates a list of NavItems forming a valid tree (up to 5 levels deep, up to 50 items per level)
    - Implement `GenPermissionSet()`: generates a random subset of known page paths
    - Implement `GenAuthState()`: generates authenticated/unauthenticated boolean
    - Implement `GenHref()`: generates hrefs with normalization edge cases (null, empty, leading slash, trailing slash, mixed case)
    - _Requirements: 7.1_

  - [x] 6.2 Write property test for filtering pipeline equivalence
    - **Property 1: Filtering Pipeline Equivalence**
    - **Validates: Requirements 7.1, 7.2, 7.3, 7.4**
    - Create `Tests/Navigation/Properties/NavigationFilteringEquivalenceTests.cs`
    - Generate random nav trees + auth states + permission sets
    - Compare `NavigationService` output to `NavMenu.ComputeVisibleNavItems` output (structural equality: item count, property values, ordering, recursive children)
    - Tag: `// Feature: api-nav-filtering, Property 1: Filtering Pipeline Equivalence`

  - [x] 6.3 Write property test for auth filtering truth table
    - **Property 2: Auth Filtering Truth Table**
    - **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6**
    - Create `Tests/Navigation/Properties/NavigationAuthFilterPropertyTests.cs`
    - Generate items with all AuthorizedOnly/NotAuthorizedOnly flag combinations × both auth states
    - Verify truth table: (true,false)→authenticated only; (false,true)→unauthenticated only; (false,false)→always visible; (true,true)→never visible
    - Tag: `// Feature: api-nav-filtering, Property 2: Auth Filtering Truth Table`

  - [x] 6.4 Write property test for permission filtering correctness
    - **Property 3: Permission Filtering Correctness**
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
    - Create `Tests/Navigation/Properties/NavigationPermissionFilterPropertyTests.cs`
    - Generate link items with random hrefs + permission sets
    - Verify: null Href → always included; System_Page → always included; in permission set → included; otherwise → excluded
    - Tag: `// Feature: api-nav-filtering, Property 3: Permission Filtering Correctness`

  - [x] 6.5 Write property test for group visibility by content children
    - **Property 4: Group Visibility By Content Children**
    - **Validates: Requirements 1.5, 4.1, 4.2, 4.3**
    - Create `Tests/Navigation/Properties/NavigationGroupVisibilityPropertyTests.cs`
    - Generate nested group structures
    - Verify bottom-up evaluation: group included iff at least one content child (Link or nested Group) passes; empty nested groups don't count as visible
    - Tag: `// Feature: api-nav-filtering, Property 4: Group Visibility By Content Children`

  - [x] 6.6 Write property test for orphan decoration removal
    - **Property 5: Orphan Decoration Removal**
    - **Validates: Requirements 4.4, 5.1, 5.2, 5.3, 5.4, 5.5**
    - Create `Tests/Navigation/Properties/NavigationDecorationPropertyTests.cs`
    - Generate item sequences with mixed types at various tree levels
    - Verify: Header included only if followed by content before next Header/end; Divider included only if preceded and followed by content
    - Tag: `// Feature: api-nav-filtering, Property 5: Orphan Decoration Removal`

  - [x] 6.7 Write property test for path normalization idempotence and correctness
    - **Property 6: Path Normalization Idempotence and Correctness**
    - **Validates: Requirements 8.1, 8.2, 8.4, 8.5**
    - Create `Tests/Navigation/Properties/NavigationPathNormalizationPropertyTests.cs`
    - Generate hrefs with/without leading slashes, trailing slashes, varying case
    - Verify: prepends `/` if missing, strips trailing `/`, OrdinalIgnoreCase comparison matches equivalent paths
    - Tag: `// Feature: api-nav-filtering, Property 6: Path Normalization Idempotence and Correctness`

- [x] 7. Write unit tests for controller and client
  - [x] 7.1 Write unit tests for NavigationController and ApiNavigationService
    - Create `Tests/Navigation/NavigationServiceUnitTests.cs`
    - Test NavigationController returns 200 with service result (mock INavigationService)
    - Test ApiNavigationService deserializes successful response correctly
    - Test ApiNavigationService returns failure on HTTP error responses
    - Test NavMenu renders items without filtering (component test or verify no filter calls)
    - Test NavMenu shows skeleton while loading
    - _Requirements: 1.1, 1.2, 6.1, 6.2, 6.3, 6.4_

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The thin controller pattern means the NavigationController has zero business logic — it only delegates to NavigationService
- The existing `NavMenu.ComputeVisibleNavItems` code should be preserved (not deleted) until Property 1 (equivalence) passes, confirming identical behavior
- All path comparisons use `StringComparer.OrdinalIgnoreCase` per existing project conventions

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["1.3", "2.1"] },
    { "id": 3, "tasks": ["3.1"] },
    { "id": 4, "tasks": ["3.2", "4.1"] },
    { "id": 5, "tasks": ["6.1"] },
    { "id": 6, "tasks": ["6.2", "6.3", "6.4", "6.5", "6.6", "6.7", "7.1"] }
  ]
}
```
