# Implementation Plan: Page Access Permissions

## Overview

Replace hardcoded `[Authorize(Roles = "...")]` attributes with a database-driven, role-based page authorization system. Implementation progresses from shared DTOs and entity modeling, through API service layer, into the Web project's authorization infrastructure, and concludes with the admin UI and cleanup of legacy role attributes.

## Status: Complete ✓

All tasks have been implemented and verified.

## Tasks

- [x] 1. Create shared DTOs and PagePermission entity
- [x] 2. Implement Page Permission Service in ApiService
- [x] 3. Implement PagePermissions API Controller
- [x] 4. Checkpoint - Ensure API layer builds and tests pass
- [x] 5. Implement PagePermissionContext in Web project
- [x] 6. Implement PagePermissionHandler for authorization enforcement
- [x] 7. Checkpoint - Ensure authorization infrastructure builds and tests pass
- [x] 8. Implement NavMenu permission filtering
- [x] 9. Implement Admin Page Permission Matrix UI
- [x] 10. Update DefaultNavigationProvider and remove hardcoded attributes
- [x] 11. Implement seed data for default permissions
- [x] 12. Final checkpoint - Ensure full solution builds and all tests pass
