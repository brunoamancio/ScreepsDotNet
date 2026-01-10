# AGENT GUIDE

## Repository Layout

- `ScreepsNodeJs/` – original Node.js Screeps server (git repo moved inside); untouched except for reference.
- `ScreepsDotNet/` – new .NET solution containing:
- `ScreepsDotNet.Backend.Core/` – cross-cutting contracts (configuration, models, repositories, services).
- `ScreepsDotNet.Backend.Http/` – ASP.NET Core Web API host (currently health + server info endpoints).
- `ScreepsDotNet.Backend.Cli/` – .NET console host running on Spectre.Console.Cli; configuration comes from CLI arguments/environment (no appsettings dependency). The root command accepts the legacy `--db`, `--connection-string`, `--cli_host`, etc., switches. Current commands:
  - `version [--json]`
  - `storage status [--json]`
  - `storage reseed --confirm RESET [--force]` (calls the shared Mongo reseeder; `--force` required for non-default DBs)
  - `user show (--user-id <id> | --username <name>) [--json]`
  - `user console --user-id <id> --expression "<code>" [--hidden]`
  - `user memory get --user-id <id> [--segment <0-99>] [--json]`
  - `user memory set --user-id <id> (--value <json> [--path <path>] | --segment <0-99> --data <text>)`
  - `world dump --room <name> [--decoded] [--json]`
  - `world stats --room <name> [--stat owners1] [--json]`
  - `world overview --room <name> [--json]`
  - `system status|pause|resume|message|reset`
  - `system tick get [--json]`
  - `system tick set --ms <milliseconds>`
  - `bots list|spawn|reload|remove`
  - `strongholds templates|spawn|expand` (all accept either `shard/Room` syntax or `--shard <name>` just like the HTTP endpoints).
  - `flag create|change-color|remove`
  - `invader create|remove`
  - `auth issue|resolve|token-list|revoke`
- Implementation rules:
  - Derive every verb from `CommandHandler<TSettings>` (for logging + cancellation) and always request `ICommandOutputFormatter` in the constructor. `OutputFormatter` exposes `WriteJson`, `WriteTable`, `WriteKeyValueTable`, `WriteMarkdownTable`, `WriteLine`, and `WriteMarkupLine`; direct `AnsiConsole` calls are no longer allowed.
  - Commands that support `--json` should short-circuit by calling `OutputFormatter.WriteJson(...)` before any table rendering, matching the behavior documented in README.
- Every CLI verb that accepts a room identifier now shares the same shard-aware parser as the HTTP surface: operators can specify either the legacy inline form (`shard2/W20N20`) or pass `--shard shard2` alongside the room name. The parser trims/uppercases room names, rejects malformed identifiers, and keeps CLI + HTTP behavior in sync.
- `ScreepsDotNet.Storage.MongoRedis/` – MongoDB/Redis infrastructure (adapter + repositories) used by the HTTP host.
  - `.editorconfig`, `.globalconfig`, `.gitattributes`, `Directory.Build.props` – shared tooling settings.
  - `docker/` – supporting assets (Mongo init scripts, etc.).
  - `docker-compose.yml` – spins up MongoDB + Redis for local dev.

## Current Features

