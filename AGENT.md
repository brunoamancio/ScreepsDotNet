# AGENT GUIDE

## Repository Layout

- `ScreepsNodeJs/` – original Node.js Screeps server (git repo moved inside); untouched except for reference.
- `ScreepsDotNet/` – new .NET solution containing:
- `ScreepsDotNet.Backend.Core/` – cross-cutting contracts (configuration, models, repositories, services).
- `ScreepsDotNet.Backend.Http/` – ASP.NET Core Web API host (currently health + server info endpoints).
- `ScreepsDotNet.Backend.Cli/` – .NET console host with shared DI wiring; configuration comes from CLI arguments/environment (no appsettings dependency) and future commands will call the same repositories/services outside HTTP.
- `ScreepsDotNet.Storage.MongoRedis/` – MongoDB/Redis infrastructure (adapter + repositories) used by the HTTP host.
  - `.editorconfig`, `.globalconfig`, `.gitattributes`, `Directory.Build.props` – shared tooling settings.
  - `docker/` – supporting assets (Mongo init scripts, etc.).
  - `docker-compose.yml` – spins up MongoDB + Redis for local dev.

## Current Features

- `/health` – ASP.NET health checks with custom JSON output (Mongo/Redis probe).
- `/api/server/info` – reads metadata from Mongo `serverData` (seeded automatically).
- `/api/user/*` (branches, code, memory, console, notify prefs, badge SVG, respawn) – wired to Mongo/Redis repositories (`MongoUserCodeRepository`, `MongoUserMemoryRepository`, `MongoUserConsoleRepository`, `MongoUserRespawnService`, `MongoUserWorldRepository`) with the same semantics as the legacy Screeps backend.
- `/api/user/badge`, `/api/user/email`, `/api/user/set-steam-visible` – newly implemented profile management endpoints writing to the `users` collection with the same validation rules as the Node server.
- `/api/game/market/*` – parity routes for `orders-index`, `orders`, `my-orders`, and `stats` backed by typed repositories and DTO factories that scale prices (thousandths → credits) and enforce query validation.
- `/api/game/*` world endpoints – `map-stats`, `room-status`, `room-terrain`, `rooms`, `world-size`, `time`, `tick` implemented with Mongo-backed repositories, DTO factories, deterministic seeds (docker + Testcontainers), and HTTP scratch files for quick smoke testing.
- Core abstractions defined for server info, users, rooms, CLI sessions, storage status, and engine ticks.
- Mongo repositories implemented for server info, users, and owned rooms; ready for future endpoints.
- Integration tests spin up disposable Mongo + Redis containers via Testcontainers to validate real storage behavior.

## Local Development Workflow

1. **Dependencies:** .NET 10 SDK, Docker Desktop (for Mongo/Redis), PowerShell.
2. **Start infrastructure (also seeds Mongo/Redis):**  
   ```powershell
   cd ScreepsDotNet
   docker compose up -d
   ```
   - Mongo 7.0 (`localhost:27017`) + Redis 7.2 (`localhost:16379`).
   - Every `docker compose up` seeds:
  - `docker/mongo-init/seed-server-data.js` – `/api/server/info` + `/api/version.serverData` fixture data.
  - `docker/mongo-init/seed-users.js` – canonical `test-user` profile, owned rooms, sample notify prefs, etc., for exercising `/api/user/*` routes.
3. **Run backend:**  
   ```powershell
   dotnet run --project ScreepsDotNet.Backend.Http/ScreepsDotNet.Backend.Http.csproj
   ```
4. **Run automated tests:**  
   ```powershell
   dotnet test
   ```
   - Unit tests swap repositories with fakes (fast, hermetic).
   - Integration tests (also under `ScreepsDotNet.Backend.Http.Tests`) spin up Mongo + Redis containers via Testcontainers; keep Docker Desktop running.
5. **Keep the repo lint-clean:** run `dotnet format style --severity error --diagnostics IDE0005,IDE0011,IDE0007` (or equivalent Rider/Roslyn analysis) during development. `dotnet format --verify-no-changes` currently fails with upstream `CHARSET` warnings on untouched files, so capture the failure output in your report instead of trying to re-encode the entire repo. Fix unused `using`s, redundant braces, `var` style issues, and any reported IDE warnings in the files you touch so we don’t leave style violations for the next person.
6. **Manual smoke tests:**  
   - `GET http://localhost:5210/health`
   - `GET http://localhost:5210/api/server/info`
   - `ScreepsDotNet.Backend.Http/MarketEndpoints.http` + `WorldEndpoints.http` contain ready-to-send requests for every market/world route once the backend is running.
7. **Build:** ensure no running `dotnet run` locks DLLs before invoking `dotnet build`.

### Resetting / Updating Seed Data

