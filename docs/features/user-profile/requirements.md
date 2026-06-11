# Requirements Document

## Introduction

This feature provides a User Profile page where authenticated users can view and edit their own profile settings. The page serves as the foundation for timezone support and other user preferences. It uses the standard MainLayout (with sidebar visible) and follows the same visual patterns as other detail pages in the application — using flat `MudPaper` containers with no elevation for section grouping, consistent with UserManagement/Details and RoleManagement/Details. The page supports a unified view/edit mode toggle pattern where the layout remains identical between modes (only field interactivity changes). It is accessible from the DropdownProfile menu in the top bar. A reusable `TimeZoneDisplayService` provides UTC-to-local conversion for rendering dates throughout the application. Timezone is auto-detected at login time to ensure correct date display across the application from the first page load.

## Glossary

- **Profile_Page**: The Blazor component accessible at `/profile` that displays and allows editing of the authenticated user's profile information, using the standard MainLayout.
- **System**: The Blazor Server application handling profile display and persistence.
- **User**: An authenticated person accessing their own profile via the Profile_Page.
- **LDAP_User**: A User whose `AuthSource` property is set to `LDAP`, indicating their identity attributes are synchronized from Active Directory.
- **Local_User**: A User whose `AuthSource` property is set to `Local`, indicating their identity attributes are managed within the application.
- **Editable_Fields**: The set of profile properties a user may modify: DisplayName, FirstName, LastName, PhoneNumber, Locale, and TimeZoneId.
- **LDAP_Synced_Fields**: The set of profile properties managed by Active Directory for LDAP users: DisplayName, FirstName, LastName, Email, JobTitle, Department, and EmployeeNumber.
- **Browser_TimeZone**: The IANA time zone identifier detected from the user's browser via JavaScript interop.
- **DropdownProfile**: The user menu component in the application top bar that provides navigation to the Profile_Page and logout functionality.
- **View_Mode**: The default display state of the Profile_Page where profile information is shown as read-only plain text with labels, using the same container layout as Edit_Mode.
- **Edit_Mode**: The active editing state of the Profile_Page where editable fields become form inputs allowing modification, using the same container layout as View_Mode.
- **TimeZoneDisplayService**: The service class implementing ITimeZoneDisplayService that provides timezone conversion utilities using IANA identifiers.
- **Cover_Banner**: A decorative gradient banner displayed at the top of the Profile_Page (160px height, `linear-gradient(135deg, #667eea 0%, #764ba2 100%)`), over which the avatar partially overlaps via negative margin-top.
- **Profile_Header_Card**: The `MudPaper` section immediately below the Cover_Banner (visually connected via `rounded-b-lg`) that contains the overlapping avatar, the edit icon button, and the user's name/summary info.
- **Login_Flow**: The post-authentication sequence where MainLayout's `OnAfterRenderAsync(firstRender)` executes timezone detection logic on the first authenticated page render.
- **OptionalPhoneAttribute**: A custom validation attribute at `BlazorWebAppTemplate.Core/Utilities/OptionalPhoneAttribute.cs` that treats null, empty, and whitespace-only values as valid while validating non-empty values against a permissive phone number pattern.
- **MudInputLabel**: A MudBlazor component used to render label text above form fields or display values.
- **fw-bold**: A CSS utility class that applies `font-weight: bold` to label text, creating visual distinction between labels and values through weight contrast rather than color contrast.
- **Account_Manage_Page**: The Identity-scaffolded account management page located at `Components/Account/Pages/Manage/Index.razor.cs`, which contains a phone number input field.

## Requirements

### Requirement 1: Profile Page Access and Navigation

**User Story:** As an authenticated user, I want to access my profile page from the top bar dropdown menu, so that I can quickly navigate to view and manage my personal information.

#### Acceptance Criteria

