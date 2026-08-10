# Requirements Document

## Introduction

The Settings page at `/settings` provides authenticated users with instant-save preference management for Time Zone, Locale, Date/Time Format, and Appearance (theme). It uses instant-save on all fields (no Save button), a PillToggle for theme selection, and integrates with the ThemeStateService for real-time theme switching. The page presents Preferences and Appearance as two sections within a single card separated by a divider, using a two-column layout (title/description on left, controls on right).

## Glossary

- **Settings_Page**: The Blazor component page located at `Components/Pages/Settings/Index.razor`, routed at `/settings`, responsible for displaying and editing user preferences including Time Zone, Locale, Date/Time Format, and Appearance (theme) using instant-save for all fields.
- **Profile_Page**: The existing Blazor component page located at `Components/Pages/Profile/Index.razor`, routed at `/profile`, responsible for displaying and editing Personal Information and Organization sections.
- **Time_Zone_Field**: A MudAutocomplete component that allows the user to search and select an IANA time zone identifier.
- **Locale_Field**: A MudTextField component that accepts a locale/culture string (e.g., "en-US").
- **DateTimeFormat_Field**: A MudSelect component that allows the user to choose a preferred date/time display format from a predefined list of common patterns.
- **Authenticated_User**: A user who has successfully signed in via ASP.NET Core Identity and has an active SignalR circuit.
- **UserManager**: The ASP.NET Core Identity service (`UserManager<ApplicationUser>`) used to load and persist user data.
- **DropdownProfile**: The existing Blazor component at `Components/Layout/DropdownProfile.razor` that renders the user profile dropdown menu in the application header, containing navigation links (Profile, Settings, Log Out).
- **Time_Zone_Alias**: An IANA time zone identifier (e.g., "Asia/Kuala_Lumpur") that is a valid alias for a canonical time zone (e.g., "Asia/Singapore") but does not appear in the list returned by `TimeZoneInfo.GetSystemTimeZones()`. Aliases are resolvable via `TimeZoneInfo.FindSystemTimeZoneById()`.
- **Canonical_Time_Zone_List**: The list of time zone entries built by `TimeZoneHelper`, which converts Windows-style IDs to IANA identifiers via `TimeZoneInfo.TryConvertWindowsIdToIanaId()`.
- **ITimeZoneHelper**: A singleton service providing the canonical time zone list and stateless UTC-to-local conversion.
- **IUserTimeZoneContext**: A scoped service (one per Blazor Server circuit) that holds the current user's time zone ID and preferred date/time format, and provides `FormatDateTime` overloads for user-aware datetime formatting.
- **ThemeStateService**: A scoped service (`Services/ThemeStateService.cs`) implementing `IThemeStateService` that holds the current `IsDarkMode` boolean state per SignalR circuit and fires an `OnChange` event when the theme changes. Acts as a pub/sub mechanism between the Settings page and the MainLayout.
- **IThemeStateService**: The interface (`Abstractions/IThemeStateService.cs`) defining the contract for theme state management: `IsDarkMode` property, `OnChange` event, `SetDarkMode(bool)` method, and `SetThemePreference(ThemePreference, bool)` method.
- **MainLayout**: The root layout component (`Components/Layout/MainLayout.razor`) that renders the `MudThemeProvider` with `IsDarkMode` bound, subscribes to `ThemeStateService.OnChange`, and re-renders when the theme changes.
- **MudThemeProvider**: MudBlazor's theme provider component that automatically applies `PaletteLight` or `PaletteDark` from the configured `MudTheme` based on its `IsDarkMode` property.
- **ApplicationTheme**: The custom `MudTheme` subclass (`AspireWebAppTemplate.UI/Theme/ApplicationTheme.cs`) that defines both `PaletteLight` and `PaletteDark` color palettes for the application.
- **ThemePreference**: An enum (`Core/Domain/Enums/ThemePreference.cs`) with values `System`, `Light`, `Dark` representing the user's preferred UI theme.
- **ApplicationUser**: The Identity user entity that stores the user's `Theme` property (of type `ThemePreference`), `TimeZoneId`, `Locale`, and `DateTimeFormat` persisted to the database.
- **JS_Interop_Module**: The JavaScript module at `wwwroot/js/theme.js` that exports `getSystemPrefersDark()` for detecting the OS color scheme preference.
- **SignalR_Circuit**: A Blazor Server connection representing a single user session; scoped DI services are unique per circuit.
- **PillToggle**: A generic reusable shared component at `AspireWebAppTemplate.UI/Components/Shared/PillToggle.razor` that wraps `MudToggleGroup<T>` with pill styling (border-radius: 9999px, no delimiters, no ripple, outlined with input field border color). Renders child `PillToggleItem<T>` elements within a pill-shaped container.
- **PillToggleItem**: A single item within a `PillToggle`, located at `AspireWebAppTemplate.UI/Components/Shared/PillToggleItem.razor`, that wraps `MudToggleItem<T>` with circular styling (rounded-circle, 36x36px) and accessibility attributes (`title` and `aria-label` set via the `Title` parameter).
- **Theme_Pill_Toggle**: A `PillToggle<ThemePreference>` instance containing three `PillToggleItem<ThemePreference>` items (Light, Dark, System) that allows the user to select a theme preference.
- **Instant_Save**: A persistence pattern where the value is saved immediately upon user selection or value change without requiring a separate Save button click. Used for all fields on the Settings page.
- **Theme_Property**: The `Theme` property on `ApplicationUser` storing the user's preferred UI theme as a `ThemePreference` enum (persisted as string via EF Core `HasConversion`).

