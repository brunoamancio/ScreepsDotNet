# Documentation & AGENT Ownership

Use this index to understand which file owns which slice of project knowledge. When you change functionality, update the relevant document(s) listed here.

| Location | Scope / Purpose | Notes |
| --- | --- | --- |
| `README.md` | High-level overview, repo layout, quick-start pointers, links into `docs/` + context files. | Keep concise; defer details to the docs below. |
| `CLAUDE.md` | AI agent context - solution-wide patterns, coding standards (with ✅/❌ examples), common tasks, storage architecture, workflows. | AI-optimized with inline examples and self-contained context. Primary reference for AI agents. |
| `docs/getting-started.md` | Environment requirements, Docker bootstrap, auth flow, test commands, seed reset instructions. | Update when setup steps or prerequisites change. |
| `docs/backend.md` | HTTP API endpoint feature coverage and smoke tests. | Solution-wide architecture/storage/workflow now lives in root `CLAUDE.md`. |
| `docs/http-endpoints.md` | Tables of HTTP routes, behaviors, and `.http` scratch files. | Update whenever you add/modify routes or scratch samples. |
| `docs/cli.md` | CLI usage, global switches, command reference, automation tips. | Update when commands/flags change. |
| `docs/driver.md` | Driver rewrite overview + links to per-step plan docs (`src/ScreepsDotNet.Driver/docs/*.md`). | Mirrors the status table from the driver AGENT; update when milestones move. |
| `docs/specs/*` | Deep specs (e.g., market/world APIs). | Keep authoritative descriptions here; cross-reference from backend docs as needed. |
| `src/ScreepsDotNet.Driver/CLAUDE.md` | Driver subsystem AI context - D1-D10 roadmap, code patterns (✅/❌), common tasks (add processor, wire telemetry), integration contracts. | AI-optimized with inline examples. |
| `src/ScreepsDotNet.Engine/CLAUDE.md` | Engine subsystem AI context - E1-E8 roadmap, 🚨 NEVER direct DB patterns, intent handler examples, parity testing, E2.3 active work. | AI-optimized. Critical: Engine NEVER accesses Mongo/Redis directly (use Driver). |
| `src/native/pathfinder/CLAUDE.md` | Native pathfinder AI context - cross-platform builds (all RIDs), parity testing (100% Node.js match), baseline refresh, CI/CD, P/Invoke patterns. | AI-optimized with build/test workflows. |

## Update checklist

Run through this list before opening a PR:

1. Identify the area you touched (backend, CLI, HTTP routes, driver, native pathfinder, etc.).
2. Find the owning doc/AGENT in the table above.
3. Update that file (or add a new entry) so the documentation matches your change.
4. If a new scope doesn’t exist yet, add it to the table with a brief description.
5. Mention in your PR summary which docs/AGENTs you updated (or why none were needed).
