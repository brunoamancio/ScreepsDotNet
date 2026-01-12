# Backend Architecture & Workflow

This guide captures the repository conventions, storage architecture, endpoint coverage, and day‑to‑day workflows that previously lived in the root README. Treat it as the “how things work under the hood” companion to the quick-start guide.

## Solution Layout (recap)

- `src/ScreepsDotNet.Backend.Core/` – contracts, DTOs, seeding defaults (`Seeding/SeedDataDefaults.cs`), abstractions for storage/services.
- `src/ScreepsDotNet.Backend.Http/` – ASP.NET Core host exposing `/api/*` plus `.http` scratch files for each route family.
- `src/ScreepsDotNet.Backend.Cli/` – Spectre CLI mirroring legacy scripts (see `docs/cli.md`).
- `src/ScreepsDotNet.Storage.MongoRedis/` – MongoDB/Redis repositories reused by HTTP/CLI/driver layers.
- `src/docker/` + `src/docker-compose.yml` – Mongo/Redis containers and seed scripts.
- `src/native/pathfinder/` – native driver support (see `docs/driver.md`).

## Storage & Seeding Notes

- MongoDB collections follow the legacy schema. Repositories live under `ScreepsDotNet.Storage.MongoRedis.Repositories.*` and use typed POCOs with `[BsonElement]`.
- Seed scripts inside `src/docker/mongo-init` run whenever the `mongo-data` volume is blank:
  - `seed-server-data.js` maintains `/api/server/info` and version metadata.
  - `seed-users.js` provisions `test-user`, `ally-user`, owned rooms, notify prefs, inbox threads, power creeps, controller/spawn docs, and starter history data.
- Testcontainers-based tests reuse the same fixtures via `IntegrationTestHarness`. Update both the JS seeds and `SeedDataDefaults` when schemas change.

### Reset Workflow

```powershell
# Full reset
docker compose -f src/docker-compose.yml down -v
docker compose -f src/docker-compose.yml up -d

# Mongo-only reset
docker volume rm screepsdotnet_mongo-data
docker compose -f src/docker-compose.yml up -d mongo
```

Tail logs with `docker compose -f src/docker-compose.yml logs -f mongo|redis` to verify seeds ran.

## Development Workflow

1. `docker compose -f src/docker-compose.yml up -d` – ensure Mongo/Redis are running (and seeded).
2. `dotnet run --project src/ScreepsDotNet.Backend.Http/ScreepsDotNet.Backend.Http.csproj` – start the HTTP host.
3. `dotnet run --project src/ScreepsDotNet.Backend.Cli/ScreepsDotNet.Backend.Cli.csproj -- --help` – explore CLI commands; use `docs/cli.md` for details.
4. `dotnet test src/ScreepsDotNet.slnx` – run unit + integration suites (requires Docker for Testcontainers).
5. `dotnet format style --severity error --diagnostics IDE0005,IDE0011,IDE0007` to keep lint clean for touched files.

### Manual Smoke Tests

- `GET http://localhost:5210/health`
- `GET http://localhost:5210/api/server/info`
- Use the `.http` files (see [docs/http-endpoints.md](http-endpoints.md)) for user/map/world/system checks or the CLI shortcuts (`./src/cli.sh` / `pwsh ./src/cli.ps1`).

## Coding Standards

- Nullable reference types enabled solution-wide; implicit usings configured via `Directory.Build.props`.
- Prefer `var` when the type is evident; keep explicit types for primitives/enums where clarity matters.
- Use expression-bodied members for single-line methods, collection expressions (`[]`) for empty lists, and `Lock` for synchronization primitives (per driver style guide).
- Shared Analyzer rules live in `.editorconfig`, `.globalconfig`, and `ScreepsDotNet.slnx.DotSettings`. Do not delete these; update them when adding new inspections.
- Mongo repositories should stay typed (`IMongoCollection<TDocument>`). Avoid `BsonDocument` except in migration utilities.
- Integration tests should rely on Testcontainers fixtures rather than the developer’s local Mongo/Redis.

## Feature Coverage Snapshot

The backend currently mirrors most of the legacy Screeps API surface:

- `/health`, `/api/server/info`, `/api/version` return Mongo-backed metadata.
- `/api/user/*` routes cover code branches, memory (paths + segments), console, notifications, badge SVG, respawn, tutorial state, set steam visibility, and profile updates.
- `/api/user/messages/*` (send/list/index/mark-read/unread-count) run through `MongoUserMessageService`, including notification counter updates.
- `/api/register/*` (check-email, check-username, set-username) implement the onboarding flow.
- `/api/game/gen-unique-object-name` / `check-unique-object-name` mirror spawn naming helpers.
- `/api/game/bot/*`, `/api/game/stronghold/*`, `/api/game/system/*`, `/api/game/map/*` expose admin tooling equivalent to the legacy CLI scripts.
- `/api/game/power-creeps/*` provides list/create/delete/cancel-delete/upgrade/rename/experimentation parity.
- `/api/game/add-object-intent` + `/api/game/add-global-intent` queue intents using `MongoIntentService` with legacy validation/transforms.
- `/api/game/world/*` read/write helpers (rooms, stats, overview, map-stats, room-terrain, place-spawn, flag helpers, invader helpers, notify toggles) all support shard-aware identifiers (`shard/Room` or explicit `shard`).
- Market endpoints (`orders-index`, `orders`, `my-orders`, `stats`) scale prices into credits and enforce the legacy query validation rules.

See [docs/http-endpoints.md](http-endpoints.md) for per-route references and `.http` scratch files.

## Repository Conventions & Tips

- Always run `git status` inside `ScreepsDotNet/`; `ScreepsNodeJs/` is a separate repository.
- Stop any running `dotnet run` instances before `dotnet build` to avoid locked DLLs.
- Keep configuration mirrored between `appsettings.json` and `appsettings.Development.json`.
- Update docs (`docs/getting-started.md`, `docs/backend.md`, `docs/http-endpoints.md`, `docs/cli.md`) whenever you add/remove features so future agents don’t have to rediscover behavior.
- When adding a new collection or service:
  1. Define POCOs under `ScreepsDotNet.Storage.MongoRedis.Repositories.Documents`.
  2. Extend seed scripts/Testcontainers fixtures.
  3. Document the behavior in `docs/http-endpoints.md` (or the relevant guide) and add `.http` samples.
