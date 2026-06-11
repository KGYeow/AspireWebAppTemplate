# Implementation Plan: Settings Page

## Overview

Complete implementation history of the Settings page feature across all phases: initial creation with Time Zone and Locale preferences, Date/Time Format and Theme property additions, theme switching with real-time pub/sub, and the final redesign removing View/Edit mode in favor of instant-save for all fields.

## Phase 1: Settings Page Creation

- [x] 1. Create Settings page structure and code-behind
  - [x] 1.1 Create `Components/Pages/Settings/Index.razor.cs` code-behind with state, services, and lifecycle
  - [x] 1.2 Implement Edit Mode event handlers in `Index.razor.cs`
  - [x] 1.3 Implement time zone search helpers with alias handling in `Index.razor.cs`

- [x] 2. Create Settings page Razor markup
  - [x] 2.1 Create `Components/Pages/Settings/Index.razor` with View Mode markup and page header
  - [x] 2.2 Add Edit Mode markup to `Components/Pages/Settings/Index.razor`
  - [x] 2.3 Add status message alert to `Components/Pages/Settings/Index.razor`

- [x] 3. Modify DropdownProfile and remove Preferences from Profile page
  - [x] 3.1 Update DropdownProfile menu: Profile → Settings → Divider → Log Out
  - [x] 3.2 Remove Preferences section from Profile page, rename `ProfileFormModel` to `InputModel`

- [x] 4. Rename TimeZoneDisplayService to TimeZoneService with IANA conversion
  - [x] 4.1 Rename `ITimeZoneDisplayService` → `ITimeZoneService`, `TimeZoneDisplayService` → `TimeZoneService`
  - [x] 4.2 Add `TryConvertWindowsIdToIanaId()` conversion in `BuildTimeZoneList()`
  - [x] 4.3 Add `.DistinctBy()` deduplication and update all references across codebase

- [x] 5. Create IUserTimeZoneContext scoped service
  - [x] 5.1 Create `Abstractions/IUserTimeZoneContext.cs` interface
  - [x] 5.2 Create `Services/UserTimeZoneContext.cs` implementation
  - [x] 5.3 Register as scoped in `Program.cs`
  - [x] 5.4 Initialize from `MainLayout.OnAfterRenderAsync` on first render

- [x] 6. Convert UTC datetime displays to user's time zone
  - [x] 6.1 Update `UserManagement/Details.razor.cs` to inject `IUserTimeZoneContext` and use `FormatDateTime`
  - [x] 6.2 Update `RoleManagement/Details.razor.cs` to inject `IUserTimeZoneContext` and use `FormatDateTime`
  - [x] 6.3 Update Razor markup in both Details pages to use `FormatDateTime` helpers

- [x] 7. Write property-based tests for Settings page
  - [x] 7.1 Write property test for null/empty field display dash
  - [x] 7.2 Write property test for time zone search filtering correctness
  - [x] 7.3 Write property test for cancel restores persisted state
  - [x] 7.4 Write property test for time zone display format
  - [x] 7.5 Write property test for search always includes saved time zone alias
  - [x] 7.6 Write property test for TimeZoneToString handles all ID types without throwing

- [x] 8. Final verification - All tests pass, project compiles

## Phase 2: Date/Time Format Preference and Theme Property

- [x] 9. Add DateTimeFormat and Theme properties to ApplicationUser
  - [x] 9.1 Add `string? DateTimeFormat` property to `ApplicationUser` with XML documentation
  - [x] 9.2 Add `string? Theme` property to `ApplicationUser` with XML documentation
  - [x] 9.3 Create EF Core migration for the two new columns

- [x] 10. Update IUserTimeZoneContext to support DateTimeFormat
  - [x] 10.1 Add `string? DateTimeFormat { get; }` property to `IUserTimeZoneContext` interface
  - [x] 10.2 Update `UserTimeZoneContext.InitializeAsync` to load `DateTimeFormat` from user entity
  - [x] 10.3 Update `FormatDateTime` overloads to use stored `DateTimeFormat` as default when no explicit format is passed

- [x] 11. Add DateTimeFormat field to Settings page
  - [x] 11.1 Add `DateTimeFormat` property to `InputModel` in `Settings/Index.razor.cs`
  - [x] 11.2 Add `GetFormatLabel` helper method to map format strings to display labels
  - [x] 11.3 Update `EnterEditMode`, `CancelEdit`, and `OnValidSubmitAsync` to include `DateTimeFormat`
  - [x] 11.4 Add DateTimeFormat View Mode display to `Settings/Index.razor`
  - [x] 11.5 Add DateTimeFormat Edit Mode MudSelect to `Settings/Index.razor`

