# ScreepsDotNet

Modern .NET rewrite of the Screeps private server backend. The solution exposes the legacy HTTP + CLI surface while running on a fully rewritten managed .NET driver and game simulation engine (no Node.js dependencies).

> Source code lives under `src/`. If you just cloned the repo, start in [docs/getting-started.md](docs/getting-started.md).

## Repository layout

| Path | Purpose |
| --- | --- |
| `src/` | All .NET projects, docker assets, CLI scripts, and shared props. |
| `docs/` | Specs, badges, and the new developer guides (`getting-started.md`, `cli.md`, plus existing specs). |
| `CLAUDE.md` | AI agent context - solution-wide patterns, standards, and workflows. |
| `ScreepsNodeJs/` | The legacy server (kept for parity checks). |

## Quick start (TL;DR)

1. Install .NET 10 SDK + Docker Desktop + PowerShell/Bash.
2. `docker compose -f src/docker-compose.yml up -d` to launch Mongo 7 + Redis 7 with seed data.
3. `dotnet run --project src/ScreepsDotNet.Backend.Http/ScreepsDotNet.Backend.Http.csproj` to run the HTTP host.
4. Use `docs/getting-started.md` for authentication steps, `.http` scratch files, and the local dev workflow.
5. Need the CLI? Jump to [docs/cli.md](docs/cli.md) for commands, global switches, and automation tips.

## Key components

- **Backend HTTP host** – ASP.NET Core service covering `/health`, `/api/server/info`, `/api/user/*`, admin/bot/map/system flows, and shard-aware world endpoints. Scratch `.http` files under `src/ScreepsDotNet.Backend.Http/` mirror every route.
- **CLI (`ScreepsDotNet.Backend.Cli`)** – Spectre-based tooling that mirrors the legacy scripts (storage, world, bots, map, auth, etc.) with consistent formatting (`--format table|markdown|json`).
- **Storage adapters** – `ScreepsDotNet.Storage.MongoRedis` supplies MongoDB/Redis repositories shared by HTTP + CLI + driver layers.
- **Driver (`ScreepsDotNet.Driver`)** – Complete rewrite of Node.js driver (D1-D10 ✅): queues, ClearScript/V8 runtime, bulk writers, native pathfinder, history/notifications, and engine contracts.
- **Engine (`ScreepsDotNet.Engine`)** – Managed .NET game simulation engine (E1-E6 ✅): 11 intent handler families (240 tests), validation pipeline (96 tests), global systems, loop orchestration. Production-ready and exclusively handles all tick processing.

## Documentation map

### For AI Agents
- [CLAUDE.md](CLAUDE.md) – AI-optimized context with code patterns, common tasks, and self-contained workflows.

### For Human Developers
- [docs/getting-started.md](docs/getting-started.md) – requirements, infra setup, auth flow, tests, seed reset instructions.
- [docs/cli.md](docs/cli.md) – CLI usage, command reference, formatting rules.
- [docs/backend.md](docs/backend.md) – HTTP API feature coverage and smoke tests.
- [docs/http-endpoints.md](docs/http-endpoints.md) – route coverage tables + `.http` scratch file index.
- [docs/driver.md](docs/driver.md) – driver rewrite overview with links to subsystem plan docs (D1-D10 complete ✅).
- [src/ScreepsDotNet.Driver/CLAUDE.md](src/ScreepsDotNet.Driver/CLAUDE.md) – driver AI context (code patterns, common tasks, D1-D10 roadmap complete ✅).
- [src/ScreepsDotNet.Engine/CLAUDE.md](src/ScreepsDotNet.Engine/CLAUDE.md) – engine AI context (E1-E6 complete ✅, NEVER direct DB patterns, intent handlers).
- [docs/engine/roadmap.md](docs/engine/roadmap.md) – engine roadmap tracking (E1-E6 complete ✅, E7-E9 pending).
- [src/native/pathfinder/CLAUDE.md](src/native/pathfinder/CLAUDE.md) – pathfinder AI context (cross-platform builds, parity testing, CI/CD, P/Invoke).
- [docs/README.md](docs/README.md) – documentation ownership map (who updates what).
- `docs/specs/*` – market/world API specs and driver design notes.
- `docs/badges/BadgeGallery.md` – generated badge samples.

## Current highlights

- `/api/user/*`, `/api/register/*`, `/api/game/*` (map/world intents, bot/stronghold/system helpers) now run entirely on the .NET backend with parity to the Node server, including shard-aware reads/writes.
- CLI covers storage, world, bot, auth, map, system, and user flows with JSON/markdown/table formatting and shared manifest (`mods.json`) plumbing.
- Native pathfinder binaries are produced per RID and downloaded automatically during driver builds (hash-verified) so developers only need the .NET toolchain.
- Test suites (unit + Testcontainers integration) run via `dotnet test src/ScreepsDotNet.slnx`; CLI smoke tests execute in CI with the same dockerized fixtures.

## Roadmap snapshot

**Driver & Engine Status (January 2026):**
- ✅ **Driver complete (D1-D10)** – All driver subsystems operational (queues, sandbox, bulk writers, pathfinder, history, engine contracts)
- ✅ **Engine operational (E1-E6)** – Managed .NET Engine is production-ready and required in all driver loops
  - E1-E2: Data model & storage contracts (11 handler families, 240 tests)
  - E3: Intent validation pipeline (5 validators, 96 tests)
  - E4: Simulation kernel (passive regeneration, 20 processor steps)
  - E5: Global systems (user stats, keeper lairs, nuker operations)
  - E6: Loop orchestration (Engine exclusively handles all processing)
- 📋 **Next milestones:**
  - E7: Compatibility & parity validation (lockstep testing vs Node.js engine)
  - E8: Observability & tooling (metrics, diagnostics, operator playbooks)
  - E9: NPC AI logic (keeper/invader pathfinding and combat)

For detailed progress, see [docs/engine/roadmap.md](docs/engine/roadmap.md) and [docs/driver.md](docs/driver.md).

## Contributing

- Read `CLAUDE.md` for repo conventions (implicit usings, lock types, collection expressions, etc.).
- Before filing PRs, run the doc ownership checklist in `docs/README.md` so the right files get updated.
- Keep new documentation in `docs/`; link to it from this README when relevant.
- Prefer Testcontainers-backed integration tests when touching storage-heavy routes; update docker seed scripts so local + CI data stay aligned.
- Before sending PRs, run `dotnet format` for touched files and `dotnet test src/ScreepsDotNet.slnx`.

Need more context? Check `CLAUDE.md` entries (root and subsystem-specific) to see active plans and TODOs.