## Requirements

### Requirement 1: Settings Page Routing and Access

**User Story:** As an authenticated user, I want to access a dedicated Settings page at `/settings`, so that I can manage my preference settings independently from my profile information.

#### Acceptance Criteria

1. THE Settings_Page SHALL be routable at the path `/settings`
2. WHEN an Authenticated_User navigates to `/settings`, THE Settings_Page SHALL load and display the user's current preference values within 2 seconds of navigation
3. IF the authenticated user's record cannot be loaded from the database, THEN THE Settings_Page SHALL redirect to the `Account/InvalidUser` page without displaying the preference form
4. WHEN an unauthenticated user navigates to `/settings`, THE Settings_Page SHALL redirect the user to the login page at `Account/Login` with a `returnUrl` query parameter preserving the original `/settings` path
5. THE Settings_Page SHALL require authentication via the `[Authorize]` attribute, consistent with the application's global authorization policy

### Requirement 2: Settings Page Layout and Structure

**User Story:** As a developer, I want the Settings page to use a consistent MudBlazor layout with instant-save controls, so that the codebase remains cohesive with the Profile page and dialog form patterns.

#### Acceptance Criteria

1. THE Settings_Page SHALL display all sections within a single MudPaper container with `Class="pa-4"` and `Elevation="0"`, with sections separated by `MudDivider Class="my-6"`.
2. THE Preferences_Section SHALL use a two-column layout: section heading "Preferences" (`MudText Typo="Typo.h6"`) and description in the left column (`MudItem xs="12" md="4"`), and form controls in the right column (`MudItem xs="12" md="8"`).
3. THE Appearance_Section SHALL use a two-column layout: section heading "Appearance" (`MudText Typo="Typo.h6"`) and description in the left column (`MudItem xs="12" md="4"`), and the Theme_Pill_Toggle in the right column (`MudItem xs="12" md="8"`).
4. THE Preferences_Section right column SHALL contain the following fields in order: Time Zone (MudAutocomplete) and Date/Time Format (MudSelect) on the same row using `MudItem xs="12" sm="6"` each, followed by Locale (MudTextField) on a second row at full width using `MudItem xs="12"`.
5. THE Settings_Page SHALL use separate `<MudInputLabel>` elements above each input field (not the built-in `Label` property of the input component) for all field labels.
6. THE Settings_Page SHALL use `Variant.Outlined` with `Margin.Dense` and `Typo="Typo.body2"` for all input fields.
7. THE Settings_Page SHALL display a page-level heading "Settings" using the existing PageHeader component above the sections.
8. THE Settings_Page SHALL NOT render a Save button, Submit button, Edit button, Cancel button, or any mechanism to toggle between a read-only view and an editable view.

### Requirement 3: Instant Save for Preference Fields

**User Story:** As an authenticated user, I want my Time Zone, Locale, and Date/Time Format changes to save immediately when I change them, so that I get instant feedback without needing to click a Save button.

#### Acceptance Criteria