- `/health` – ASP.NET health checks with custom JSON output (Mongo/Redis probe).
- `/api/server/info` – reads metadata from Mongo `serverData` (seeded automatically).
- `/api/user/*` (branches, code, memory, console, notify prefs, badge SVG, respawn, tutorial done, set steam visible) – wired to Mongo/Redis repositories (`MongoUserCodeRepository`, `MongoUserMemoryRepository`, `MongoUserConsoleRepository`, `MongoUserRespawnService`, `MongoUserWorldRepository`, `MongoUserRepository`) with the same semantics as the legacy Screeps backend.
- `/api/user/badge`, `/api/user/email`, `/api/user/set-steam-visible`, `/api/user/notify-prefs` – implemented profile management and preference endpoints writing to the `users` collection with the same validation rules and parity logic as the Node server.
- `/api/user/messages/*` (send, list, index, mark-read, unread-count) – powered by `MongoUserMessageService`, which persists conversations in `users.messages`, mirrors legacy validation (string payload + 100 KB cap, respondent lookup), and upserts notification counters in `users.notifications` before notifying offline players.
- `/api/register/*` (check-email, check-username, set-username) – mirrors the onboarding flow so the official client can validate credentials and claim a username/email without falling back to the Node backend.
- `/api/game/gen-unique-object-name` + `/api/game/check-unique-object-name` – expose the legacy spawn naming helpers via `MongoObjectNameService`, so the official client can suggest/validate first-spawn names without hitting the Node server.
- Shard regression coverage: `BotEndpointsIntegrationTests.RemoveBot_ShardsRemainIsolated`, `StrongholdEndpointsIntegrationTests.Expand_WithShardTargetsMatchingCore`, `IntentEndpointsIntegrationTests.AddObjectIntent_WithShard_PersistsShardScopedDocument`, and the new `MapEndpointsIntegrationTests.OpenRoom_WithShard_DoesNotAffectOtherShard` ensure write-heavy routes mutate only the targeted shard documents.
- `/api/game/market/*` – parity routes for `orders-index`, `orders`, `my-orders`, and `stats` backed by typed repositories and DTO factories that scale prices (thousandths → credits) and enforce query validation.
- `/api/game/*` world endpoints – `map-stats`, `room-status`, `room-terrain`, `rooms`, `world-size`, `time`, `tick`, `place-spawn`, `create-flag`, `change-flag-color`, `remove-flag`, `create-invader`, and `remove-invader` implemented with Mongo-backed repositories, DTO factories, deterministic seeds (docker + Testcontainers), and HTTP scratch files for quick smoke testing. All room-based routes (including `create-invader`) accept either `shard/Room` notation or an explicit `shard` field.
- Legacy helper routes `/api/game/room-overview`, `/api/game/gen-unique-flag-name`, `/api/game/check-unique-flag-name`, and `/api/game/set-notify-when-attacked` are now wired to Mongo services so the official client’s world panels and safety toggles work identically to backend-local (with shard-aware room parsing).
- `/api/game/power-creeps/*` – legacy parity for list/create/delete/cancel-delete/upgrade/rename/experimentation, all backed by the new `MongoPowerCreepService` so the responses include `_id`, shard/room metadata, fatigue, store contents, and cooldown/delete timestamps exactly like `backend-local`.
- `/api/game/add-object-intent` + `/api/game/add-global-intent` – new intent endpoints backed by `MongoIntentService`, reusing the legacy sanitization rules (string/number/boolean transforms, price scaling, body-part filtering) and enforcing the safe-mode guard before storing data in `rooms.intents` / `users.intents`.
- `/api/game/bot/*`, `/api/game/stronghold/*`, `/api/game/system/*`, and `/api/game/map/*` – admin routes for bot AI management, stronghold templates/spawn/expand (now shard-aware), system controls (pause/resume, tick duration, broadcast, storage status, reseed with `confirm=RESET`), and map generation/open/close/remove tasks reusing the shared Mongo/Redis services from the CLI.
- `mods.json` plumbing – the new `FileSystemModManifestProvider` watches the manifest for bot directories and `customIntentTypes/customObjectTypes`, `ManifestIntentSchemaCatalog` merges them with the built-in schemas for `MongoIntentService`, and `/api/server/info` / `/api/version` surface the mod-defined object metadata so the official client renders custom assets just like backend-local.
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
- `ScreepsDotNet.Backend.Http/MarketEndpoints.http` + `WorldEndpoints.http` contain ready-to-send requests for every market/world route once the backend is running. The world scratch file now includes shard-aware samples (set the `shard` query string or JSON property) so you can hit the seeded `shard1` data.
- `ScreepsDotNet.Backend.Http/MapEndpoints.http` includes shard-prefixed examples for generate/open/close/remove/assets; the endpoints accept either `shard/Room` within the `room` field or a separate `shard` property, matching the CLI input rules.
- All `/api/game/world/*` read routes accept either explicit shard parameters **or** the legacy `shardName/RoomName` notation (e.g., `shard1/W21N20`). Explicit parameters override the inline prefix.
   - `ScreepsDotNet.Backend.Http/BotEndpoints.http` exercises the `/api/game/bot/*` admin routes (list/spawn/reload/remove).
