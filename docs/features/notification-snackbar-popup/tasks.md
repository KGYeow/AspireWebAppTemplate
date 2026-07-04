# Implementation Plan: Notification Snackbar Popup

## Overview

Replace the plain-text snackbar notification popup in `NotificationBell.ShowToast()` with a rich custom component using MudBlazor's `ISnackbar.Add<TComponent>()` API. The implementation creates a new `NotificationSnackbarContent` component in the UI project, a `SnackbarTextHelper` utility for testable truncation logic, and updates the existing `NotificationBell` to use the generic snackbar API with per-snackbar top-right positioning.

## Tasks

- [x] 1. Create SnackbarTextHelper utility class
  - [x] 1.1 Create `AspireWebAppTemplate.UI/Utilities/SnackbarTextHelper.cs`
    - Implement static `TruncateTitle(string)` method with 100-character limit and ellipsis append
    - Implement static `TruncateMessage(string)` method with 200-character limit and ellipsis append
    - Define `MaxTitleLength` and `MaxMessageLength` public constants
    - Handle null/empty input gracefully (return empty string)
    - Include full XML documentation comments
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [x] 1.2 Write property test for title truncation
    - **Property 1: Title truncation preserves content within limit**
    - **Validates: Requirements 7.1, 7.2**
    - Create `AspireWebAppTemplate.Tests/Notifications/SnackbarTitleTruncationPropertyTests.cs`
    - Use `[Property(MaxTest = 2)]` per project convention
    - Tag: `// Feature: notification-snackbar-popup, Property 1: Title truncation preserves content within limit`
    - Test: for any string, output length never exceeds 101 characters; strings ≤100 chars returned unchanged; strings >100 chars return first 100 + "…"

  - [x] 1.3 Write property test for message truncation
    - **Property 2: Message truncation preserves content within limit**
    - **Validates: Requirements 7.3, 7.4**
    - Create `AspireWebAppTemplate.Tests/Notifications/SnackbarMessageTruncationPropertyTests.cs`
    - Use `[Property(MaxTest = 2)]` per project convention
    - Tag: `// Feature: notification-snackbar-popup, Property 2: Message truncation preserves content within limit`
    - Test: for any string, output length never exceeds 201 characters; strings ≤200 chars returned unchanged; strings >200 chars return first 200 + "…"

- [x] 2. Create NotificationSnackbarContent component
  - [x] 2.1 Create `AspireWebAppTemplate.UI/Components/Shared/NotificationSnackbarContent.razor`
    - Implement the Razor template with `MudStack Row`, `MudAvatar`, `MudIcon`, and two `MudText` elements
    - Add `@onclick="HandleClick"` on the container div with `cursor: pointer` style
    - Apply CSS text overflow with `white-space: nowrap; overflow: hidden; text-overflow: ellipsis` on text elements
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 5.1, 5.2, 8.1, 8.2_

  - [x] 2.2 Create `AspireWebAppTemplate.UI/Components/Shared/NotificationSnackbarContent.razor.cs`
    - Implement the code-behind partial class extending `ComponentBase`
    - Inject `NavigationManager` and `ISnackbar` via `[Inject]` attribute
    - Define parameters: `Title`, `Message`, `Category`, `NavigateUrl` (all string)
    - Add `[CascadingParameter]` for `MudSnackbarElement` to access snackbar key
    - Implement computed properties: `DisplayTitle`, `DisplayMessage`, `CategoryIcon`, `CategoryColor`
    - Use `SnackbarTextHelper.TruncateTitle()` and `SnackbarTextHelper.TruncateMessage()` for display
    - Implement `GetCategoryIcon()` switch: account→Security, activity→People, system→Info, default→Notifications
    - Implement `GetCategoryColor()` switch: account→Error, activity→Primary, system→Info, default→Default
    - Implement `HandleClick()`: remove snackbar via key, navigate to `NavigateUrl`
    - Organize with `#region` blocks: Injected Services, Parameters, Computed Properties, Event Handlers, Helpers
    - Include full XML documentation comments
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 5.1, 5.2, 7.1, 7.2, 7.3, 7.4_

  - [x] 2.3 Write property test for unknown category fallback
    - **Property 4: Unknown category fallback to default icon**
    - **Validates: Requirements 3.4**
    - Create `AspireWebAppTemplate.Tests/Notifications/SnackbarCategoryFallbackPropertyTests.cs`
    - Use `[Property(MaxTest = 2)]` per project convention
    - Tag: `// Feature: notification-snackbar-popup, Property 4: Unknown category fallback to default icon`
    - Test: for any category string not case-insensitively matching "account", "activity", or "system", the component returns `Icons.Material.Outlined.Notifications` and `Color.Default`

- [x] 3. Update NotificationBell to use custom snackbar component
  - [x] 3.1 Modify `AspireWebAppTemplate.Web/Components/Layout/Topbar/NotificationBell.razor.cs`
    - Replace the existing `ShowToast` method body with `Snackbar.Add<NotificationSnackbarContent>()`
    - Pass `Title`, `Message`, `Category`, and `NavigateUrl = "/account/notifications"` as component parameters
    - Configure per-snackbar options: `VisibleStateDuration = 5000`, `ShowCloseIcon = true`, `SnackbarVariant = Variant.Outlined`, `HideIcon = true`, `PositionClass = Defaults.Classes.Position.TopRight`
    - Keep the `NotificationPopupsEnabled` guard check at the top of the method
    - Remove the old `TruncateTitle` static method (superseded by `SnackbarTextHelper`)
    - Add `using AspireWebAppTemplate.UI.Components.Shared` if not already present
    - Update XML documentation on `ShowToast` to describe the new behavior
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 4.1, 4.2, 6.1, 6.2, 6.3, 8.1, 8.3, 9.1, 9.2, 9.3_

  - [x] 3.2 Write property test for popup suppression
    - **Property 3: Popup suppression respects preference**
    - **Validates: Requirements 6.1, 6.2, 6.3**
    - Create `AspireWebAppTemplate.Tests/Notifications/SnackbarPopupSuppressionPropertyTests.cs`
    - Use `[Property(MaxTest = 2)]` per project convention
    - Tag: `// Feature: notification-snackbar-popup, Property 3: Popup suppression respects preference`
    - Test: for any notification (title, message, category), snackbar is displayed if and only if `NotificationPopupsEnabled` is true

- [x] 4. Checkpoint - Verify integration
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Write unit tests for NotificationSnackbarContent
  - [x] 5.1 Create `AspireWebAppTemplate.Tests/Notifications/NotificationSnackbarContentTests.cs`
    - Test category icon mapping: Account→Security icon + Error color
    - Test category icon mapping: Activity→People icon + Primary color
    - Test category icon mapping: System→Info icon + Info color
    - Test HandleClick navigates to provided NavigateUrl
    - Test HandleClick with empty NavigateUrl skips navigation
    - Test null/empty Title and Message render without error
    - _Requirements: 3.1, 3.2, 3.3, 5.1, 5.2_

- [x] 6. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The existing `TitleTruncationPropertyTests.cs` may need updating or removal once `SnackbarTextHelper` replaces the old `NotificationBell.TruncateTitle()` method
- The `MudSnackbarProvider` in `MainLayout.razor` is NOT modified — positioning is per-snackbar only

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "2.1", "2.2"] },
    { "id": 2, "tasks": ["2.3", "3.1"] },
    { "id": 3, "tasks": ["3.2"] },
    { "id": 4, "tasks": ["5.1"] }
  ]
}
```