1. WHEN the user changes the Time Zone value, THE Settings_Page SHALL persist the new value to the database via UserManager.UpdateAsync immediately upon value change without requiring a Save button click.
2. WHEN the user changes the Locale value, THE Settings_Page SHALL persist the new value to the database via UserManager.UpdateAsync immediately upon value change without requiring a Save button click.
3. WHEN the user changes the Date/Time Format value, THE Settings_Page SHALL persist the new value to the database via UserManager.UpdateAsync immediately upon value change without requiring a Save button click.
4. IF a preference field save operation fails due to a database error (DbUpdateException or TaskCanceledException), THEN THE Settings_Page SHALL display a dismissible error alert indicating the save failed and SHALL revert the field to its previously persisted value.
5. IF a preference field save operation fails due to a concurrency conflict (DbUpdateConcurrencyException), THEN THE Settings_Page SHALL display a dismissible error alert instructing the user to reload the page and SHALL revert the field to its previously persisted value.
6. WHEN a preference field save operation succeeds, THE Settings_Page SHALL display a dismissible success alert indicating the preference has been updated.
7. IF the same value that is already persisted is selected again, THEN THE Settings_Page SHALL NOT trigger a save operation (no-op on same value).

### Requirement 4: Instant Save for Theme

**User Story:** As an authenticated user, I want my theme selection to take effect immediately when I click a theme option, so that I get instant visual feedback.

#### Acceptance Criteria

1. WHEN the user selects a ThemePreference option (Light, Dark, or System) on the Theme_Pill_Toggle, THE Settings_Page SHALL persist the value to the database via UserManager.UpdateAsync immediately without requiring the user to click a Save button.
2. AFTER successfully persisting the theme preference, THE Settings_Page SHALL detect the OS dark mode preference by importing the JS_Interop_Module and calling `getSystemPrefersDark()`.
3. AFTER obtaining the system preference, THE Settings_Page SHALL call `ThemeState.SetThemePreference(theme, systemPrefersDark)` to update the shared state and trigger the layout re-render.
4. IF the same ThemePreference value that is already active is selected again, THEN THE Settings_Page SHALL NOT trigger a save operation (no-op on same value).
5. IF the theme save operation fails, THEN THE Settings_Page SHALL revert the Theme_Pill_Toggle selection to the previously persisted ThemePreference value and display a dismissible error alert.

### Requirement 5: Time Zone Autocomplete and IANA Conversion

**User Story:** As an authenticated user, I want to search for time zones by name or identifier using IANA conventions, so that I can quickly find and select my correct time zone.

#### Acceptance Criteria

1. WHEN the user types in the Time_Zone_Field, THE Settings_Page SHALL filter available time zones by performing a case-insensitive substring match of the search text against both the display name and the IANA identifier, displaying only matching results.
2. WHEN the search text is empty or contains only whitespace, THE Time_Zone_Field SHALL display all available time zones ordered by UTC offset ascending, then alphabetically by IANA identifier.
3. WHEN the user selects a time zone from the autocomplete results, THE Time_Zone_Field SHALL display the selected time zone's display name in the format "(UTC±HH:mm) IANA_Identifier".
4. THE ITimeZoneHelper SHALL convert Windows-style time zone IDs to IANA identifiers using `TimeZoneInfo.TryConvertWindowsIdToIanaId()` when building the canonical time zone list.
5. THE ITimeZoneHelper SHALL deduplicate entries where multiple Windows IDs map to the same IANA ID.
6. THE ITimeZoneHelper SHALL format all display names in the pattern "(UTC±HH:mm) IANA_Identifier".

### Requirement 6: Time Zone Alias Handling

**User Story:** As an authenticated user whose browser reports a time zone alias not present in the canonical list, I want the Settings page to correctly display and include my saved time zone.

#### Acceptance Criteria

1. WHEN the Time_Zone_Field search function executes, THE Settings_Page SHALL always include the user's currently-saved TimeZoneId in the autocomplete results if the saved TimeZoneId is not already present in the Canonical_Time_Zone_List.
2. WHEN the Settings_Page displays a saved TimeZoneId that is a Time_Zone_Alias not present in the Canonical_Time_Zone_List, THE Settings_Page SHALL display the alias with proper UTC offset formatting by resolving the alias via `TimeZoneInfo.FindSystemTimeZoneById()`.
3. THE TimeZoneToString function SHALL handle Time_Zone_Alias identifiers by first checking the Canonical_Time_Zone_List for a match, and IF no match is found, THEN resolving the identifier via `TimeZoneInfo.FindSystemTimeZoneById()` to produce a display string in the format "(UTC±HH:mm) IANA_Identifier".
4. IF `TimeZoneInfo.FindSystemTimeZoneById()` throws a `TimeZoneNotFoundException` for a saved TimeZoneId, THEN THE TimeZoneToString function SHALL return the raw identifier string as the display value without throwing an exception.