- `ScreepsDotNet.Backend.Http/StrongholdEndpoints.http` covers `/api/game/stronghold/*` (templates/spawn/expand) including shard samples.
- `ScreepsDotNet.Backend.Http/SystemEndpoints.http` covers `/api/game/system/*` (status, pause/resume, tick duration, server messages, storage status, storage reseed/reset with `confirm=RESET`).
   - `ScreepsDotNet.Backend.Http/MapEndpoints.http` covers `/api/game/map/*` (generate/open/close/remove/assets/terrain refresh).
   - `ScreepsDotNet.Backend.Http/IntentEndpoints.http` sends `/api/game/add-object-intent` + `/api/game/add-global-intent` payloads so you can verify manual intents end-to-end.
   - `ScreepsDotNet.Backend.Http/PowerCreepEndpoints.http` hits `/api/game/power-creeps/*` so you can create/rename/upgrade/delete/cancel creeps plus register experimentation resets without wiring a custom client.
   - `ScreepsDotNet.Backend.Http/RegisterEndpoints.http` covers `/api/register/*` (check email, check username, set username) to validate onboarding parity quickly.
   - `ScreepsDotNet.Backend.Http/SpawnEndpoints.http` now includes `/api/game/gen-unique-object-name` and `/api/game/check-unique-object-name` samples alongside the spawn/construction flows.
   - `ScreepsDotNet.Backend.Http/UserEndpoints.http` now includes the `/api/user/messages/*` flows (send/list/index/mark-read/unread-count) using the seeded `ally-user` respondent so you can smoke test the inbox endpoints without crafting payloads from scratch.
- CLI quick checks (run from `ScreepsDotNet`):
  - `dotnet run --project ScreepsDotNet.Backend.Cli -- version --json`
  - `dotnet run --project ScreepsDotNet.Backend.Cli -- storage status --json`
  - `dotnet run --project ScreepsDotNet.Backend.Cli -- system status --json`
  - `dotnet run --project ScreepsDotNet.Backend.Cli -- bots list --json`
  - `dotnet run --project ScreepsDotNet.Backend.Cli -- map generate --room W10N5 --shard shard1 --overwrite --json`
  - When you need to reuse the same CLI configuration across runs, copy `.screepscli.sample` to `.screepscli`, adjust the exports, and `source` it so the `SCREEPSCLI_*` environment variables are in place.
7. **Build:** ensure no running `dotnet run` locks DLLs before invoking `dotnet build`.

### Resetting / Updating Seed Data

- All Mongo scripts inside `docker/mongo-init` run only when the container initializes an empty volume.
- `docker/mongo-init/seed-server-data.js` keeps the canonical server metadata document (`server.data` collection) in sync with the legacy backend defaults (`welcomeText`, socket throttles, renderer metadata). For .NET-side tooling/tests, the shared constants live in `ScreepsDotNet.Backend.Core/Seeding/SeedDataDefaults.cs`—reuse that class whenever you need deterministic seed values so the CLI and integration harness stay aligned.
- `docker/mongo-init/seed-users.js` inserts/updates the canonical `test-user` record plus a peer account (`ally-user`) so messaging has a deterministic respondent. It also seeds controller/spawn objects (`rooms.objects`), a deterministic pair of power creeps (`users.power_creeps` + a matching `rooms.objects` document for the spawned creep), a short credit history in `users.money`, and a starter inbox thread/notification (`users.messages` + `users.notifications`) so `/api/user/messages/*` has data out of the box. The newer HTTP routes (code/memory/console) lazily create their own per-user documents once you hit them.
- `seed-world.js` and `SeedDataService` now include a dedicated shard sample (`shard1`) using `SeedDataDefaults.World.SecondaryShardRoom`. The room, terrain, and controller/mineral objects all persist a `shard` field so upcoming shard-aware routes/tests can filter deterministically across both docker and Testcontainers environments.
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
  - `Storage:MongoRedis` connection strings + collection names.
