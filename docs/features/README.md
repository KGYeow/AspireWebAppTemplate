# Feature Specifications

This directory holds feature specifications for the application. It is split by **ownership**,
mirroring the `.kiro/steering/template` vs `.kiro/steering/business` convention, so that template
specs and business specs never mingle and template updates never collide with your own docs.

## Layout

```
docs/features/
├── template/     TEMPLATE-OWNED — inherited; updated only by pulling from the template repo
└── business/     BUSINESS-OWNED — your application's feature specs live here
```

- **`template/`** documents the capabilities that ship with `AspireWebAppTemplate` (auth, audit,
  notifications, roles, permissions, email, announcements, and the architectural refactors). Treat it
  as read-only reference: keep it as-is and pull updates from the template repository. Do not add your
  own feature specs here.
- **`business/`** is where your application's completed feature specs are recorded. It starts empty
  (a `.gitkeep` placeholder) and is never synced from the template.

## What each feature folder contains

Retained specs keep **`design.md`** (how the capability works — architecture, interfaces, data models,
decisions, correctness properties). Cross-cutting capability features additionally keep
**`requirements.md`** (the behavioral contract in EARS form) where that contract has lasting reference
value. Implementation checklists (`tasks.md`) are **not** retained here — they are process artifacts
that live only in `.kiro/specs/` during development.

## How specs get here

The `Sync Spec to Docs` hook (`.kiro/hooks/sync-spec-to-docs.json`) copies a spec's `design.md`
(and `requirements.md`/`bugfix.md` for cross-cutting capabilities) into `business/{feature-name}/`
once all of its tasks are complete. It never copies `tasks.md` and never writes to `template/`.
See `docs/guides/adding-a-feature.md` for the full workflow.
