# Adding a Feature

## Workflow

This project uses spec-driven development. Every feature follows:

1. **Requirements** → Define what it does (user stories + acceptance criteria)
2. **Design** → Define how it's built (architecture, interfaces, correctness properties)
3. **Tasks** → Ordered implementation plan with dependency graph
4. **Implement** → Execute tasks, write property tests, verify build
5. **Document** → Retain the spec's design (and requirements, for cross-cutting capabilities) under `docs/features/business/`

## Using Kiro Specs

Start a new spec session in Kiro and describe the feature. Walk through:
- Requirements gathering (EARS format acceptance criteria)
- Technical design (interfaces, data models, error handling)
- Task generation (ordered, with dependency waves)

Spec files live at `.kiro/specs/{feature-name}/` during development.

## After Implementation

When all tasks are complete, the `Sync Spec to Docs` hook copies the retained documents into
`docs/features/business/{feature-name}/` automatically. If you document manually, follow the same rules:

1. Copy `design.md` to `docs/features/business/{feature-name}/`.
2. Also copy `requirements.md` (or `bugfix.md`) **only** for cross-cutting/reusable capabilities whose
   behavioral contract has lasting reference value; skip it for small, narrowly-scoped features.
3. Do **not** copy `tasks.md` — implementation checklists are not retained in `docs/features`.
4. Business specs go under `docs/features/business/`; never write to `docs/features/template/` (that is
   template-owned — see `docs/features/README.md`).
5. Update the `docs/README.md` feature table if the feature is a notable capability.

## Feature Folder Structure

```
docs/features/business/{feature-name}/
├── design.md          — How it works (always retained)
└── requirements.md    — What it does (retained for cross-cutting capabilities)
```

## Conventions

- Feature names use kebab-case: `audit-log`, `user-management`
- All requirements use EARS format (WHEN/IF/THEN/THE/SHALL)
- All designs include correctness properties for PBT
- All tasks reference specific requirement numbers
- Task status: `[x]` complete, `[ ]` pending
