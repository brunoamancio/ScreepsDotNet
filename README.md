# ScreepsDotNet

Modern .NET rewrite of the Screeps private server backend. The solution exposes the legacy HTTP + CLI surface area while we gradually replace the Node.js driver/engine pieces.

> Source code lives under `src/`. If you just cloned the repo, start in [docs/getting-started.md](docs/getting-started.md).

## Repository layout

| Path | Purpose |
| --- | --- |
| `src/` | All .NET projects, docker assets, CLI scripts, and shared props. |
| `docs/` | Specs, badges, and the new developer guides (`getting-started.md`, `cli.md`, plus existing specs). |
| `AGENT.md` | High-level orientation + coding conventions for new contributors. |
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
- **Driver rewrite** – New `ScreepsDotNet.Driver` assemblies (plus native pathfinder) live under `src/`; see driver-specific AGENT notes for progress tracking.

## Documentation map

- [docs/getting-started.md](docs/getting-started.md) – requirements, infra setup, auth flow, tests, seed reset instructions.
- [docs/cli.md](docs/cli.md) – CLI usage, command reference, formatting rules.
- [docs/backend.md](docs/backend.md) – architecture notes, storage/seeding details, dev workflow, coding standards.
- [docs/http-endpoints.md](docs/http-endpoints.md) – route coverage tables + `.http` scratch file index.
- [docs/driver.md](docs/driver.md) – driver rewrite overview with links to subsystem plan docs and AGENT notes.
- [docs/README.md](docs/README.md) – documentation/AGENT ownership map (who updates what).
- [src/ScreepsDotNet.Engine/AGENT.md](src/ScreepsDotNet.Engine/AGENT.md) – engine rewrite execution plan (E1–E8) and current status.
- `docs/specs/*` – market/world API specs and driver design notes.
- `docs/badges/BadgeGallery.md` – generated badge samples.

## Current highlights

- `/api/user/*`, `/api/register/*`, `/api/game/*` (map/world intents, bot/stronghold/system helpers) now run entirely on the .NET backend with parity to the Node server, including shard-aware reads/writes.
- CLI covers storage, world, bot, auth, map, system, and user flows with JSON/markdown/table formatting and shared manifest (`mods.json`) plumbing.
- Native pathfinder binaries are produced per RID and downloaded automatically during driver builds (hash-verified) so developers only need the .NET toolchain.
- Test suites (unit + Testcontainers integration) run via `dotnet test src/ScreepsDotNet.slnx`; CLI smoke tests execute in CI with the same dockerized fixtures.

## Roadmap snapshot

1. **Driver/engine parity** – finish the remaining driver subsystems (runtime sandbox hooks, processor loops, pathfinder integration) and align them with the documented D1–D10 milestones.
2. **Notification & history services** – once bulk writers and queue services are finalized, wire history uploads + notification delivery into the new loops.
3. **Compatibility shims** – expose adapter layers so the legacy engine can talk to the new backend while incremental rewrites land.

For detailed progress per subsystem, see the driver’s `AGENT.md` plus the docs referenced inside each plan file.

## Contributing

- Read `AGENT.md` for repo conventions (implicit usings, lock types, collection expressions, etc.).
- Before filing PRs, run the doc ownership checklist in `docs/README.md` so the right files get updated.
- Keep new documentation in `docs/`; link to it from this README when relevant.
- Prefer Testcontainers-backed integration tests when touching storage-heavy routes; update docker seed scripts so local + CI data stay aligned.
- Before sending PRs, run `dotnet format` for touched files and `dotnet test src/ScreepsDotNet.slnx`.

Need more context? Ping the latest `AGENT.md` entries (root and subsystem-specific) to see active plans and TODOs.
