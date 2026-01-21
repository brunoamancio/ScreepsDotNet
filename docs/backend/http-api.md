# HTTP Endpoint Reference

Use this document to find the route coverage, behaviors, and scratch files for manual testing. Every `.http` file lives under `src/ScreepsDotNet.Backend.Http/` and can be executed from Rider, VS Code (REST Client), or HTTPie.

## Auth & Setup

- `POST /api/auth/steam-ticket` – accepts the dev ticket in `appsettings.Development.json`. Use this to retrieve the `X-Token` used by every `/api/user/*` call.
- Scratch file: `AuthEndpoints.http`.

## User & Profile

| Routes | Notes | Scratch file |
| --- | --- | --- |
| `/api/user/info`, `/api/user/badge`, `/api/user/email`, `/api/user/set-steam-visible` | Profile metadata, badge SVG, visibility toggles. | `UserEndpoints.http` |
| `/api/user/code/*` | Upload/download branches via `MongoUserCodeRepository`; supports `HEAD`, `POST`, `DELETE`. | `UserEndpoints.http` |
| `/api/user/memory/*` | Read/write memory paths and segments; `DictionaryPathExtensions` handle nested paths. | `UserEndpoints.http` |
| `/api/user/console` | Queue console commands and fetch output. | `UserEndpoints.http` |
| `/api/user/tutorial/done`, `/api/user/notify-prefs`, `/api/user/set-badge` | Misc profile operations. | `UserEndpoints.http` |
| `/api/user/messages/*` | send/list/index/mark-read/unread-count with notification counters. | `UserEndpoints.http` |

## Registration

| Routes | Purpose | Scratch file |
| --- | --- | --- |
| `/api/register/check-email`, `/api/register/check-username`, `/api/register/set-username` | Mirrors the legacy onboarding flow. | `RegisterEndpoints.http` |

## Intents & Runtime Helpers

| Routes | Purpose | Scratch file |
| --- | --- | --- |
| `/api/game/add-object-intent`, `/api/game/add-global-intent` | Validates payloads (type coercion, price scaling, safe-mode guard) before writing to Mongo `rooms.intents` / `users.intents`. | `IntentEndpoints.http` |
| `/api/game/gen-unique-object-name`, `/api/game/check-unique-object-name` | Spawn naming helpers. | `SpawnEndpoints.http` |
| `/api/game/gen-unique-flag-name`, `/api/game/check-unique-flag-name`, `/api/game/set-notify-when-attacked` | Legacy helpers surfaced via Mongo services. | `MapEndpoints.http` / `SystemEndpoints.http` |

## Bot, Stronghold, Map, System Admin

| Routes | Purpose | Scratch file |
| --- | --- | --- |
| `/api/game/bot/list|spawn|reload|remove` | Administer bot AI bundles discovered from `mods.json`. | `BotEndpoints.http` |
| `/api/game/stronghold/templates|spawn|expand` | Manage stronghold templates and deployments (shard-aware). | `StrongholdEndpoints.http` |
| `/api/game/map/generate|open|close|remove|assets|terrain-refresh` | Map lifecycle helpers; accept `shard/Room` or `shard` property. | `MapEndpoints.http` |
| `/api/game/system/status|pause|resume|tick|get|tick|set|message|storage-status|storage-reset` | System controls, tick pacing, storage reseed with `confirm=RESET`. | `SystemEndpoints.http` |

## Power Creeps & Invaders

| Routes | Purpose | Scratch file |
| --- | --- | --- |
| `/api/game/power-creeps/*` | list/create/delete/cancel-delete/upgrade/rename/experimentation; responses include `_id`, shard/room metadata, cooldowns, store state. | `PowerCreepEndpoints.http` |
| `/api/game/invader/create|remove` | Admin helpers for invader creeps. | `WorldEndpoints.http` / CLI |

## World & Map Data

| Routes | Highlights | Scratch file |
| --- | --- | --- |
| `/api/game/world-size`, `/api/game/time`, `/api/game/tick` | Global info mirrors backend-local. | `WorldEndpoints.http` |
| `/api/game/rooms`, `/api/game/room-status`, `/api/game/room-terrain` | Support shard-aware identifiers and explicit `shard` params; returns deterministic seed data for `shard0` + `shard1`. | `WorldEndpoints.http` |
| `/api/game/room-overview`, `/api/game/world-stats`, `/api/game/map-stats` | Overview + stats endpoints with Mongo-backed aggregates. | `WorldEndpoints.http` |
| `/api/game/flag/create|change-color|remove`, `/api/game/invader/create|remove` | Admin writes for flags/invaders; reuse the shard-aware parser shared with the CLI. | `MapEndpoints.http` / `WorldEndpoints.http` |

## Market

| Routes | Notes | Scratch file |
| --- | --- | --- |
| `/api/game/market/orders-index`, `/api/game/market/orders`, `/api/game/market/my-orders`, `/api/game/market/stats` | Price scaling (thousandths → credits) and validation follow the legacy rules. | `MarketEndpoints.http` |

## Scratch File Tips

- Each `.http` file declares a `@ScreepsDotNet_User_Token` variable; update it after authenticating.
- Use the shard-aware helpers: either pass `room = shard1/W21N20` or `room = W21N20` with `shard = shard1`. Explicit `shard` wins if both are provided.
- The files double as documentation—when adding new endpoints, include samples showing required headers/body and update this guide.

## CLI vs HTTP

Most HTTP features have CLI counterparts (storage status, system controls, world/map helpers, bots/strongholds). When deciding which surface to extend:

- Use HTTP for client-facing parity or when the official client expects a route.
- Use CLI for operator-only workflows, but document any new verbs in `docs/backend/cli.md` and keep `.http` samples in sync if they share repositories/services.