- All Mongo scripts inside `docker/mongo-init` run only when the container initializes an empty volume.
- `docker/mongo-init/seed-server-data.js` keeps the canonical server metadata document (`server.data` collection) in sync with the legacy backend defaults (`welcomeText`, socket throttles, renderer metadata).
- `docker/mongo-init/seed-users.js` inserts/updates the canonical `test-user` record plus sample controller/spawn objects (`rooms.objects`) and a short credit history in `users.money` so `/api/user/money-history` has data. The newer HTTP routes (code/memory/console) lazily create their own per-user documents once you hit them.
- When schemas or seed files change, do a clean reset so everyone picks up the new baseline:
  ```powershell
  docker compose down -v      # stops containers and removes mongo-data / redis-data volumes
  docker compose up -d        # recreates services and reruns all init scripts
  docker compose logs -f mongo  # optional: verify each seed script finished without errors
  ```
- To tweak seed data locally without nuking Redis, remove only the Mongo volume:
  ```powershell
  docker volume rm screepsdotnet_mongo-data
  docker compose up -d mongo
  ```
- Document any new collections in both a seed script and this section so future agents know how to refresh their environment quickly.

## Configuration

- `appsettings.json` & `appsettings.Development.json`:
  - `VersionInfo` and `ServerData` configure `/api/version` responses.
  - `Storage:MongoRedis` connection strings + collection names.
- `docker-compose.yml` uses volumes `mongo-data` / `redis-data`. Run `docker compose down -v` to reseed.

## Coding Standards

- Nullable reference types enabled solution-wide.
- `.editorconfig` enforces strict code style (ReSharper settings check in `.DotSettings`).
- Health and endpoint logic extracted into dedicated classes to keep `Program.cs` minimal.
- Constants used for repeated strings (routes, content types, field names).
- JetBrains Rider/ReSharper rules are stored in `ScreepsDotNet.slnx.DotSettings` (solution-level). Keep this file updated when adding new inspections; don’t delete it since it enforces shared inspection severity.
- Tests now share helper modules under `ScreepsDotNet.Backend.Http.Tests`:
  - `TestSupport/RepositoryPathHelper.cs` – locate repo root when tests need to emit artifacts (badge gallery, etc.).
  - `Rendering/Helpers/BadgeSampleFactory.cs` – builds consistent badge payloads (numeric + custom samples) and exposes the `BadgeSample`/`BadgePayload` types.
  - `Rendering/Helpers/BadgeGalleryMarkdownBuilder.cs` – generates the markdown table stored in `docs/badges/BadgeGallery.md`.
  Reuse these helpers instead of re-serializing badges or duplicating file-system logic in new tests.
- Mongo access now goes through typed POCOs under `ScreepsDotNet.Storage.MongoRedis.Repositories.Documents`. When you add a new collection:
  1. Create a document type with `[BsonElement]` attributes (and `[BsonIgnoreExtraElements]` when needed).
  2. Point the repository at `IMongoCollection<TDocument>`; do **not** fall back to `BsonDocument`.
  3. Extend the integration harness seeders so tests have representative data.
- `Extensions/DictionaryPathExtensions.cs` and `Extensions/JsonElementExtensions.cs` provide the only conversion helpers we need (for user memory path mutations). Reuse them instead of reintroducing BSON utilities.
- User integration tests (`ScreepsDotNet.Backend.Http.Tests/Integration`) seed Mongo via `IntegrationTestHarness`. If you add an endpoint or collection, update the harness so every scenario has deterministic data and extend `UserEndpointsIntegrationTests` accordingly.
- User API smoke tests live in `ScreepsDotNet.Backend.Http/UserEndpoints.http`. Always update this file (and `ScreepsDotNet/README.md`) whenever you add/rename an endpoint so new agents can exercise the API immediately.

## Pending / Next Steps

1. **CLI Command Framework & Utilities**
   - Build out `ScreepsDotNet.Backend.Cli` beyond the new host scaffold: add a command framework plus verbs for reseeding storage, inspecting users, dumping world metadata, and scripting regression workflows.
2. **Write-heavy `/api/game/*` routes**
   - Implement spawn placement, construction/flag intents, notify toggles, and invader management per the remainder of `docs/specs/MarketWorldEndpoints.md`. These depend on the room/world repositories we just added—extend docker + Testcontainers seeds before adding the endpoints/tests.
3. **Server info provider parity**
   - Replace the remaining in-memory providers (e.g., `VersionInfoProvider` caching) with storage-backed equivalents so `/api/version` and `/api/server/info` always reflect Mongo state, then remove duplicated configuration blocks.

## Market & World API Spec Snapshot

- `docs/specs/MarketWorldEndpoints.md` remains the canonical reference for legacy behavior, Mongo schemas, .NET repository contracts, and the integration test matrix. The read-model sections (market + world) are now implemented; treat the remaining chapters as the backlog for write-heavy routes.
- Scope priorities (in order): CLI tooling + regression scaffolding, then the remaining write-heavy endpoints (spawn placement, flags, intents) once we have deterministic seeds/tests for those mutations.

## Tips for Agents

- Run `git status` inside `ScreepsDotNet` repo, not project root; `ScreepsNodeJs` is a separate git directory.
- Stop any running backend (`dotnet run`) before building to avoid locked assemblies.
- Use `docker compose logs -f mongo|redis` for debugging local data issues.
- Keep new config sections mirrored between default and development appsettings.