### Requirement 7: Date/Time Format Preference

**User Story:** As an authenticated user, I want to choose my preferred date/time display format, so that timestamps throughout the application are shown in a format I find natural and readable.

#### Acceptance Criteria

1. THE Settings_Page SHALL display a "Date/Time Format" field in the Preferences section as a MudSelect component.
2. THE DateTimeFormat_Field SHALL provide the following predefined format options:
   - `yyyy-MM-dd HH:mm` — labeled "ISO (2026-05-28 14:30)"
   - `dd/MM/yyyy HH:mm` — labeled "Day first (28/05/2026 14:30)"
   - `MM/dd/yyyy h:mm tt` — labeled "US (05/28/2026 2:30 PM)"
   - `dd MMM yyyy HH:mm` — labeled "Short month (28 May 2026 14:30)"
   - `d MMMM yyyy HH:mm` — labeled "Long month (28 May 2026 14:30)"
3. IF the user's DateTimeFormat value is null or empty, THEN the system SHALL use the default format "yyyy-MM-dd HH:mm".
4. THE IUserTimeZoneContext SHALL use the user's stored DateTimeFormat as the default format parameter when no explicit format is passed to `FormatDateTime`, falling back to "yyyy-MM-dd HH:mm" if the stored value is null or empty.

### Requirement 8: User Time Zone Context Service

**User Story:** As a developer, I want a scoped service that provides user-aware datetime formatting, so that pages can display UTC dates in the user's configured time zone without per-page boilerplate.

#### Acceptance Criteria

1. THE IUserTimeZoneContext SHALL be registered as a scoped service (one per Blazor Server circuit).
2. THE IUserTimeZoneContext SHALL be initialized once per circuit from MainLayout on first render.
3. THE IUserTimeZoneContext SHALL provide `FormatDateTime` overloads for `DateTime`, `DateTime?`, and `DateTimeOffset?` with configurable format strings and fallback values.
4. WHEN the user has no time zone configured, THE IUserTimeZoneContext SHALL format dates with a "UTC" suffix as fallback.
5. THE IUserTimeZoneContext interface SHALL reside in `AspireWebAppTemplate.Web/Abstractions/` alongside other frontend-project interfaces.

### Requirement 9: UTC DateTime Display Conversion

**User Story:** As an authenticated user, I want all datetime values displayed in the application to be converted to my configured time zone, so that I can understand timestamps in my local context.

#### Acceptance Criteria

1. THE UserManagement Details page SHALL display Created, Last Updated, Last Login, Last Password Change, and Lockout End dates in the viewer's configured time zone.
2. THE RoleManagement Details page SHALL display Created Date and Last Updated Date in the viewer's configured time zone.
3. WHEN the viewer has no time zone configured, dates SHALL display in UTC with a "UTC" suffix.

### Requirement 10: Theme State Service as Pub/Sub Mechanism

**User Story:** As a developer, I want a scoped service that holds the current dark mode state and notifies subscribers when it changes, so that theme changes propagate instantly across components within the same user session.

#### Acceptance Criteria

1. THE ThemeStateService SHALL expose a boolean `IsDarkMode` property indicating whether the dark palette is currently active.
2. THE ThemeStateService SHALL expose an `OnChange` event that fires when the `IsDarkMode` value changes.
3. WHEN `SetDarkMode(isDark)` is called with a value equal to the current `IsDarkMode`, THE ThemeStateService SHALL NOT fire the `OnChange` event (no-op on same value).
4. WHEN `SetDarkMode(isDark)` is called with a value different from the current `IsDarkMode`, THE ThemeStateService SHALL update `IsDarkMode` and fire the `OnChange` event.
5. WHEN `SetThemePreference(preference, systemPrefersDark)` is called, THE ThemeStateService SHALL resolve the effective dark mode state as: `Dark` maps to `true`, `Light` maps to `false`, `System` maps to the `systemPrefersDark` parameter value.
6. THE ThemeStateService SHALL be registered in the DI container as a scoped service (`AddScoped<IThemeStateService, ThemeStateService>()`), ensuring one instance per SignalR_Circuit.

### Requirement 11: MainLayout Theme Subscription and Rendering

**User Story:** As an authenticated user, I want the application layout to automatically reflect my theme preference as soon as it changes, so that I see the correct color palette without reloading the page.

