# Documentation & AGENT Ownership

Use this index to understand which file owns which slice of project knowledge. When you change functionality, update the relevant document(s) listed here.

| Location | Scope / Purpose | Notes |
| --- | --- | --- |
| `README.md` | High-level overview, repo layout, quick-start pointers, links into `docs/` + AGENT files. | Keep concise; defer details to the docs below. |
| `docs/getting-started.md` | Environment requirements, Docker bootstrap, auth flow, test commands, seed reset instructions. | Update when setup steps or prerequisites change. |
| `docs/backend.md` | Architecture/storage notes, dev workflow, restoring data, coding standards, feature snapshot. | Primary reference for backend internals replacing the old README sections. |
| `docs/http-endpoints.md` | Tables of HTTP routes, behaviors, and `.http` scratch files. | Update whenever you add/modify routes or scratch samples. |
| `docs/cli.md` | CLI usage, global switches, command reference, automation tips. | Update when commands/flags change. |
| `docs/driver.md` | Driver rewrite overview + links to per-step plan docs (`src/ScreepsDotNet.Driver/docs/*.md`). | Mirrors the status table from the driver AGENT; update when milestones move. |
| `docs/specs/*` | Deep specs (e.g., market/world APIs). | Keep authoritative descriptions here; cross-reference from backend docs as needed. |
| `AGENT.md` (root) | Repo-wide conventions, orientation, and links to all docs. | Keep short; link to the docs above instead of repeating content. |
| `src/ScreepsDotNet.Driver/AGENT.md` | Day-to-day driver tasks, coding conventions (locks, primary constructors), current TODOs. | Reference `docs/driver.md` for plan context; list actionable items here. |
| `src/native/pathfinder/AGENT.md` | Native build instructions, CI workflow notes, release process for pathfinder binaries. | Mention hash/download requirements covered in driver docs. |

## Update checklist

Run through this list before opening a PR:

1. Identify the area you touched (backend, CLI, HTTP routes, driver, native pathfinder, etc.).
2. Find the owning doc/AGENT in the table above.
3. Update that file (or add a new entry) so the documentation matches your change.
4. If a new scope doesn’t exist yet, add it to the table with a brief description.
5. Mention in your PR summary which docs/AGENTs you updated (or why none were needed).
