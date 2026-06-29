# Implementation Plan: StatusAlert Component

## Overview

Implement a reusable `StatusAlert` Blazor component in the shared UI library (`AspireWebAppTemplate.UI`) that wraps MudBlazor's `MudAlert` with self-hiding behavior, consistent styling, dismissible support, dense mode, rich content, and two-way binding. The component uses the partial class pattern (`.razor` + `.razor.cs`) consistent with existing shared components (LoadingOverlay, PageHeader, PageContent). Property-based tests validate correctness properties using FsCheck.Xunit.

## Tasks

- [x] 1. Create the StatusAlert component files
  - [x] 1.1 Create the StatusAlert code-behind file (`AspireWebAppTemplate.UI/Components/Shared/StatusAlert.razor.cs`)
    - Define partial class inheriting `ComponentBase` in namespace `AspireWebAppTemplate.UI.Components.Shared`
    - Add all parameters: `Message` (string?), `MessageChanged` (EventCallback<string?>), `Severity` (Severity, default Error), `Dismissible` (bool, default true), `Dense` (bool, default false), `Class` (string?), `ChildContent` (RenderFragment?)
    - Add a computed property or method for CSS class composition: always include `border-1`, include `mb-4` when Dense is false, append consumer `Class` when non-null
    - Add XML documentation comments on the class and all parameters
    - _Requirements: 1.1, 1.2, 2.1, 2.2, 3.4, 4.1, 4.2, 4.3, 4.4, 5.3, 7.1, 7.2, 8.1, 8.2, 8.3_

  - [x] 1.2 Create the StatusAlert Razor template file (`AspireWebAppTemplate.UI/Components/Shared/StatusAlert.razor`)
    - Gate rendering on `!string.IsNullOrEmpty(Message)` — render nothing when Message is null or empty
    - Render `MudAlert` with: `Severity`, `Dense`, `ShowCloseIcon=Dismissible`, computed CSS class string
    - Wire `CloseIconClicked` to invoke `MessageChanged` with null
    - Render `ChildContent` inside MudAlert when provided, otherwise render `@Message` text
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 2.3, 3.1, 3.2, 3.3, 5.1, 5.2, 6.1, 6.2, 6.3, 7.3_

- [x] 2. Checkpoint - Verify component compiles
  - Ensure the solution builds without errors, ask the user if questions arise.

- [x] 3. Write property-based tests for StatusAlert
  - [x] 3.1 Create test file `AspireWebAppTemplate.Tests/StatusAlert/Properties/StatusAlertPropertyTests.cs`
    - Set up FsCheck generators for Severity (Error, Success, Info, Warning), bool (Dense, Dismissible), string? (null, empty, non-empty samples), and string? Class (null, sample CSS strings)
    - _Requirements: 1.1, 1.2, 2.3, 3.1, 3.3, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2_

  - [x] 3.2 Write property test for Self-Hiding Invariant
    - **Property 1: Self-Hiding Invariant**
    - For any combination of parameters, when Message is null or empty string, the component produces no rendered output
    - Use `[Property(MaxTest = 2)]` per project convention
    - **Validates: Requirements 1.1, 1.2, 1.5**

  - [x] 3.3 Write property test for Parameter Pass-Through
    - **Property 2: Parameter Pass-Through**
    - For any non-empty Message and valid parameter combinations, the MudAlert receives the exact Severity, Dense, and ShowCloseIcon values provided
    - Use `[Property(MaxTest = 2)]` per project convention
    - **Validates: Requirements 2.3, 3.1, 3.3, 5.1, 5.2**

  - [x] 3.4 Write property test for CSS Class Composition
    - **Property 3: CSS Class Composition**
    - For any non-empty Message, the computed CSS always contains `border-1`, contains `mb-4` iff Dense is false, and contains consumer Class when non-null
    - Use `[Property(MaxTest = 2)]` per project convention
    - **Validates: Requirements 4.1, 4.2, 4.3, 4.4**

  - [x] 3.5 Write property test for Message Content Rendering
    - **Property 4: Message Content Rendering**
    - For any non-empty Message when ChildContent is not provided, the rendered alert body contains the exact Message text
    - Use `[Property(MaxTest = 2)]` per project convention
    - **Validates: Requirements 1.3, 6.2**

- [x] 4. Write unit tests for StatusAlert
  - [x] 4.1 Create unit test file `AspireWebAppTemplate.Tests/StatusAlert/StatusAlertUnitTests.cs`
    - Test default parameter values (Severity=Error, Dismissible=true, Dense=false)
    - Test ChildContent takes precedence over Message text (requirement 6.3)
    - Test close icon click invokes MessageChanged with null (requirement 3.2)
    - Test Message changing from non-empty to null removes the alert (requirement 1.4)
    - Test ChildContent provided with non-empty Message renders ChildContent (requirement 1.6)
    - _Requirements: 1.4, 1.6, 2.2, 3.2, 3.4, 5.3, 6.3, 7.3_

- [x] 5. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties using FsCheck.Xunit with `[Property(MaxTest = 2)]`
- Unit tests validate specific examples, edge cases, and interaction behavior
- The component follows the same partial class pattern as existing shared components (LoadingOverlay, PageHeader, PageContent)
- Test files go in `AspireWebAppTemplate.Tests/StatusAlert/` following the feature-based organization convention

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["3.1"] },
    { "id": 3, "tasks": ["3.2", "3.3", "3.4", "3.5", "4.1"] }
  ]
}
```
