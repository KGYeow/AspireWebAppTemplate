# Implementation Plan: Audit Log Old/New Values

## Overview

This plan implements two complementary enhancements to the audit log system: (1) an `AuditLogRequest` DTO to replace the long-parameter-list `LogAsync()` method, and (2) old/new value capture for all update operations across controllers. Implementation proceeds bottom-up — DTO and utility first, then service layer, then controller-by-controller migration with change tracking.

## Status: Complete ✓

All tasks have been implemented and verified. 51 tests pass.

## Tasks

- [x] 1. Create AuditLogRequest DTO and AuditChangeHelper utility
- [x] 2. Refactor IAuditLogService and AuditLogService
- [x] 3. Refactor UsersController to use AuditLogRequest with old/new values
- [x] 4. Checkpoint - Ensure all tests pass
- [x] 5. Refactor RolesController to use AuditLogRequest with old/new values
- [x] 6. Refactor PagePermissionsController to use AuditLogRequest with old/new values
- [x] 7. Refactor AuthController to use AuditLogRequest with old/new values
- [x] 8. Final checkpoint - Ensure all tests pass
