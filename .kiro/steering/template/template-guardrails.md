---
inclusion: always
---

<!-- TEMPLATE-OWNED steering (capability-protection rule). Inherited from AspireWebAppTemplate.
     Highest-stakes guardrail: keep it always-loaded. Pull updates from the template repo. -->

# Template Capability Protection

This application was generated from **AspireWebAppTemplate**. It ships production-ready
capabilities that business features build on: authentication (local Identity + optional LDAP),
authorization, user & role management, audit logging, notifications, email/templates,
announcements, page permissions, navigation, shared UI components, and common infrastructure.

## MUST follow

- **Do NOT remove, replace, disable, or significantly restructure any existing template
  capability** merely because a new business requirement does not mention or use it. Absence
  from a business spec is NOT a signal to delete or rewrite template code.
- **Treat existing template code as intentional and in use** unless the user explicitly requests
  its removal, OR an architectural review the user has approved confirms it is obsolete.
- **Extend in place.** When a business feature builds on a template capability (adds a claim, a
  role, a notification category, an email type, a page permission, a nav item), extend the
  existing component; do not fork or rewrite it.
- **Prefer additive changes.** Add new feature folders (`Features/{YourFeature}/`,
  `Services/{YourFeature}/`, new controllers/pages) rather than modifying template files.
- **Stop and ask on conflict.** If a business requirement appears to conflict with a template
  capability or convention, pause and ask the user rather than silently altering template code.

## Precedence (how business vs template guidance interact)

- Business steering (`steering/business/*`) defines **what** to build and the product scope.
- Template steering (`steering/template/*`) defines **how** code is written (structure, naming,
  layering, docs, patterns) and protects existing capabilities.
- These are usually orthogonal: a business requirement says *what*; template conventions say *how*.
  Business scope wins on *what*; template engineering conventions still govern *how*.
- This guardrail wins over any interpretation that would remove or degrade a template capability.
  When genuinely in conflict, **stop and ask** rather than resolve destructively.

## Safeguards beyond this file

- Architecture tests (if present) mechanically enforce layer/feature boundaries — do not weaken
  them to make a change compile.
- `TEMPLATE-ORIGIN.md` (repo root, if present) records the template version this app was created
  from; use it as the reference for "what came from the template."