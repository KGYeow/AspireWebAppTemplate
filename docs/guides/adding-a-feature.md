# Adding a Feature

## Workflow

This project uses spec-driven development. Every feature follows:

1. **Requirements** → Define what it does (user stories + acceptance criteria)
2. **Design** → Define how it's built (architecture, interfaces, correctness properties)
3. **Tasks** → Ordered implementation plan with dependency graph
4. **Implement** → Execute tasks, write property tests, verify build
5. **Document** → Copy spec to `docs/features/`, log session in `docs/logs/`

## Using Kiro Specs

Start a new spec session in Kiro and describe the feature. Walk through:
- Requirements gathering (EARS format acceptance criteria)
- Technical design (interfaces, data models, error handling)
- Task generation (ordered, with dependency waves)

Spec files live at `.kiro/specs/{feature-name}/` during development.

## After Implementation

1. Copy final spec files to `docs/features/{feature-name}/`
2. Add a session entry to `docs/logs/sessions.md`
3. Update `docs/README.md` feature table

## Feature Folder Structure

```
docs/features/{feature-name}/
├── requirements.md    — What it does
├── design.md          — How it works
└── tasks.md           — Implementation history
```

## Conventions

- Feature names use kebab-case: `audit-log`, `user-management`
- All requirements use EARS format (WHEN/IF/THEN/THE/SHALL)
- All designs include correctness properties for PBT
- All tasks reference specific requirement numbers
- Task status: `[x]` complete, `[ ]` pending
