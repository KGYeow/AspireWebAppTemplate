# Steering Strategy (Template vs Business)

Kiro steering files guide how Kiro works in this repository. Because the template and each
business application live in **separate Git repositories** (template -> clone/fork -> business app),
steering is split by **ownership** so template conventions stay protected while business context
can be added and evolve independently.

## Layout

```
.kiro/steering/
+-- README.md                 (inclusion: manual - human-facing map, not injected into context)
+-- template/                 TEMPLATE-OWNED (inherited; pull updates from the template repo)
|   +-- coding-standards.md
|   +-- api-patterns.md
|   +-- structure.md
|   +-- tech.md
|   +-- ui-patterns.md
|   +-- project-context.md
|   +-- template-guardrails.md   (capability-protection rule)
+-- business/                 BUSINESS-OWNED (this app edits; never synced from the template)
    +-- product.md               (business replaces the template seed)
    +-- business-context.md      (domain rules, integrations, project conventions/workflows)
```

## Ownership model

- **Template-owned (`template/`)** = engineering conventions and the capability guardrail. A business
  app normally keeps these unchanged and pulls updates from the template repository. They describe
  **how** code is written and protect existing template capabilities.
- **Business-owned (`business/`)** = product identity and domain/business context for THIS app.
  The business app owns and edits these; they are **never** synced from the template.

## Precedence

- Business steering defines **what** to build and the product scope.
- Template steering defines **how** code is written and protects existing capabilities.
- They are usually orthogonal (what vs how). Business scope wins on *what*; template engineering
  conventions still govern *how*.
- `template/template-guardrails.md` wins over any interpretation that would remove or degrade a
  template capability. On genuine conflict, Kiro stops and asks rather than resolving destructively.

## Inclusion modes

Every steering file declares an `inclusion` mode in YAML front-matter:

- `always` - loaded every interaction (universal rules + the guardrail).
- `fileMatch` - loaded only when a matching file is in context (e.g., `**/*.razor` for UI rules).
- `auto` - loaded on demand when the request matches the file description.
- `manual` - loaded only when referenced with `#` (used by the steering `README.md`).

Keep universal rules and the guardrail `always`; as business steering grows, push situational
context to `auto`/`fileMatch` to control token cost.

## Lifecycle & template updates

1. **Template dev** - the template evolves its `template/*` steering.
2. **Business app created** - clone/fork the template (shared history). Inherits `template/*` and
   the `business/*` seeds. Record the origin in `TEMPLATE-ORIGIN.md`.
3. **Business steering added** - the team fills in `business/*`; it does not edit `template/*`.
4. **Business evolves independently** - business steering grows; template steering stays put.
5. **Template improves** - new/updated `template/*` files upstream.
6. **Incorporate updates** - `git fetch template` then cherry-pick/merge. Because template steering
   lives under `template/` and business steering under `business/`, updates touch only `template/*`
   and do not conflict with business steering. Never sync `business/*`.

## The capability-protection rule (why it exists)

A business developer may ask Kiro to implement a feature whose spec does not mention template
capabilities (auth, audit, notifications, etc.). Without a guardrail, Kiro could wrongly treat that
silence as permission to remove or rewrite "unused" template code. `template/template-guardrails.md`
makes the rule explicit: do not remove/replace/restructure template capabilities unless the user
explicitly requests it or an approved architectural review confirms obsolescence; extend in place;
prefer additive changes; stop and ask on conflict. Back it with architecture tests where possible.