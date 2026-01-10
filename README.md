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
- `ScreepsDotNet.Backend.Http/SystemEndpoints.http` hits `/api/game/system/*` (status, pause/resume, tick get/set, server message, storage status, storage reseed/reset with `confirm=RESET`) for admin verification. The CLI `storage reseed` command now enforces the same `--confirm RESET` gate.
- `ScreepsDotNet.Backend.Http/MapEndpoints.http` manages `/api/game/map/*` (generate, open/close, remove, assets update, terrain refresh).
- `ScreepsDotNet.Backend.Http/IntentEndpoints.http` queues `/api/game/add-object-intent` and `/api/game/add-global-intent` payloads so you can verify manual intents without writing custom tooling.
- `ScreepsDotNet.Backend.Http/PowerCreepEndpoints.http` hits `/api/game/power-creeps/*` (list/create/rename/upgrade/delete/cancel-delete/experimentation) for testing the new operator management surface.
- `ScreepsDotNet.Backend.Http/RegisterEndpoints.http` covers `/api/register/*` (check-email, check-username, set-username) so you can exercise the onboarding flow end-to-end.
- `ScreepsDotNet.Backend.Http/SpawnEndpoints.http` now includes samples for `/api/game/gen-unique-object-name` and `/api/game/check-unique-object-name` so you can generate/validate spawn names like the legacy client.
- `ScreepsDotNet.Backend.Http/WorldEndpoints.http` includes both default-room samples and new shard-aware requests (pass `shard=shard1` or include `"shard": "shard1"` in the JSON body) so you can exercise the secondary shard seeded by default.
- All `/api/game/world/*` read routes also understand the legacy `shardName/RoomName` notation (e.g., `shard1/W21N20`). If you supply both a `shard` parameter and a prefixed room, the explicit `shard` parameter wins.
- Any `customIntentTypes` / `customObjectTypes` declared in your `mods.json` are now honored automatically: `/api/game/add-*intent` uses the merged schemas, while `/api/server/info` and `/api/version` surface the mod-supplied object metadata so the official client can render custom assets.

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
| `--modfile` / `SCREEPSCLI_modfile` / `MODFILE` | Path to the legacy `mods.json` manifest containing bot AI directories plus any `customIntentTypes/customObjectTypes`. |
| `--format <table\|markdown\|json>` | (Optional) Overrides the default formatting for status-style commands; JSON behaves the same as the traditional `--json` switches. |

Every option can also be supplied via `SCREEPSCLI_<option>` environment variables, e.g., `SCREEPSCLI_connection-string`. For convenience we ship `.screepscli.sample`; copy or source it to preload the common Mongo/Redis/asset settings before running the CLI. Commands that support formatted summaries (storage status, system status, etc.) honor `--format table|markdown|json` when not already emitting JSON.
If you just need something to point at while experimenting, copy `ScreepsDotNet.Backend.Http/mods.sample.json` to a writable location, adjust the bot paths, and edit the sample `customIntentTypes` / `customObjectTypes` entries as needed.
To set a default formatter globally, export `SCREEPSCLI_FORMAT=markdown` (or `json`/`table`); the CLI uses that value whenever `--format` isn’t specified.

### Storage commands

| Command | Purpose | Key flags |
| --- | --- | --- |
| `storage status [--json] [--format table\|markdown\|json]` | Pings Mongo + Redis and emits connection latency/status. | |
| `storage reseed --confirm RESET [--force] [--json] [--format table\|markdown\|json]` | Drops and reseeds Mongo with the canonical fixtures. | `--force` required when targeting any DB other than the default. |

```powershell
dotnet run --project ScreepsDotNet.Backend.Cli -- storage status --format markdown
dotnet run --project ScreepsDotNet.Backend.Cli -- storage reseed --confirm RESET --force --json
```

### User commands

| Command | Purpose | Key flags |
| --- | --- | --- |
| `user show (--username <name> \| --user-id <id>) [--json] [--format table\|markdown\|json]` | Displays profile metadata, credits, and owned rooms. | |
| `user console --user-id <id> --expression <js> [--hidden] [--json] [--format table\|markdown\|json]` | Queues a console expression for the player. | `--hidden` mirrors the legacy “silent” flag. |
| `user memory get --user-id <id> [--path <path>] [--segment <0-99>] [--json] [--format table\|markdown\|json]` | Reads root memory, a nested path, or a memory segment. | |
| `user memory set --user-id <id> (--path <path> --value <json> \| --segment <0-99> --value <json>) [--json] [--format table\|markdown\|json]` | Writes structured data into memory/segments (JSON validated). | |

