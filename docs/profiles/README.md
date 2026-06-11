# Deployment Profiles

This template is designed to be deployed in multiple contexts. Each profile contains context-specific documentation that doesn't apply universally:

- **Brand guidelines** — Visual identity, colors, typography for that context
- **Infrastructure** — Auth configuration (LDAP vs. local-only), hosting, SMTP, external services
- **Deployment** — Environment-specific build and deploy procedures

## Available Profiles

| Profile | Context | Key Differences |
|---------|---------|----------------|
| [Jabil](./jabil/) | Corporate internal apps at Jabil | LDAP auth, corporate SMTP, internal server hosting, Jabil branding |
| [Personal](./personal/) | Personal projects, freelance, learning | Local auth only, cloud hosting, custom branding, no LDAP |

## How Profiles Work

The core template (architecture, features, guides) is context-agnostic — it works regardless of which profile is active. Profiles document the **deployment-specific** configuration and branding that varies between contexts.

### Adding a New Profile

If you start a freelance project for a specific client:

```
docs/profiles/client-xyz/
├── brand-guidelines.md    (client's brand specs)
├── infrastructure.md      (client's hosting, auth, services)
└── deployment.md          (client-specific deploy steps)
```

## What Goes Where

| Content | Location | Why |
|---------|----------|-----|
| How MudDataGrid works | `docs/architecture/` | Same regardless of context |
| How to add a feature | `docs/guides/` | Same workflow everywhere |
| Audit Log requirements | `docs/features/` | Same feature in all deployments |
| Jabil Blue color `#003B6B` | `docs/profiles/jabil/` | Only applies to Jabil deployment |
| LDAP settings and flow | `docs/profiles/jabil/` | Jabil uses LDAP; personal doesn't |
| Azure deployment steps | `docs/profiles/personal/` | Only applies to personal hosting |
