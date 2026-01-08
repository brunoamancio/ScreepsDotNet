# ScreepsDotNet

Modern .NET rewrite of the Screeps private server backend. The solution contains an ASP.NET Core HTTP host backed by MongoDB + Redis so we can iteratively replace the legacy Node.js services while keeping the public API and storage layout compatible with the official backend.

## Requirements

- .NET 10 SDK
- Docker Desktop (used for MongoDB 7 + Redis 7 via `docker compose`)
- PowerShell (scripts and docs assume PS)

## Quick Start

1. **Start infrastructure (Mongo + Redis + seed data):**
   ```powershell
   cd ScreepsDotNet
   docker compose up -d
   ```
   This launches:
   - MongoDB on `localhost:27017` with the `screeps` database.
   - Redis on `localhost:16379`.
   - Mongo seed scripts:
     - `docker/mongo-init/seed-users.js` ensures `test-user` exists together with example controller/spawn records.
     - `docker/mongo-init/seed-server-data.js` keeps the `/api/server/info` payload in sync with the legacy backend defaults.

2. **Run the HTTP backend:**
   ```powershell
   dotnet run --project ScreepsDotNet.Backend.Http/ScreepsDotNet.Backend.Http.csproj
   ```

3. **Authenticate and hit protected endpoints:**
   - Get a token via `POST /api/auth/steam-ticket` using the development ticket bundled in `appsettings.Development.json`:
     ```http
     POST http://localhost:5210/api/auth/steam-ticket
     Content-Type: application/json

     {
       "ticket": "dev-ticket",
       "useNativeAuth": false
     }
     ```
   - Copy the returned `token` and use it as the `X-Token` header for every `/api/user/*` call.

4. **Use the `.http` helpers for smoke testing:**
   - `ScreepsDotNet.Backend.Http/UserEndpoints.http` contains ready-made requests for memory, code, branches, console, badge SVG, etc. Update the `@ScreepsDotNet_User_Token` variable with the token from the previous step and execute the requests directly from JetBrains Rider / VS Code (REST Client) / HTTPie.
   - `ScreepsDotNet.Backend.Http/CoreEndpoints.http` provides `/health`, `/api/version`, and `/api/server/info` requests.
- `ScreepsDotNet.Backend.Http/BotEndpoints.http` exercises the `/api/game/bot/*` routes (list/spawn/reload/remove).
- `ScreepsDotNet.Backend.Http/StrongholdEndpoints.http` covers `/api/game/stronghold/*` (templates/spawn/expand) for quick smoke testing.
- `ScreepsDotNet.Backend.Http/SystemEndpoints.http` hits `/api/game/system/*` (status, pause/resume, tick get/set, server message, reset with `confirm=RESET`) for admin verification.
- `ScreepsDotNet.Backend.Http/MapEndpoints.http` manages `/api/game/map/*` (generate, open/close, remove, assets update, terrain refresh).
- `ScreepsDotNet.Backend.Http/IntentEndpoints.http` queues `/api/game/add-object-intent` and `/api/game/add-global-intent` payloads so you can verify manual intents without writing custom tooling.
- `ScreepsDotNet.Backend.Http/PowerCreepEndpoints.http` hits `/api/game/power-creeps/*` (list/create/rename/upgrade/delete/cancel-delete/experimentation) for testing the new operator management surface.

