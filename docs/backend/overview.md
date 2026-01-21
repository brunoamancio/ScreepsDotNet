# Backend HTTP API

This guide covers the HTTP API endpoints exposed by `ScreepsDotNet.Backend.Http`. For solution-wide information (storage, seeding, development workflow, coding standards), see `CLAUDE.md` in the solution root.

## Manual Smoke Tests

After starting the HTTP host with `dotnet run --project src/ScreepsDotNet.Backend.Http/ScreepsDotNet.Backend.Http.csproj`:

- `GET http://localhost:5210/health`
- `GET http://localhost:5210/api/server/info`
- Use the `.http` files (see [http-api.md](http-api.md)) for user/map/world/system checks or the CLI shortcuts (`./src/cli.sh` / `pwsh ./src/cli.ps1`).

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

See [http-api.md](http-api.md) for per-route references and `.http` scratch files.