```powershell
dotnet run --project ScreepsDotNet.Backend.Cli -- user show --username test-user --format markdown
dotnet run --project ScreepsDotNet.Backend.Cli -- user console --user-id 57874d42d0ae911e3bd15bbc --expression "console.log(Game.time)" --json
dotnet run --project ScreepsDotNet.Backend.Cli -- user memory set --user-id 57874d42d0ae911e3bd15bbc --path stats.logLevel --value "\"info\"" --format table
```

### Bot commands

| Command | Purpose | Key flags |
| --- | --- | --- |
| `bots list [--json] [--format table\|markdown\|json]` | Enumerate bot AI bundles discovered from `mods.json`. | Non-JSON runs default to a Spectre table; `--format markdown/json` helps automation. |
| `bots spawn --bot <name> --room <room> [--username <name>] [--cpu <int>] [--gcl <int>] [-x/--spawn-x <0-49> -y/--spawn-y <0-49>] [--json] [--format table\|markdown\|json]` | Create a bot user, upload its modules, and place a spawn. | Coordinates default to a valid pad unless both `-x`/`-y` (or their long forms) are provided. |
| `bots reload --bot <name> [--json] [--format table\|markdown\|json]` | Reload scripts for every user currently running the target AI. | Exit code `0` even when no users matched. |
| `bots remove --username <name> [--json] [--format table\|markdown\|json]` | Delete a bot-controlled user (with respawn/cleanup). | Exit code `1` if the user is missing or not a bot. |

Example:

```powershell
dotnet run --project ScreepsDotNet.Backend.Cli -- bots spawn --bot invader --room W1N1 --cpu 150 --gcl 3
```

### World commands

| Command | Purpose | Key flags |
| --- | --- | --- |
| `world dump --room <name> [--room <name> ...] [--shard <name>] [--decoded] [--json]` | Export terrain documents (encoded or decoded) for one or more rooms. | Accepts repeated `--room` flags; `--decoded` expands 2 500 tiles. |
| `world stats --room <name> [--room <name> ...] [--shard <name>] [--stat <owners1\|power5\|...>] [--json] [--format table\|markdown\|json]` | Query `/api/game/map-stats` for the requested rooms, including ownership, signs, safe-mode status, and mineral hints. | Validates the legacy `ownersN`/`powerN` suffix pattern before hitting storage. |
| `world overview --room <name> [--shard <name>] [--json] [--format table\|markdown\|json]` | Display controller ownership for a single room (mirrors `/api/game/room-overview`). | Shows `(unowned)` when the controller has no owner/reservation. |

```powershell
dotnet run --project ScreepsDotNet.Backend.Cli -- world dump --room W1N1 --decoded --format markdown
dotnet run --project ScreepsDotNet.Backend.Cli -- world stats --room shard1/W21N20 --stat owners1 --json
```

### Stronghold commands

| Command | Purpose | Key flags |
| --- | --- | --- |
| `strongholds templates [--json] [--format table\|markdown\|json]` | Display the embedded stronghold templates and deposit types. | |
| `strongholds spawn --room <name> [--shard <name>] [--template <name>] [-x/--pos-x <0-49> -y/--pos-y <0-49>] [--owner <userId>] [--deploy-delay <ticks>] [--json] [--format table\|markdown\|json]` | Place an NPC stronghold in the room, optionally forcing template/coords and shard. | Coordinates default to a valid placement if omitted. |
| `strongholds expand --room <name> [--shard <name>] [--json] [--format table\|markdown\|json]` | Force the invader core in the room to queue its next expansion. | Exit code `1` if no eligible core exists. |

Example:

```powershell
dotnet run --project ScreepsDotNet.Backend.Cli -- strongholds spawn --room W5N3 --shard shard1 --template bunker3 --deploy-delay 10
```

### Invader commands

| Command | Purpose | Key flags |
| --- | --- | --- |
| `invader create --room <name> [--shard <name>] (--user-id <id> \| --username <name>) -x/--pos-x <0-49> -y/--pos-y <0-49> [--type <Melee\|Ranged\|Healer>] [--size <Small\|Big>] [--boosted] [--json] [--format table\|markdown\|json]` | Summon an NPC invader in an owned or reserved room. | Accepts either explicit `--shard` or the legacy `shard/Room` syntax. |
| `invader remove --id <objectId> (--user-id <id> \| --username <name>) [--json] [--format table\|markdown\|json]` | Remove a previously summoned invader (requires the original summoner). | |

Example:

```powershell
dotnet run --project ScreepsDotNet.Backend.Cli -- invader create --username IntegrationUser --room W21N20 --shard shard1 -x 12 -y 18 --type Ranged
```

### System commands