5. **Run automated tests (unit + integration):**
   ```powershell
   dotnet test
   ```
   - Unit tests swap repositories with fast fakes (no Docker dependencies).
   - Integration tests spin up disposable Mongo + Redis containers via [Testcontainers](https://github.com/testcontainers/testcontainers-dotnet) and exercise the real storage adapters/endpoints (including `/api/user/respawn`). Docker Desktop must be running for these tests to pass.

## CLI (`screeps-cli`)

The Spectre-based CLI mirrors the legacy `cli/` scripts so you can manage bots, strongholds, and map data without bringing up the HTTP host. Configuration comes exclusively from command-line switches or `SCREEPSCLI_*` environment variables (no `appsettings.json` dependency).

Run the CLI with:

```powershell
dotnet run --project ScreepsDotNet.Backend.Cli/ScreepsDotNet.Backend.Cli.csproj -- --help
```

### Global switches

| Option | Description |
| --- | --- |
| `--db`, `--storage`, `--storage-backend` | Select the storage backend (`mongodb` is currently the only valid value). |
| `--connection-string`, `--mongo` | Override the MongoDB connection string (default `mongodb://localhost:27017/screeps`). |
| `--cli_host`, `--cli_port` | Retain the legacy CLI listener flags (accepted for compatibility). |
| `--host`, `--port`, `--password` | Legacy HTTP overrides accepted so the client launcher still works. |
| `--modfile` / `SCREEPSCLI_modfile` / `MODFILE` | Path to the legacy `mods.json` manifest containing bot AI definitions. |

Every option can also be supplied via `SCREEPSCLI_<option>` environment variables, e.g., `SCREEPSCLI_connection-string`.
If you just need something to point at while experimenting, copy `ScreepsDotNet.Backend.Http/mods.sample.json` to a writable location and adjust the bot paths to the modules you want to load.

### Bot commands

| Command | Purpose | Key flags |
| --- | --- | --- |
| `bots list [--json]` | Enumerate bot AI bundles discovered from `mods.json`. | `--json` returns structured metadata. |
| `bots spawn --bot <name> --room <room> [--username <name>] [--cpu <int>] [--gcl <int>] [--x <0-49> --y <0-49>] [--json]` | Create a bot user, upload its modules, and place a spawn. | Coordinates default to a valid pad unless both `--x`/`--y` are provided. |
| `bots reload --bot <name> [--json]` | Reload scripts for every user currently running the target AI. | Exit code `0` even when no users matched. |
| `bots remove --username <name> [--json]` | Delete a bot-controlled user (with respawn/cleanup). | Exit code `1` if the user is missing or not a bot. |

Example:

```powershell
dotnet run --project ScreepsDotNet.Backend.Cli -- bots spawn --bot invader --room W1N1 --cpu 150 --gcl 3
```

### Stronghold commands

| Command | Purpose | Key flags |
| --- | --- | --- |
| `strongholds templates [--json]` | Display the embedded stronghold templates and deposit types. | |
| `strongholds spawn --room <name> [--template <name>] [--x <0-49> --y <0-49>] [--owner <userId>] [--deploy-delay <ticks>] [--json]` | Place an NPC stronghold in the room, optionally forcing template/coords. | Coordinates default to a valid placement if omitted. |
| `strongholds expand --room <name> [--json]` | Force the invader core in the room to queue its next expansion. | Exit code `1` if no eligible core exists. |

Example:

```powershell
dotnet run --project ScreepsDotNet.Backend.Cli -- strongholds spawn --room W5N3 --template bunker3 --deploy-delay 10
```

### System commands

| Command | Purpose | Key flags |
| --- | --- | --- |
| `system status [--json]` | Show whether the simulation loop is paused and the current tick duration. | |
| `system pause` / `system resume` | Toggle the simulation main loop. | |
| `system message "<text>"` | Broadcast a server notification via Redis pub/sub. | |
| `system reset --confirm RESET [--force]` | Reseed Mongo/Redis using the canonical seed data. | `--force` required when targeting non-default DBs. |
| `system tick get [--json]` | Display the minimal tick duration stored in Redis. | |
| `system tick set --ms <milliseconds>` | Update and broadcast the minimal tick duration. | |

> HTTP automation can call the same maintenance flows via `/api/game/system/*`: `tick`, `tick-set`, and `message` mirror the CLI switches, and `POST /api/game/system/reset` requires `{"confirm":"RESET"}` (plus `"force": true` when resetting any non-default database).

Example:

```powershell
dotnet run --project ScreepsDotNet.Backend.Cli -- system tick set --ms 750
```

### Map commands

| Command | Purpose | Key flags |
| --- | --- | --- |
| `map generate --room <name> [--sources <1-5>] [--terrain <preset>] [--no-controller] [--keeper-lairs] [--mineral <type>] [--overwrite] [--seed <int>] [--json]` | Procedurally create or overwrite a room with deterministic options. | Terrain presets follow `plain`, `swampLow`, `swampHeavy`, `checker`, `mixed`. |
| `map open --room <name>` | Mark a room as active/accessible. | |
| `map close --room <name>` | Disable a room (legacy “bus” flag). | |
| `map remove --room <name> [--purge-objects]` | Delete the room entry (optionally wipe room objects). | |
| `map assets update --room <name> [--full]` | Regenerate cached renderer assets for a room. | `--full` performs a complete rebuild instead of incremental. |
| `map terrain refresh` | Rebuild the global terrain cache (no parameters). | |

Example:

```powershell
dotnet run --project ScreepsDotNet.Backend.Cli -- map generate --room W10N5 --sources 3 --terrain swampHeavy --keeper-lairs --overwrite --json
```

## Storage Notes

- User data (profile, notify prefs, branches, memory, console queue) lives in Mongo collections:
  - `users` – canonical player documents (`seed-users.js` keeps `test-user` up-to-date).
  - `users.code`, `users.memory`, `users.console` – lazily created by the new repositories when the HTTP endpoints mutate state.
  - `users.money` – rolling credit transactions surfaced via `/api/user/money-history`.
  - `rooms.objects` – source of controller/spawn information for `/api/user/world-*` endpoints.
- Server metadata (`server.data`) and version metadata (`server.version`) power `/api/server/info` and the `protocol/useNativeAuth/packageVersion` fields returned by `/api/version`. Adjust `seed-server-data.js` (and `SeedDataDefaults`) if you need to change these defaults.
- Redis is reserved for token storage and other future Screeps subsystems; the `docker compose` file already wires the container, but current endpoints do not rely on it yet.

### Repository Conventions

- Every Mongo collection has a matching POCO under `ScreepsDotNet.Storage.MongoRedis.Repositories.Documents`. Repositories always take a typed `IMongoCollection<TDocument>` so LINQ queries translate cleanly—please don’t reintroduce `BsonDocument` projections.
- When you add a new collection/field, update the corresponding document type **and** the integration harness (`ScreepsDotNet.Backend.Http.Tests/Integration/IntegrationTestHarness.cs`) so the disposable Mongo instance contains representative data.
- Integration tests in `UserEndpointsIntegrationTests` should cover every storage-backed endpoint you touch; seed data + assertions keep us aligned with the legacy backend.

### Resetting Data

When seed scripts or schemas change, reset the Docker volumes so everyone shares the same baseline:
```powershell
docker compose down -v
docker compose up -d
```
This wipes `mongo-data` / `redis-data`, reruns every script in `docker/mongo-init`, and gives you a clean `test-user`.

## User API Coverage

- Protected routes (`/api/user/world-*`, `/api/user/branches`, `/api/user/code`, `/api/user/memory`, `/api/user/memory-segment`, `/api/user/console`, `/api/user/notify-prefs`, `/api/user/overview`, `/api/user/tutorial-done`, `/api/user/respawn`) operate against the Mongo repositories (`MongoUserCodeRepository`, `MongoUserMemoryRepository`, `MongoUserConsoleRepository`, `MongoUserWorldRepository`, `MongoUserRespawnService`).
- Public routes (`/api/user/find`, `/api/user/rooms`, `/api/user/badge-svg`, `/api/user/stats`) return data seeded into Mongo.
- Profile management routes (`/api/user/badge`, `/api/user/email`, `/api/user/set-steam-visible`) update the canonical `users` document with the same validation rules as the legacy backend.

If you add new endpoints or storage requirements, update:
1. `docker/mongo-init/seed-users.js` (and document the change here).
2. The `.http` files so there is always a runnable example.
3. `AGENT.md` so automation agents know how to refresh their environment.

## Intent endpoints

- `/api/game/add-object-intent` and `/api/game/add-global-intent` now write to `rooms.intents` / `users.intents` via the new `MongoIntentService`. Payloads are sanitized against the legacy schema (string/number/boolean converters, body part filters, price scaling), and activating safe mode enforces the same gametime/safeMode guard that Node uses.
- Integration coverage lives in `ScreepsDotNet.Backend.Http.Tests/Integration/IntentEndpointsIntegrationTests` so every intent mutation is validated against a disposable Mongo + Redis stack.
- Use `IntentEndpoints.http` for quick smoke tests (authentication snippet included).

## Power creep endpoints

- `/api/game/power-creeps/list`, `/create`, `/delete`, `/cancel-delete`, `/upgrade`, `/rename`, and `/experimentation` now mirror the legacy maintenance routes using `MongoPowerCreepService`. They perform the same validation as `backend-local` (class gating, spawn/delete cooldowns, power budget math, experimentation cooldowns) while projecting room data (hits, fatigue, shard, coordinates) when a creep is spawned.
- Integration coverage lives in `ScreepsDotNet.Backend.Http.Tests/Integration/PowerCreepEndpointsIntegrationTests`, which drives the HTTP host against Testcontainers Mongo/Redis to verify list/create/rename/delete/cancel/upgrade/experimentation flows.
- The HTTP scratch file `PowerCreepEndpoints.http` matches the CLI defaults so you can manage creeps via REST without shelling into the host. These endpoints reuse the same storage-backed services, so anything you test here automatically benefits the CLI and future automation.
