# Implementation Plan: User Profile

## Overview

Complete implementation of the User Profile page with view/edit mode, timezone auto-detection, avatar display, DropdownProfile navigation, and subsequent refinements including typography fixes, label consistency, phone validation improvements, and label visual hierarchy styling.

## Completed Tasks

### Phase 1: Core Profile Page Implementation

- [x] 1. Create core service interfaces and implementations
  - [x] 1.1 Create ITimeZoneDisplayService interface and TimeZoneOption record
  - [x] 1.2 Create TimeZoneDisplayService implementation with full XML documentation
  - [x] 1.3 Register TimeZoneDisplayService in DI container

- [x] 2. Create Profile page with view/edit mode
  - [x] 2.1 Create `Components/Pages/Profile/Index.razor` with cover banner and avatar
  - [x] 2.2 Implement View Mode with MudPaper sections, MudInputLabel labels, and MudText values
  - [x] 2.3 Implement Edit Mode with MudInputLabel above MudTextField inputs
  - [x] 2.4 Implement code-behind (`Index.razor.cs`) with state, lifecycle, and event handlers
  - [x] 2.5 Implement EnterEditMode, CancelEdit, and OnValidSubmitAsync
  - [x] 2.6 Implement LDAP field disabling and helper text
  - [x] 2.7 Implement avatar display with fallback character
  - [x] 2.8 Implement timezone search (MudAutocomplete with SearchTimeZones/TimeZoneToString)
  - [x] 2.9 Implement form submission and save logic

- [x] 3. Update DropdownProfile component
  - [x] 3.1 Add Profile link and rename Manage Account to Settings

- [x] 4. Implement timezone auto-detection in MainLayout
  - [x] 4.1 Create `wwwroot/js/timezone.js` with getBrowserTimeZone function
  - [x] 4.2 Add OnAfterRenderAsync logic to MainLayout.razor.cs for auto-detection

- [x] 5. Write property-based tests
  - [x] 5.1 Property: Profile save round-trip
  - [x] 5.2 Property: Field editability rules based on AuthSource
  - [x] 5.3 Property: Timezone display format
  - [x] 5.4 Property: Timezone search filtering
  - [x] 5.5 Property: MaxLength validation rejects oversized input
  - [x] 5.6 Property: Fallback avatar character derivation
  - [x] 5.7 Property: Null field placeholder display
  - [x] 5.8 Property: Cancel discards modifications
  - [x] 5.9 Property: Timezone auto-save conditional on null TimeZoneId

### Phase 2: Post-Implementation Adjustments

- [x] 6. Combine dual profile headers into single LinkedIn-style design
  - [x] 6.1 Merge banner avatar and Header_Row into one section
  - [x] 6.2 Restructure to LinkedIn layout: banner → overlapping avatar (bottom-left) → name below avatar
  - [x] 6.3 Replace text Edit button with pencil icon button (MudIconButton) top-right of banner

- [x] 7. Remove timezone detection from Profile Page
  - [x] 7.1 Remove OnAfterRenderAsync timezone detection logic and IJSRuntime injection

- [x] 8. Remove Profile from sidebar navigation
  - [x] 8.1 Remove Profile NavItem from DefaultNavigationProvider

- [x] 9. Update DropdownProfile with divider and item ordering
  - [x] 9.1 Add divider before Log Out (Profile → Settings → Divider → Log Out)

- [x] 10. Apply rounded menu item styling to DropdownProfile
  - [x] 10.1 Add action-menu CSS class to PopoverClass for rounded menu items

- [x] 11. Fix content sections to span full page width
  - [x] 11.1 Remove MudGrid/MudItem xs="12" md="8" wrapper constraining content width

### Phase 3: Typography, Label, and Validation Bugfixes

- [x] 12. Fix View Mode typography and label consistency
  - [x] 12.1 Fix View Mode typography: change `Typo.body1` to `Typo.body2` on all field value MudText elements
  - [x] 12.2 Fix View Mode labels: replace `<MudText Typo="Typo.caption">` with `<MudInputLabel>` on all field labels
  - _Requirements: 15.1, 15.2, 15.3, 15.4_

- [x] 13. Fix phone validation (OptionalPhoneAttribute)
  - [x] 13.1 Create `OptionalPhoneAttribute` at `AspireWebAppTemplate.Core/Utilities/OptionalPhoneAttribute.cs`
    - Inherit from ValidationAttribute
    - Return true for null, empty, or whitespace values
    - Validate non-empty values against phone regex `^\+?[\d\s\-\(\)\.]+$`
  - [x] 13.2 Replace `[Phone]` with `[OptionalPhone]` on Profile page `InputModel.PhoneNumber`
  - [x] 13.3 Replace `[Phone]` with `[OptionalPhone]` on Account Manage page `InputModel.PhoneNumber`
  - _Requirements: 16.1, 16.2, 16.3, 16.4_

- [x] 14. Write phone validation property-based tests
  - [x] 14.1 Property: Phone number clearing allowed (null/empty/whitespace always valid)
  - [x] 14.2 Property: Valid phone character strings always accepted
  - [x] 14.3 Property: Strings with disallowed characters always rejected

### Phase 4: Label Visual Hierarchy Styling

- [x] 15. Apply fw-bold class to Profile page labels
  - [x] 15.1 Add `Class="fw-bold"` to all MudInputLabel elements in View Mode
  - [x] 15.2 Add `Class="fw-bold"` to all MudInputLabel elements in Edit Mode
  - _Requirements: 17.1, 17.2, 17.3, 17.4_

## Notes

- All tasks are completed
- Build MSB3027 errors during development were from running dev server locking DLLs — not code issues
- LinkedIn-style layout: cover banner → avatar overlapping bottom-left → name below → full-width sections
- FsCheck 3.1.0 with FsCheck.Xunit is used for property-based tests
- The `OptionalPhoneAttribute` is placed in `AspireWebAppTemplate.Core/Utilities/` following the existing pattern
- Preferences section was later extracted to a dedicated Settings page (see `docs/settings-page/`)
- `ProfileFormModel` was renamed to `InputModel` for consistency with all other pages
- `TimeZoneDisplayService` was renamed to `TimeZoneService` with additional IANA conversion support

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "2.4"] },
    { "id": 2, "tasks": ["2.5", "2.6", "2.7", "2.8", "2.9"] },
    { "id": 3, "tasks": ["3.1", "4.1", "4.2"] },
    { "id": 4, "tasks": ["5.1", "5.2", "5.3", "5.4", "5.5", "5.6", "5.7", "5.8", "5.9"] },
    { "id": 5, "tasks": ["6.1", "6.2", "6.3", "7.1", "8.1"] },
    { "id": 6, "tasks": ["9.1", "10.1", "11.1"] },
    { "id": 7, "tasks": ["12.1", "12.2", "13.1"] },
    { "id": 8, "tasks": ["13.2", "13.3", "14.1", "14.2", "14.3"] },
    { "id": 9, "tasks": ["15.1", "15.2"] }
  ]
}
```
