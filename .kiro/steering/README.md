---
inclusion: manual
---

# Kiro Steering Layout

Steering files guide Kiro. They are split by **ownership** so template updates and business
context can evolve independently.

## Folders

- `template/` — **Template-owned** engineering conventions inherited from AspireWebAppTemplate.
  Business apps normally KEEP these unchanged and pull updates from the template repo.
  - `coding-standards.md`, `api-patterns.md`, `structure.md`, `tech.md`, `ui-patterns.md`,
    `project-context.md` — how code is written and organized.
  - `template-guardrails.md` — the capability-protection rule (do not delete/alter template
    capabilities without an explicit request or approved review).
- `business/` — **Business-owned** context for THIS application. The business app edits these.
  - `product.md` — what the product is / who it is for (business REPLACES the template seed).
  - `business-context.md` — domain rules, integrations, project-specific conventions/workflows.
  - Add more as needed (e.g., per-domain files with `inclusion: auto` or `fileMatch`).

## Inclusion modes (in each file''s front-matter)

- `always` — loaded into every interaction (use for universal rules).
- `fileMatch` — loaded only when a matching file is in context (e.g., `**/*.razor` for UI rules).
- `auto` — loaded on demand when the request matches the file''s description.
- `manual` — loaded only when referenced with `#` in chat.

Keep universal rules and the guardrail `always`; push situational business context to
`auto`/`fileMatch` as the business steering grows, to control token cost.

## Precedence

- Business steering defines **what** to build and the product scope.
- Template steering defines **how** code is written and protects existing capabilities.
- The guardrail wins over any interpretation that would remove/degrade a template capability;
  on genuine conflict, Kiro should stop and ask.

## Template updates (two-repo lifecycle)

The template and each business app live in separate Git repos. Because template steering lives
under `template/` and business steering under `business/`, pulling template updates
(`git merge template/main` or cherry-pick) touches only `template/*` and does not conflict with
business steering. Never sync `business/*` from the template.