# Implementation Plan: User Profile — Adjustments

## Overview

Post-implementation adjustments based on user feedback. All tasks completed.

## Completed Tasks

- [x] 1. Combine dual profile headers into single LinkedIn-style design
  - [x] 1.1 Merge banner avatar and Header_Row into one section
  - [x] 1.2 Restructure to LinkedIn layout: banner → overlapping avatar (bottom-left) → name below avatar
  - [x] 1.3 Replace text Edit button with pencil icon button (MudIconButton) top-right of banner

- [x] 2. Remove timezone detection from Profile Page
  - [x] 2.1 Remove OnAfterRenderAsync timezone detection logic and IJSRuntime injection

- [x] 3. Remove Profile from sidebar navigation
  - [x] 3.1 Remove Profile NavItem from DefaultNavigationProvider

- [x] 4. Update DropdownProfile with divider and item ordering
  - [x] 4.1 Add divider before Log Out (Profile → Settings → Divider → Log Out)

- [x] 5. Apply rounded menu item styling to DropdownProfile
  - [x] 5.1 Add action-menu CSS class to PopoverClass for rounded menu items

- [x] 6. Fix content sections to span full page width
  - [x] 6.1 Remove MudGrid/MudItem xs="12" md="8" wrapper constraining content width

- [x] 7. Checkpoint - All diagnostics pass (0 issues)

## Notes

> **Post-implementation:** Preferences section was extracted to a dedicated Settings page. See `.kiro/specs/settings-page/` for the current implementation.

- Build MSB3027 errors are from running dev server locking DLLs — not code issues
- All IDE diagnostics pass with zero issues on all modified files
- LinkedIn-style layout: cover banner → avatar overlapping bottom-left → name below → full-width sections
