# CLI Guide

The Spectre.Console-based CLI mirrors the legacy Screeps scripts so you can manage bots, storage, and world data without spinning up the HTTP host. All configuration flows through command-line switches or `SCREEPSCLI_*` environment variables.

## Running the CLI

```powershell
dotnet run --project src/ScreepsDotNet.Backend.Cli/ScreepsDotNet.Backend.Cli.csproj -- --help
```

Shortcuts:

- Unix/macOS: `./src/cli.sh storage status`
- Windows PowerShell: `pwsh ./src/cli.ps1 system status --json`

Copy `src/.screepscli.sample` to `.screepscli` and `source` it to preload environment variables (Mongo connection string, auth secrets, default output format, etc.).

## Global switches

| Option | Description |
| --- | --- |
| `--db`, `--storage`, `--storage-backend` | Selects the storage backend (`mongodb` today). |
| `--connection-string`, `--mongo` | Override Mongo connection string (`mongodb://localhost:27017/screeps`). |
| `--cli_host`, `--cli_port` | Legacy listener flags (accepted for compatibility). |
| `--host`, `--port`, `--password` | Legacy HTTP overrides so the client launcher keeps working. |
| `--modfile` / `SCREEPSCLI_modfile` / `MODFILE` | Path to `mods.json` describing bot bundles and custom intent/object schemas. |
| `--format table\|markdown\|json` | Overrides the formatter; `SCREEPSCLI_FORMAT` sets the default. |

All commands understand `--json`. Non-JSON output honors `--format`; JSON is always newline-delimited for easy scripting.

## Common workflow: reseed + world validation

```powershell
# Reset Docker volumes (optional but recommended when seeds change)
docker compose -f src/docker-compose.yml down -v
docker compose -f src/docker-compose.yml up -d

# Reseed Mongo with canonical fixtures
./src/cli.sh storage reseed --confirm RESET --force

# Run world integration tests (includes Testcontainers coverage)
dotnet test src/ScreepsDotNet.slnx --filter WorldEndpointsIntegrationTests --nologo

# Spot-check world data from the CLI
./src/cli.sh world dump --room W1N1 --decoded --format markdown
```

## Command reference

The tables below summarize the most frequently used verbs. Each command inherits the global switches and `--json/--format` behavior described earlier.

### Storage

| Command | Purpose |
| --- | --- |
| `storage status [--json] [--format table\|markdown\|json]` | Ping Mongo + Redis and emit connection stats. |
| `storage reseed --confirm RESET [--force] [--json] [--format ...]` | Drop + reseed Mongo with canonical fixtures. |

### User

| Command | Purpose |
| --- | --- |
| `user show (--username <name> \| --user-id <id>) [--json] [--format ...]` | Display profile metadata, credits, owned rooms. |
| `user console --user-id <id> --expression <js> [--hidden]` | Queue a console expression (respects legacy "hidden" flag). |
| `user memory get --user-id <id> [--path <path>] [--segment <0-99>]` | Read root memory, nested paths, or segments. |
| `user memory set --user-id <id> (--path <path> --value <json> \| --segment <0-99> --value <json>)` | Write structured data into memory/segments (validated JSON). |

### Bots & Strongholds

| Command | Purpose |
| --- | --- |
| `bots list` | Enumerate bot AI bundles from `mods.json`. |
| `bots spawn --bot <name> --room <room> [--username <name>] [--cpu <int>] [--gcl <int>] [-x <0-49> -y <0-49>]` | Create a bot user, upload modules, place a spawn. |
| `bots reload --bot <name>` | Reload scripts for every player running the AI. |
| `bots remove --username <name>` | Delete a bot-controlled user, respawn, and cleanup. |
| `strongholds templates|spawn|expand` | Manage stronghold templates and scheduled spawns. |

### Map & World

| Command | Purpose |
| --- | --- |
| `map generate|open|close|remove|assets` | Shard-aware map management helpers (rooms accept `shard/Room` or `--shard`). |
| `world dump --room <name> [--decoded]` | Dump room terrain/objects similar to `backend-local`. |
| `world overview|stats` | Summaries mirroring the HTTP `/api/game/world/*` routes. |

### System & Storage admin

| Command | Purpose |
| --- | --- |
| `system status|pause|resume|message|reset` | Admin controls for tick pacing and announcements (`reset` requires `--confirm RESET`). |
| `system tick get|set` | Inspect or change tick durations. |

### Auth helpers

| Command | Purpose |
| --- | --- |
| `auth issue|resolve|token-list|revoke` | Manage dev/test auth tokens to mimic the legacy CLI flow. |

## Output helpers

Every command derives from `FormattableCommandSettings` and writes through `ICommandOutputFormatter`:

- `WriteJson` for `--json`
- `WriteTable`, `WriteMarkdownTable`, `WriteKeyValueTable`, `WriteLine`, `WriteMarkupLine` for table/markdown output

Please do not call `AnsiConsole` directly in new commands—wire all rendering through the formatter so future automation can flip defaults without editing each handler.