#### Acceptance Criteria

1. THE MainLayout SHALL bind the `MudThemeProvider` component's `IsDarkMode` property to a local `_isDarkMode` field.
2. WHEN the MainLayout initializes, THE MainLayout SHALL subscribe to `ThemeStateService.OnChange`.
3. WHEN `ThemeStateService.OnChange` fires, THE MainLayout SHALL update its local `_isDarkMode` field from `ThemeState.IsDarkMode` and call `StateHasChanged` to re-render.
4. WHEN the MainLayout is disposed, THE MainLayout SHALL unsubscribe from `ThemeStateService.OnChange` to prevent memory leaks.
5. WHEN the MainLayout renders for the first time after authentication, THE MainLayout SHALL load the Authenticated_User's `ThemePreference` from the database via `UserManager`, resolve it (using JS interop for System preference), and call `ThemeState.SetDarkMode(isDark)` to synchronize the shared state.

### Requirement 12: Dual Palette Theme Definition

**User Story:** As a user, I want the application to have distinct light and dark color palettes, so that each theme mode provides appropriate contrast and readability.

#### Acceptance Criteria

1. THE ApplicationTheme SHALL define a `PaletteLight` with light background colors, dark text colors, and the application's brand primary color for light mode rendering.
2. THE ApplicationTheme SHALL define a `PaletteDark` with dark background colors, light text colors, and adjusted brand colors optimized for dark surface readability.
3. THE MudThemeProvider SHALL automatically apply `PaletteLight` when `IsDarkMode` is `false` and `PaletteDark` when `IsDarkMode` is `true`.
4. THE ApplicationTheme SHALL define both `AppbarBackground` and `DrawerBackground` values in each palette to ensure the application shell renders correctly in both modes.

### Requirement 13: JavaScript Interop for System Preference Detection

**User Story:** As a user with the "System" theme preference, I want the application to detect my operating system's color scheme preference.

#### Acceptance Criteria

1. THE JS_Interop_Module SHALL export a `getSystemPrefersDark()` function that returns a boolean.
2. WHEN called, THE `getSystemPrefersDark()` function SHALL evaluate `window.matchMedia('(prefers-color-scheme: dark)').matches` and return the result.
3. IF the `window.matchMedia` call throws an error, THEN THE `getSystemPrefersDark()` function SHALL return `false` as a safe default.
4. THE JS_Interop_Module SHALL be located at `wwwroot/js/theme.js` and loaded via dynamic ES module import.

### Requirement 14: Scoped Service Isolation per User Session

**User Story:** As a user on a multi-user Blazor Server application, I want my theme preference to apply only to my session, so that changing my theme does not affect other users.

#### Acceptance Criteria

1. THE ThemeStateService SHALL be registered with scoped lifetime, ensuring each SignalR_Circuit receives its own independent instance.
2. WHEN one user changes their theme preference, THE ThemeStateService instance for other users' circuits SHALL NOT be affected.
3. THE ThemeStateService SHALL NOT use static fields or shared state that could leak between circuits.

### Requirement 15: DropdownProfile Settings Navigation

**User Story:** As an authenticated user, I want a Settings link in the profile dropdown menu, so that I can quickly navigate to the Settings page from anywhere in the application.

#### Acceptance Criteria

1. THE DropdownProfile SHALL render a "Settings" MudMenuItem after the "Profile" menu item, making the full menu order: Profile, Settings, Divider, Log Out.
2. THE DropdownProfile Settings menu item SHALL navigate to `/settings` when clicked.
3. THE DropdownProfile Settings menu item SHALL display the `Icons.Material.Rounded.Settings` icon consistent with the icon style used by other menu items.

### Requirement 16: Theme Pill Toggle Accessibility

**User Story:** As a user relying on assistive technology, I want the theme toggle buttons to have accessible labels, so that I can understand and operate the theme selection.

#### Acceptance Criteria

1. EACH PillToggleItem in the Theme_Pill_Toggle SHALL include a `Title` parameter with the value "Light", "Dark", or "System" corresponding to its ThemePreference value, which the PillToggleItem component renders as both a `title` attribute and an `aria-label` attribute.
2. THE active theme option SHALL be visually distinguished by MudToggleGroup's built-in active state styling (Color.Primary applied to the selected item).
3. THE Theme_Pill_Toggle SHALL use the PillToggle and PillToggleItem shared components, delegating active state management and keyboard navigation to MudToggleGroup's native behavior.