- Sample manifest lives at `ScreepsDotNet.Backend.Http/mods.sample.json`. Copy it when you need a quick `mods.json` target for either the CLI (`--modfile`) or the HTTP host (`BotManifestOptions:ManifestFile` / `MODFILE`); it already includes example bot entries plus `customIntentTypes` / `customObjectTypes` so you can validate the new plumbing end-to-end.
- Server metadata + version info now live in Mongo (`server.data` + `server.version`). Update `docker/mongo-init/seed-server-data.js` and `SeedDataDefaults` before changing these defaults so HTTP + CLI surfaces stay in sync.
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
- CLI integration coverage lives in `ScreepsDotNet.Backend.Cli.Tests/Integration` (map, bots, strongholds, and system suites). These use Testcontainers Mongo + Redis; when adding new commands, follow the same fixture pattern so `dotnet test` exercises real storage.
- Mongo access now goes through typed POCOs under `ScreepsDotNet.Storage.MongoRedis.Repositories.Documents`. When you add a new collection:
  1. Create a document type with `[BsonElement]` attributes (and `[BsonIgnoreExtraElements]` when needed).
  2. Point the repository at `IMongoCollection<TDocument>`; do **not** fall back to `BsonDocument`.
  3. Extend the integration harness seeders so tests have representative data.
- `Extensions/DictionaryPathExtensions.cs` and `Extensions/JsonElementExtensions.cs` provide the only conversion helpers we need (for user memory path mutations). Reuse them instead of reintroducing BSON utilities.
- User integration tests (`ScreepsDotNet.Backend.Http.Tests/Integration`) seed Mongo via `IntegrationTestHarness`. If you add an endpoint or collection, update the harness so every scenario has deterministic data and extend `UserEndpointsIntegrationTests` accordingly.
- User API smoke tests live in `ScreepsDotNet.Backend.Http/UserEndpoints.http`. Always update this file (and `ScreepsDotNet/README.md`) whenever you add/rename an endpoint so new agents can exercise the API immediately.

## Pending / Next Steps

1. **Shard-aware helpers & write-heavy `/api/game/*` mutations**
   - Power-creep parity (list/create/delete/cancel-delete/upgrade/rename/experimentation) is complete across HTTP + CLI, so the remaining backlog from `docs/specs/MarketWorldEndpoints.md` centers on shard-scoped write APIs (spawn placement edge cases, construction batching, notify toggles, etc.). These require deterministic Mongo/Testcontainers fixtures plus HTTP integration suites mirroring the CLI coverage so shard behavior stays consistent.
2. **CLI/HTTP polish**
   - Add optional `--json` output to any remaining CLI commands, extend the HTTP admin routes with structured responses/logging, and keep README/AGENT/manual `.http` files current whenever new management features land.

## Market & World API Spec Snapshot

- `docs/specs/MarketWorldEndpoints.md` remains the canonical reference for legacy behavior, Mongo schemas, .NET repository contracts, and the integration test matrix. It now documents both the read-model endpoints and the manual intent routes (`add-object-intent`, `add-global-intent`, spawn/flag/invader helpers) plus the new mods manifest plumbing so future tweaks stay consistent with backend-local.
- Scope priorities (in order): finish the shard-aware write-heavy endpoints with deterministic seeds/tests, then tackle any CLI/HTTP polish items that remain.

## Tips for Agents

- Run `git status` inside `ScreepsDotNet` repo, not project root; `ScreepsNodeJs` is a separate git directory.
- Stop any running backend (`dotnet run`) before building to avoid locked assemblies.
- Use `docker compose logs -f mongo|redis` for debugging local data issues.
- Keep new config sections mirrored between default and development appsettings.