| Command | Purpose | Key flags |
| --- | --- | --- |
| `system status [--json] [--format table\|markdown\|json]` | Show whether the simulation loop is paused and the current tick duration. | `--format` controls non-JSON output. |
| `system pause [--json] [--format table\|markdown\|json]` / `system resume [--json] [--format ...]` | Toggle the simulation main loop. | `--format` only applies to non-JSON output. |
| `system message "<text>" [--json] [--format table\|markdown\|json]` | Broadcast a server notification via Redis pub/sub. | |
| `system reset --confirm RESET [--force] [--json] [--format table\|markdown\|json]` | Reseed Mongo/Redis using the canonical seed data. | `--force` required when targeting non-default DBs. |
| `system tick get [--json] [--format table\|markdown\|json]` | Display the minimal tick duration stored in Redis. | |
| `system tick set --ms <milliseconds> [--json] [--format table\|markdown\|json]` | Update and broadcast the minimal tick duration. | |

> HTTP automation can call the same maintenance flows via `/api/game/system/*`: `tick`, `tick-set`, `message`, and the new storage helpers all mirror the CLI switches. `POST /api/game/system/reset` or `/storage-reseed` requires `{"confirm":"RESET"}` (plus `"force": true` when resetting any non-default database).

Example:

```powershell
dotnet run --project ScreepsDotNet.Backend.Cli -- system tick set --ms 750
dotnet run --project ScreepsDotNet.Backend.Cli -- system status --format markdown
dotnet run --project ScreepsDotNet.Backend.Cli -- system reset --confirm RESET --force --json
```

### Map commands

| Command | Purpose | Key flags |
| --- | --- | --- |
| `map generate --room <name> [--shard <name>] [--sources <1-5>] [--terrain <preset>] [--no-controller] [--keeper-lairs] [--mineral <type>] [--overwrite] [--seed <int>] [--json] [--format table\|markdown\|json]` | Procedurally create or overwrite a room with deterministic options. | Terrain presets follow `plain`, `swampLow`, `swampHeavy`, `checker`, `mixed`. |
| `map open --room <name> [--shard <name>] [--json] [--format table\|markdown\|json]` | Mark a room as active/accessible. | |
| `map close --room <name> [--shard <name>] [--json] [--format table\|markdown\|json]` | Disable a room (legacy “bus” flag). | |
| `map remove --room <name> [--shard <name>] [--purge-objects] [--json] [--format table\|markdown\|json]` | Delete the room entry (optionally wipe room objects). | |
| `map assets update --room <name> [--shard <name>] [--full] [--json] [--format table\|markdown\|json]` | Regenerate cached renderer assets for a room. | `--full` performs a complete rebuild instead of incremental. |
| `map terrain refresh [--json] [--format table\|markdown\|json]` | Rebuild the global terrain cache (no parameters). | |

Example:

```powershell
dotnet run --project ScreepsDotNet.Backend.Cli -- map generate --room W10N5 --shard shard1 --sources 3 --terrain swampHeavy --keeper-lairs --overwrite --json
```

### Flag commands

| Command | Purpose | Key flags |
| --- | --- | --- |
| `flag create --username <name> --room <room> -x/--pos-x <0-49> -y/--pos-y <0-49> --name <flagName> [--color <primary>] [--secondary-color <secondary>] [--json] [--format table\|markdown\|json]` | Creates a flag at the chosen coordinate for the user. | Color names follow the legacy palette (`red`, `purple`, etc.). |
| `flag change-color --username <name> --room <room> --name <flagName> --color <primary> [--secondary-color <secondary>] [--json] [--format table\|markdown\|json]` | Updates the primary/secondary colors without moving the flag. | |
| `flag remove --username <name> --room <room> --name <flagName> [--json] [--format table\|markdown\|json]` | Deletes the flag. | |

```powershell
dotnet run --project ScreepsDotNet.Backend.Cli -- flag create --username test-user --room W1N1 -x 25 -y 23 --name AlphaFlag --color red --secondary-color white --json
dotnet run --project ScreepsDotNet.Backend.Cli -- flag change-color --username test-user --room W1N1 --name AlphaFlag --color yellow --format markdown
```

### Auth commands

| Command | Purpose | Key flags |
| --- | --- | --- |
| `auth issue --user-id <id> [--json] [--format table\|markdown\|json]` | Mint a token for the specified user via the shared Redis token service (handy for HTTP smoke tests). | Token prints as JSON or a key/value table. |
| `auth resolve --token <value> [--json] [--format table\|markdown\|json]` | Resolve an auth token back to its user id, matching the logic in the HTTP middleware. | Exit code `1` if the token is missing or expired. |
| `auth token-list [--user-id <id>] [--json]` | Enumerate active tokens, optionally filtering by user id, to audit who currently has access. | Shows TTL countdown and exits `0` even when no tokens match. |
| `auth revoke --token <value> [--json] [--format table\|markdown\|json]` | Delete a token immediately (returns `1` if the token does not exist). | Useful for revoking leaked/expired credentials without flushing Redis. |

