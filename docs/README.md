# Documentation & AGENT Ownership

Use this index to understand which file owns which slice of project knowledge. When you change functionality, update the relevant document(s) listed here.

| Location | Scope / Purpose | Notes |
| --- | --- | --- |
| `README.md` | High-level overview, repo layout, quick-start pointers, links into `docs/` + context files. | Keep concise; defer details to the docs below. |
| `CLAUDE.md` | AI agent context - solution-wide patterns, coding standards (with ✅/❌ examples), common tasks, workflows. | AI-optimized with inline examples and self-contained context. Primary reference for AI agents. Storage architecture moved to `docs/storage/`. |
| `docs/getting-started.md` | Environment requirements, Docker bootstrap, auth flow, test commands, seed reset instructions. | Update when setup steps or prerequisites change. |
| `docs/common-tasks.md` | Cross-cutting how-to guides spanning multiple subsystems. | Update when adding new common workflows. |
| **Backend Subsystem** | | |
| `docs/backend/overview.md` | HTTP API endpoint feature coverage and smoke tests. | Moved from `docs/backend.md`. Entry point for backend docs. |
| `docs/backend/http-api.md` | Tables of HTTP routes, behaviors, and `.http` scratch files. | Moved from `docs/http-endpoints.md`. Update whenever you add/modify routes. |
| `docs/backend/cli.md` | CLI usage, global switches, command reference, automation tips. | Moved from `docs/cli.md`. Update when commands/flags change. |
| `docs/backend/specs/*.md` | Deep specs (e.g., market/world APIs). | Moved from `docs/specs/`. Keep authoritative descriptions here. |
| **Driver Subsystem** | | |
| `docs/driver/roadmap.md` | Driver roadmap (D1-D10) with milestone overview table. | NEW. Replaced `docs/driver.md`. Entry point for driver docs. All D1-D10 complete ✅. |
| `docs/driver/d1-d10.md` | Individual milestone design docs (D1 through D10). | Moved from `src/ScreepsDotNet.Driver/docs/`. Renamed to milestone pattern. |
| `src/ScreepsDotNet.Driver/CLAUDE.md` | Driver subsystem AI context - code patterns (✅/❌), common tasks, integration contracts. | AI-optimized with inline examples. References `docs/driver/` for design docs. |
| **Engine Subsystem** | | |
| `docs/engine/roadmap.md` | Engine roadmap tracking (E1-E9) - milestone status, test counts, deferred features. | Human-readable roadmap. Update when milestones complete. E1-E6 complete ✅, E7-E9 pending. |
| `docs/engine/e1-e9.md` | Individual milestone design docs (E1 through E9). | Detailed handler breakdown with test counts and parity notes. |
| `docs/engine/data-model.md` | Engine data contracts design - Driver↔Engine boundary, DTOs, deferred features. | Reference for E2 implementation. All features complete except non-parity-critical deferrals. |
| `src/ScreepsDotNet.Engine/CLAUDE.md` | Engine subsystem AI context - 🚨 NEVER direct DB patterns, intent handler examples, parity testing. | AI-optimized. Critical: Engine NEVER accesses Mongo/Redis directly (use Driver). E1-E6 complete ✅. |
| **Storage Subsystem** | | |
| `docs/storage/overview.md` | Storage architecture, MongoDB collections, Redis keys, connection management. | NEW. Extracted from CLAUDE.md. Entry point for storage docs. |
| `docs/storage/mongodb.md` | MongoDB patterns, collection schemas, repository patterns, bulk writers. | NEW. Detailed MongoDB reference. |
| `docs/storage/redis.md` | Redis patterns, queue patterns, caching strategies, pub/sub. | NEW. Detailed Redis reference. |
| `docs/storage/seeding.md` | Seed data patterns, test fixtures, SeedDataDefaults.cs, reset workflows. | NEW. Seed data reference. |
| **Other** | | |
| `docs/assets/badges/gallery.md` | Badge gallery (presentational). | Moved from `docs/badges/BadgeGallery.md`. |
| `docs/assets/templates/claude-md-subsystem.md` | Template for subsystem CLAUDE.md files. | Moved from `docs/claude-md-subsystem-template.md`. |
| `src/native/pathfinder/CLAUDE.md` | Native pathfinder AI context - cross-platform builds, parity testing, CI/CD, P/Invoke patterns. | AI-optimized with build/test workflows. |

## Update checklist

Run through this list before opening a PR:

1. Identify the area you touched (backend, CLI, HTTP routes, driver, native pathfinder, etc.).
2. Find the owning doc/AGENT in the table above.
3. Update that file (or add a new entry) so the documentation matches your change.
4. If a new scope doesn’t exist yet, add it to the table with a brief description.
5. Mention in your PR summary which docs/AGENTs you updated (or why none were needed).