1. THE Profile_Page SHALL be accessible to all authenticated users at the route `/profile`.
2. WHEN an unauthenticated user navigates to the Profile_Page route, THE System SHALL redirect the user to the login page.
3. WHEN the Profile_Page loads, THE System SHALL display the current user's profile information within 2 seconds.
4. THE Profile_Page SHALL use the InteractiveServer render mode (applied globally via `App.razor`'s `PageRenderMode`) to support form interactions without full page reloads.
5. THE Profile_Page SHALL rely on the global `AuthorizeRouteView` in `Routes.razor` for authentication enforcement (no explicit `[Authorize]` attribute required on the page itself).
6. THE DropdownProfile SHALL always display a "Profile" menu item that navigates to the `/profile` route.
7. THE DropdownProfile SHALL rename the existing "Manage Account" menu item to "Settings" and always display it.
8. THE DropdownProfile SHALL always show both "Profile" and "Settings" menu items to authenticated users.
9. THE Profile_Page SHALL NOT be linked from the application sidebar navigation.

### Requirement 2: Profile Page Visual Design and Container Consistency

**User Story:** As an authenticated user, I want my profile page to use the same visual container patterns as other detail pages in the application, so that the UI feels consistent and cohesive across the entire app.

#### Acceptance Criteria

1. THE Profile_Page SHALL display a Cover_Banner image at the top of the page.
2. THE Profile_Page SHALL display the user's avatar overlapping the Cover_Banner bottom-left edge (LinkedIn-style layout: avatar below-left of banner, name below avatar).
3. THE Profile_Page SHALL use `MudPaper` components with `Class="pa-4 mb-4"` and `Elevation="0"` to group profile sections, matching the container pattern used by UserManagement/Details and RoleManagement/Details pages.
4. THE Profile_Page SHALL NOT use `MudCard` components with non-zero elevation for section containers.
5. THE Profile_Page SHALL provide clear visual hierarchy and spacing between sections.
6. THE Profile_Page SHALL NOT use the flat PageHeader plus form layout pattern used by UserManagement or RoleManagement list pages.
7. THE Profile_Page content sections SHALL expand to fill the full available page width (no column width constraint).

### Requirement 3: Profile Information Display

**User Story:** As an authenticated user, I want to see all my profile information organized in sections, so that I can quickly find and review my details.

#### Acceptance Criteria

1. THE Profile_Page SHALL display the following personal information fields: DisplayName, FirstName, LastName, Email, and PhoneNumber.
2. THE Profile_Page SHALL display the following preference fields: Locale and TimeZoneId.
3. THE Profile_Page SHALL display the following organization fields as read-only: JobTitle, Department, and EmployeeNumber.
4. WHEN a field value is null or empty, THE Profile_Page SHALL display a placeholder indicator (e.g., "-") instead of blank space.

### Requirement 4: View and Edit Mode Toggle with Unified Layout

**User Story:** As an authenticated user, I want to view my profile in read-only mode by default and switch to edit mode when I want to make changes, with no layout shift between modes, so that I can browse my information without risk of accidental edits and the transition feels seamless.

#### Acceptance Criteria

1. WHEN the Profile_Page loads, THE Profile_Page SHALL display profile information in View_Mode (read-only plain text with labels).
2. WHEN the User clicks the "Edit" button, THE Profile_Page SHALL transition to Edit_Mode regardless of the current mode.
3. WHILE the Profile_Page is in Edit_Mode, THE Profile_Page SHALL render editable fields as form inputs in the same positions as their View_Mode counterparts.
4. WHEN the User clicks the "Cancel" button in Edit_Mode, THE Profile_Page SHALL revert to View_Mode without saving changes.
5. WHEN the User clicks the "Cancel" button in Edit_Mode, THE Profile_Page SHALL discard all unsaved modifications and restore the original field values.
6. WHILE the Profile_Page is in View_Mode, THE Profile_Page SHALL display an edit icon button (pencil) in the Profile_Header_Card, aligned to the right of the avatar row.
7. WHILE the Profile_Page is in Edit_Mode, THE Profile_Page SHALL display "Save" and "Cancel" buttons.
8. THE Profile_Page SHALL use identical container components (MudPaper), section headers, and field positions in both View_Mode and Edit_Mode.
9. WHEN transitioning between View_Mode and Edit_Mode, THE Profile_Page SHALL NOT change the container type, section structure, or field positions — only the field interactivity (plain text vs. active input) SHALL change.
10. THE Profile_Page section headers SHALL be identical in both View_Mode and Edit_Mode.

### Requirement 5: Edit Mode Form Field Styling

**User Story:** As a user editing my profile, I want form fields to use separate labels above the inputs, so that the editing experience is consistent with other dialog forms in the application.

#### Acceptance Criteria

1. WHILE the Profile_Page is in Edit_Mode, THE Profile_Page SHALL render form field labels using separate MudInputLabel components positioned above the input fields.
2. WHILE the Profile_Page is in Edit_Mode, THE Profile_Page SHALL NOT use the built-in Label property on MudTextField components for field labeling.
3. THE Profile_Page edit form styling SHALL follow the same pattern used in existing dialog forms within the application, whether or not those dialogs use separate labels.

### Requirement 6: Profile Editing for Local Users

**User Story:** As a local user, I want to edit my profile fields, so that I can keep my information up to date.

#### Acceptance Criteria

1. WHILE the User has AuthSource set to Local, THE Profile_Page SHALL render all Editable_Fields as editable form inputs when in Edit_Mode.
2. WHEN the User modifies Editable_Fields and submits the form, THE System SHALL persist the changes to the database.
3. WHEN the profile is saved successfully, THE System SHALL display a success confirmation message and transition the Profile_Page to View_Mode, even if the save operation is still completing asynchronously.
4. IF the profile save operation fails, THEN THE System SHALL display an error message describing the failure.
5. WHILE the save operation is in progress, THE Profile_Page SHALL disable the submit button and display a loading indicator.
6. WHEN the save operation completes (success or failure), THE Profile_Page SHALL clear the loading indicator and re-enable the submit button.

### Requirement 7: Profile Editing Restrictions for LDAP Users

**User Story:** As an LDAP user, I want to see which fields are managed by Active Directory, so that I understand why certain fields cannot be edited.

#### Acceptance Criteria

1. WHILE the User has AuthSource set to LDAP and the Profile_Page is in Edit_Mode, THE Profile_Page SHALL render LDAP_Synced_Fields as read-only (disabled) inputs.
2. WHILE the User has AuthSource set to LDAP and the Profile_Page is in Edit_Mode, THE Profile_Page SHALL render non-LDAP Editable_Fields (PhoneNumber, Locale, TimeZoneId) as editable form inputs, regardless of any configuration issues with LDAP_Synced_Fields.
3. WHILE the User has AuthSource set to LDAP, THE Profile_Page SHALL display a visual indicator (e.g., tooltip or helper text) explaining that LDAP_Synced_Fields are managed by Active Directory.

### Requirement 8: Timezone Auto-Detection at Login

**User Story:** As a user without a timezone set, I want the system to detect and save my timezone automatically at login time, so that dates across the entire application display correctly from the first page load without requiring me to visit the profile page.

#### Acceptance Criteria

1. WHEN a User successfully authenticates and the first authenticated page renders (MainLayout's `OnAfterRenderAsync(firstRender)`), THE System SHALL detect the Browser_TimeZone using JavaScript interop.
2. WHEN the Browser_TimeZone is detected and the User's TimeZoneId is null, THE System SHALL automatically save the detected Browser_TimeZone to the User's profile without requiring user interaction.
3. WHEN the Browser_TimeZone is detected and the User's TimeZoneId is already set to a non-null value, THE System SHALL NOT overwrite the existing TimeZoneId.
4. IF the JavaScript interop for timezone detection fails during the Login_Flow, THEN THE System SHALL NOT display an error to the User and SHALL leave the TimeZoneId unchanged.
5. THE Profile_Page SHALL continue to display the timezone field for manual override regardless of whether auto-detection has occurred.
6. WHEN the Profile_Page loads and the User's TimeZoneId is null (auto-detection did not succeed at login), THE Profile_Page SHALL display the timezone field as empty, allowing the user to manually select a timezone from the searchable dropdown.

### Requirement 9: Timezone Selection

**User Story:** As a user, I want to select my timezone from a searchable dropdown, so that I can override the auto-detected value with my preferred timezone.

#### Acceptance Criteria

1. THE Profile_Page SHALL display a searchable dropdown for timezone selection containing all IANA time zone identifiers.
2. WHEN the User types in the timezone dropdown, THE Profile_Page SHALL filter the available options to match the search text.
3. THE timezone dropdown SHALL display timezone names in a human-readable format (e.g., "(UTC+08:00) Asia/Kuala_Lumpur").
4. WHEN the User selects a timezone from the dropdown, THE Profile_Page SHALL update the TimeZoneId field with the selected IANA identifier.

### Requirement 10: Form Validation

**User Story:** As a user, I want to receive clear feedback when I enter invalid data, so that I can correct my input before saving.

#### Acceptance Criteria

1. WHEN the User submits the form with an invalid phone number format, THE System SHALL display a validation error on the PhoneNumber field.
2. WHEN the User submits the form with a DisplayName exceeding 100 characters, THE System SHALL display a validation error on the DisplayName field.
3. WHEN the User submits the form with a FirstName or LastName exceeding 100 characters, THE System SHALL display a validation error on the respective field.
4. THE Profile_Page SHALL validate all fields on the client side before submitting to the server.

### Requirement 11: Avatar Display

**User Story:** As a user, I want to see my profile avatar on the profile page, so that I can confirm my visual identity.

#### Acceptance Criteria

1. WHEN the User has a non-empty AvatarUrl, THE Profile_Page SHALL display the avatar image and SHALL NOT display the fallback avatar.
2. WHEN the User has a null or empty AvatarUrl, THE Profile_Page SHALL display a fallback avatar using the first character of the DisplayName or UserName and SHALL NOT display the avatar image.
3. THE Profile_Page SHALL display the avatar overlapping the Cover_Banner as specified in Requirement 2.

### Requirement 12: XML Documentation for TimeZoneDisplayService

**User Story:** As a developer, I want the TimeZoneDisplayService implementation to have XML documentation on public members, so that the code is self-documenting and maintainable.

#### Acceptance Criteria

1. THE TimeZoneDisplayService SHALL have XML documentation comments (summary, param, returns, remarks where appropriate) on the class declaration and all public members (using `<inheritdoc />` where the interface already documents the contract).
2. THE TimeZoneDisplayService XML documentation SHALL follow standard C# XML documentation conventions.

### Requirement 13: Edit Icon Button in Profile Header

**User Story:** As an authenticated user, I want the Edit button to be a small pencil icon in the profile header area, so that it is discoverable without cluttering the profile and follows the LinkedIn-style profile editing pattern.

#### Acceptance Criteria

1. WHILE the Profile_Page is in View_Mode, THE Profile_Page SHALL display a pencil icon button (MudIconButton with `Icons.Material.Outlined.Edit`) positioned in the Profile_Header_Card, aligned to the right of the avatar row via `MudStack Row Justify="Justify.SpaceBetween"`.
2. THE edit icon button SHALL use `Size.Small` for a compact appearance.
3. THE edit icon button SHALL be hidden in Edit_Mode (Save/Cancel buttons are shown instead).
4. THE Profile_Page SHALL NOT use a text-based "Edit" button.

### Requirement 14: DropdownProfile Menu Styling and Organization

**User Story:** As an authenticated user, I want the dropdown profile menu to have clear visual grouping with dividers and rounded menu items, so that it feels polished and consistent with other menus in the application.

#### Acceptance Criteria

1. THE DropdownProfile SHALL display menu items in the order: Profile → Settings → Divider → Log Out.
2. THE DropdownProfile SHALL include a visual divider (MudDivider) between the Settings and Log Out items to separate navigation from destructive actions.
3. THE DropdownProfile menu items SHALL use rounded-square styling (border-radius) matching the action-menu overflow pattern used in UserManagement and RoleManagement index pages.
4. THE Profile_Page SHALL NOT be linked from the application sidebar navigation — it is accessible only via the DropdownProfile.

### Requirement 15: View Mode Typography and Label Consistency

**User Story:** As an authenticated user, I want field values and labels on the Profile page to use consistent typography and element types, so that the page looks polished and matches the rest of the application.

#### Acceptance Criteria

1. WHILE the Profile_Page is in View_Mode, THE Profile_Page SHALL render all field values using `Typo="Typo.body2"` (14px) to match the standard text size used in data grids across the application.
2. WHILE the Profile_Page is in View_Mode, THE Profile_Page SHALL render all field labels using `<MudInputLabel>` elements to be visually consistent with Edit_Mode labels.
3. THE Profile_Page SHALL NOT use `Typo="Typo.body1"` for field values in View_Mode.
4. THE Profile_Page SHALL NOT use `<MudText Typo="Typo.caption">` for field labels in View_Mode.

### Requirement 16: Phone Number Clearing (OptionalPhone Validation)

**User Story:** As a user, I want to clear my phone number on both the Profile page and the Account/Manage page without encountering validation errors, so that I can remove my phone number if I no longer wish to share it.

#### Acceptance Criteria

1. WHEN the phone number field is submitted with an empty or whitespace-only value on either the Profile_Page or the Account_Manage_Page, THE System SHALL accept the submission without validation errors and store the phone number as cleared (null).
2. WHEN the phone number field is submitted with a non-empty value matching the pattern of digits, spaces, hyphens, parentheses, periods, and an optional leading plus sign, THE System SHALL accept the submission without validation errors.
3. WHEN the phone number field is submitted with a non-empty value containing characters outside the allowed set, THE System SHALL display a validation error message indicating the phone number is not valid.
4. THE Profile_Page and Account_Manage_Page SHALL both use the OptionalPhoneAttribute on their respective `PhoneNumber` properties instead of the built-in `[Phone]` attribute.

### Requirement 17: Label Visual Hierarchy (fw-bold)

**User Story:** As a user, I want labels to appear visually distinct from values on the Profile page, so that I can quickly distinguish field names from their content.

#### Acceptance Criteria

1. THE Profile_Page SHALL render all MudInputLabel elements in both View_Mode and Edit_Mode with the `fw-bold` CSS class applied via the Class attribute.
2. WHILE the Profile_Page is in View_Mode, THE Profile_Page SHALL render label elements with the `fw-bold` class and value elements (MudText) without the `fw-bold` class, so that labels appear in bold font weight and values appear in normal font weight.
3. WHEN the user switches between View_Mode and Edit_Mode, THE Profile_Page SHALL render labels with the same `fw-bold` class such that label styling does not change between modes.
4. THE Profile_Page SHALL NOT apply any mode-specific CSS classes to MudInputLabel elements that would override or alter the `fw-bold` styling in either View_Mode or Edit_Mode.

---

## Post-Implementation Changes

> **Note:** The following changes were made after the original implementation of this spec. They are documented here for traceability.

1. **Requirement 3.2 superseded** — The Preferences fields (Locale, TimeZoneId) are no longer on the Profile page. They were moved to a dedicated Settings page at `/settings`. See `.kiro/specs/settings-page/` for the current implementation.

2. **Requirement 9 superseded** — Timezone Selection (searchable dropdown) is no longer on the Profile page. It now lives on the Settings page at `/settings`.

3. **`TimeZoneDisplayService` renamed to `TimeZoneService`** — The interface `ITimeZoneDisplayService` was renamed to `ITimeZoneService` and the implementation `TimeZoneDisplayService` was renamed to `TimeZoneService`. The service now also converts Windows timezone IDs to IANA via `TryConvertWindowsIdToIanaId()`.

4. **`ProfileFormModel` renamed to `InputModel`** — The nested form model class was renamed from `ProfileFormModel` to `InputModel` for consistency with all other pages in the application.

5. **DropdownProfile menu simplified** — The original menu order was "Profile → Preferences → Settings → Divider → Log Out". The old "Settings" item (pointing to `Account/Manage`) was removed, and "Preferences" was renamed to "Settings" pointing to `/settings`. Final order: **Profile → Settings → Divider → Log Out**.

6. **Requirement 15 label count** — Originally referenced 10 labels (5 Personal Information + 2 Preferences + 3 Organization). After Preferences extraction, the Profile page now has 8 labels (5 Personal Information + 3 Organization).

7. **Requirement 17 label count and styling** — Originally referenced 20 MudInputLabel elements with `mud-text-secondary`. The actual implementation uses `fw-bold` instead of `mud-text-secondary` for label visual hierarchy (bold weight contrast rather than color contrast). After Preferences extraction, the Profile page now has 16 labels (8 View Mode + 8 Edit Mode).