Example:

```powershell
dotnet run --project ScreepsDotNet.Backend.Cli -- auth issue --user-id test-user --format markdown
dotnet run --project ScreepsDotNet.Backend.Cli -- auth resolve --token deadbeef --json
dotnet run --project ScreepsDotNet.Backend.Cli -- auth revoke --token deadbeef --format table
```

### CLI architecture notes

- Every command derives from `CommandHandler<TSettings>`, which logs start/finish events, links cancellation tokens to `ConsoleLifetime`, and normalizes cancellation/failure exit codes.
- All output flows through `ICommandOutputFormatter` (the `CommandOutputFormatter` DI registration). The formatter exposes `WriteJson`, `WriteTable`, `WriteKeyValueTable`, `WriteMarkdownTable`, `WriteLine`, and `WriteMarkupLine`, so operators and tests see consistent results. To add a new command, inject the formatter via the handler constructor and use `OutputFormatter` inside `ExecuteCommandAsync`:

  ```csharp
  internal sealed class ExampleCommand(IMyService service,
                                      ILogger<ExampleCommand>? logger = null,
                                      IHostApplicationLifetime? lifetime = null,
                                      ICommandOutputFormatter? outputFormatter = null)
      : CommandHandler<ExampleCommand.Settings>(logger, lifetime, outputFormatter)
  {
      protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken token)
      {
          var payload = await service.GetAsync(token);
          OutputFormatter.WriteJson(payload);
          return 0;
      }
  }
  ```
- Running `screeps-cli` without a subcommand emits the same configuration summary through the formatter, so even `--format markdown` at the root level produces predictable output for automation.

## Storage Notes

- User data (profile, notify prefs, branches, memory, console queue) lives in Mongo collections:
  - `users` – canonical player documents (`seed-users.js` keeps `test-user` up-to-date).
  - `users.code`, `users.memory`, `users.console` – lazily created by the new repositories when the HTTP endpoints mutate state.
  - `users.money` – rolling credit transactions surfaced via `/api/user/money-history`.
  - `users.messages` / `users.notifications` – private inbox threads and notification counters backing `/api/user/messages/*` (seed data inserts a sample exchange between the integration user and a peer for smoke tests).
  - `rooms.objects` – source of controller/spawn information for `/api/user/world-*` endpoints.
- Server metadata (`server.data`) and version metadata (`server.version`) power `/api/server/info` and the `protocol/useNativeAuth/packageVersion` fields returned by `/api/version`. Adjust `seed-server-data.js` (and `SeedDataDefaults`) if you need to change these defaults.
- Both the Testcontainers harness (`SeedDataService`) and the docker init scripts now seed a dedicated shard sample (`shard1` / `SeedDataDefaults.World.SecondaryShardRoom`) with matching `rooms`, `rooms.objects`, and `rooms.terrain` documents. Use it when exercising upcoming shard-aware world endpoints—each document carries a `shard` field so you can filter deterministically.
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
- Messaging routes (`/api/user/messages/send|list|index|mark-read|unread-count`) share `MongoUserMessageService`, which persists bi-directional threads in `users.messages`, upserts notifications in `users.notifications`, and enforces the same 100 KB payload limit + respondent validation as the legacy backend. Scratch samples live in `UserEndpoints.http`.
- Registration routes (`/api/register/check-email`, `/api/register/check-username`, `/api/register/set-username`) mirror the onboarding flow from backend-local: the public checks validate format and uniqueness, while the protected `set-username` call (token required) claims a username and optional email for the authenticated user. Examples live in `RegisterEndpoints.http`.
- Cross-shard regression tests now cover bots (`BotEndpointsIntegrationTests.RemoveBot_ShardsRemainIsolated`), strongholds (`StrongholdEndpointsIntegrationTests.Expand_WithShardTargetsMatchingCore`), intents, and map management (`MapEndpointsIntegrationTests.OpenRoom_WithShard_DoesNotAffectOtherShard`) so we know that multi-shard mutations touch only the targeted shard’s documents.

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

## World helper endpoints

- `/api/game/room-overview`, `/api/game/gen-unique-flag-name`, `/api/game/check-unique-flag-name`, and `/api/game/set-notify-when-attacked` match backend-local behavior so the official client’s room panels, flag dialogs, and structure toggles work out of the box. They reuse the same shard-aware parsing rules (`?shard=` or `shardName/RoomName`) introduced for the other world routes.
- Coverage lives in `WorldEndpointsIntegrationTests`, `FlagEndpointsIntegrationTests`, and the new `StructureEndpointsIntegrationTests`, each running under the Testcontainers harness.
