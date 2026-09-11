# Template Origin

This repository is **AspireWebAppTemplate** itself (the template), OR a business application
created from it. Use this file to record and track the template relationship.

## For the template repository
This IS the template. Business applications are created by cloning/forking this repo so they
share Git history (which makes later template updates mergeable).

## For a business application created from the template
When you create a business app from this template, record:

- **Template repository:** <url of AspireWebAppTemplate>
- **Created from commit:** <commit SHA you forked/cloned at>
- **Created on:** <date>

### Receiving template updates later
1. Add the template as a read-only remote (once):
   `git remote add template <template-repo-url>`
2. Fetch and review changes: `git fetch template`
3. Bring in improvements selectively (`git cherry-pick <commit>`) or in bulk
   (`git merge template/main`), resolving conflicts where both sides changed the same files.

### Ownership rules
- **Template-owned** (safe to sync from the template): everything under `.kiro/steering/template/`,
  and the template''s engineering code/conventions.
- **Business-owned** (never sync from the template): everything under `.kiro/steering/business/`,
  your business features, and this file''s business section.
- See `.kiro/steering/template/template-guardrails.md` for the capability-protection rule.

---
This template repository is currently at commit: `fd83296d2433e9566b1edd71e728b60c5a8b26b7`