- [x] 12. Checkpoint - Verify Settings page compiles and DateTimeFormat works end-to-end

- [x] 13. Write property-based tests for DateTimeFormat
  - [x] 13.1 Write property test for DateTimeFormat preference applied to formatting
  - [x] 13.2 Write property test for cancel restores DateTimeFormat

- [x] 14. Final verification - All tests pass, project compiles

## Phase 3: Theme Switching

- [x] 15. Define domain enum and JS interop module
  - [x] 15.1 Create the ThemePreference enum at `BlazorWebAppTemplate.Core/Domain/Enums/ThemePreference.cs`
  - [x] 15.2 Create the `theme.js` JavaScript interop module at `wwwroot/js/theme.js`

- [x] 16. Implement ThemeStateService and interface
  - [x] 16.1 Create `IThemeStateService` interface at `Abstractions/IThemeStateService.cs`
  - [x] 16.2 Implement `ThemeStateService` at `Services/ThemeStateService.cs`

- [x] 17. Implement ApplicationTheme with dual palettes
  - [x] 17.1 Create `ApplicationTheme` class at `BlazorWebAppTemplate.UI/Theme/ApplicationTheme.cs`

- [x] 18. Integrate MainLayout with theme subscription
  - [x] 18.1 Update MainLayout to subscribe to ThemeStateService and bind MudThemeProvider
  - [x] 18.2 Implement initial theme loading on first render with JS interop

- [x] 19. Implement Settings page theme change flow
  - [x] 19.1 Add ThemePreference property with instant-save setter, JS interop, and ThemeState notification

- [x] 20. Register DI services and wire everything together
  - [x] 20.1 Register `ThemeStateService` as scoped in `Program.cs`

- [x] 21. Write unit tests for ThemeStateService
  - [x] 21.1 Write tests covering SetDarkMode, SetThemePreference, no-op, and instance independence

- [x] 22. Final checkpoint - All tests pass

## Phase 4: Settings Page Redesign (Instant-Save Refactor)

- [x] 23. Refactor Settings page code-behind for instant-save
  - [x] 23.1 Remove EditForm infrastructure and add instant-save backing fields
    - Removed `InputModel` nested class, `Input` property, `editContext`, `IsBusy`, `OnValidSubmitAsync`
    - Added backing fields: `_timeZoneValue`, `_previousTimeZoneValue`, `_localeValue`, `_previousLocaleValue`, `_dateTimeFormatValue`, `_previousDateTimeFormatValue`
    - Added property setters with no-op check and async save trigger
    - Updated `OnInitializedAsync` to set backing fields directly
  - [x] 23.2 Implement `SaveTimeZoneAsync`, `SaveLocaleAsync`, and `SaveDateTimeFormatAsync` methods
    - Each follows optimistic-UI-with-revert pattern
    - Success alerts: "Time zone updated.", "Locale updated.", "Date/time format updated."
    - Error handling: DbUpdateConcurrencyException, DbUpdateException, TaskCanceledException, IdentityResult.Failed

- [x] 24. Refactor Settings page markup for new layout
  - [x] 24.1 Rewrite `Index.razor` with single MudPaper, two sections, instant-save bindings
    - Removed EditForm, Save/Cancel buttons, View Mode rendering
    - Added two-column layout with MudGrid (md="4" title, md="8" controls)
    - Preferences section: Time Zone + Date/Time Format side by side (sm="6"), Locale full width
    - Appearance section: PillToggle<ThemePreference> with Light/Dark/System items
    - All fields use separate MudInputLabel, Variant.Outlined, Margin.Dense, Typo.body2

- [x] 25. Final checkpoint - Build and verify all changes compile, all tests pass

## Notes

- The page was originally created with View/Edit mode (Phase 1) and later redesigned to instant-save (Phase 4)
- Form model classes were renamed from `PreferencesFormModel` → `ProfileFormModel` → `InputModel` → removed (replaced by direct backing fields)
- `ITimeZoneService` remains a singleton; `IUserTimeZoneContext` and `IThemeStateService` are scoped (one per circuit)
- Time zone alias handling is in the Settings page helpers, not the shared service
- IANA conversion uses built-in .NET 6+ `TryConvertWindowsIdToIanaId()` — no external packages needed
- Property tests use FsCheck 3.1.0 with `[Property(MaxTest = 100)]`
- The `Theme` property type was changed from `string?` to `ThemePreference` enum with EF Core `HasConversion`
- PillToggle and PillToggleItem are generic shared components in the UI project
- All phases are COMPLETE — this document represents the full implementation history
