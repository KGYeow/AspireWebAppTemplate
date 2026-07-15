# Product

## What Is This?

An enterprise Blazor Server web application template built on .NET Aspire. It provides a production-ready starting point for internal business applications with built-in admin features.

## Who Is It For?

- Enterprise development teams at Jabil building internal web applications
- Developers who need a pre-built admin dashboard with user/role management
- Teams needing a Blazor Server template with .NET Aspire service orchestration

## Core Features

- **User Management** — CRUD, activation/deactivation, LDAP sync, role assignment
- **Role Management** — custom roles with position ordering, system role protection
- **Announcement System** — multi-surface announcements with banner, list page, admin CRUD, scheduling, severity levels, per-user dismissal, HTML content editing, and notification integration
- **Audit Logging** — security-event tracking with old/new value change capture
- **Page-Level Permissions** — database-driven, role-based page access (whitelist model)
- **Notification System** — real-time in-app notifications with SignalR, snackbar popups, deep-linking, and per-category preferences
- **Settings & Preferences** — per-user theme, timezone, date/time format
- **Profile Management** — user profile viewing and editing
- **Authentication** — local Identity + optional LDAP/Active Directory integration
- **Excel Export** — audit log export with server-side filtering

## Optional / Custom Extensions

Features built on top of the template for specific project needs. These are not part of the core template but demonstrate how to extend it:

- **AI Integration** — provider-agnostic AI text generation via Amazon Bedrock (Nova 2 Lite), with configurable model, three-tier credential resolution, and Aspire parameter-based secrets

## Design Principles

- **Template-first**: code should be easy to understand, extend, and strip down
- **Convention over configuration**: follow established patterns rather than inventing new ones
- **Comprehensive documentation**: all code includes XML docs and inline comments
- **Testable**: property-based tests for correctness properties, unit tests for specific cases
- **Separation of concerns**: clear boundaries between Core, API, Web, and UI projects
- **Privacy by design**: only security-sensitive operations are audited; personal preferences and profile edits are not logged
