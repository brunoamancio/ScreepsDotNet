# Getting Started

This guide walks you through local setup of the ScreepsDotNet stack (Mongo + Redis + HTTP backend), how to authenticate, and how to keep the fixtures/tests in sync with the legacy server.

## Requirements

- .NET 10 SDK
- Docker Desktop (MongoDB 7 + Redis 7 are launched via `docker compose`)
- PowerShell (scripts and docs assume PS, but Bash equivalents live next to them)

> All commands assume your shell is in the repo root: `ScreepsDotNet/`. .NET projects live under `src/`.

## Start Mongo + Redis

```powershell
cd ScreepsDotNet
docker compose -f src/docker-compose.yml up -d
```

Services launched:

- MongoDB on `localhost:27017` with the `screeps` database
- Redis on `localhost:16379`
- Mongo seed scripts (run automatically when the `mongo-data` volume is empty)
  - `src/docker/mongo-init/seed-users.js` creates `test-user`, spawns, controller docs, inbox samples, power creeps, etc.
  - `src/docker/mongo-init/seed-server-data.js` mirrors `/api/server/info` defaults from the legacy backend.

## Run the HTTP backend

```powershell
dotnet run --project src/ScreepsDotNet.Backend.Http/ScreepsDotNet.Backend.Http.csproj
```

Authenticate via `POST /api/auth/steam-ticket` (payload stored in `appsettings.Development.json`):

```http
POST http://localhost:5210/api/auth/steam-ticket
Content-Type: application/json

{
  "ticket": "dev-ticket",
  "useNativeAuth": false
}
```

Use the returned `token` as `X-Token` for `/api/user/*` routes.

## HTTP scratch files

We ship `.http` helpers under `src/ScreepsDotNet.Backend.Http/` for quick smoke tests. Update the `@ScreepsDotNet_User_Token` variable once you authenticate.

- `UserEndpoints.http`, `RegisterEndpoints.http`, `PowerCreepEndpoints.http`, `IntentEndpoints.http`, etc.
- `MapEndpoints.http`, `WorldEndpoints.http`, and `StrongholdEndpoints.http` contain shard-aware samples.
- `SystemEndpoints.http` covers pause/resume/tick/storage reseed (with the same `confirm=RESET` guard the CLI enforces).

## Automated tests

```powershell
dotnet test src/ScreepsDotNet.slnx
```

- Unit tests rely on fakes (no Docker dependency).
- Integration tests use [Testcontainers](https://github.com/testcontainers/testcontainers-dotnet) to launch Mongo + Redis; ensure Docker Desktop is running.

## Local development workflow

1. `docker compose -f src/docker-compose.yml up -d` – be sure infra is running.
2. `dotnet run --project src/ScreepsDotNet.Backend.Http/ScreepsDotNet.Backend.Http.csproj` – start the HTTP host when working on endpoints.
3. `dotnet run --project src/ScreepsDotNet.Backend.Cli/ScreepsDotNet.Backend.Cli.csproj -- --help` – explore CLI verbs (details in [docs/cli.md](cli.md)).
4. `dotnet test src/ScreepsDotNet.slnx` – run before sending PRs; integration suites rely on the dockerized services.
5. Keep `dotnet format` handy for IDE warnings: `dotnet format style --severity error --diagnostics IDE0005,IDE0011,IDE0007`.

## Resetting or updating seed data

Seed scripts under `src/docker/mongo-init` run when Mongo starts with an empty volume.

- Reset everything: `docker compose -f src/docker-compose.yml down -v && docker compose -f src/docker-compose.yml up -d`
- Reset only Mongo: `docker volume rm screepsdotnet_mongo-data && docker compose -f src/docker-compose.yml up -d mongo`
- Tail logs: `docker compose -f src/docker-compose.yml logs -f mongo`

Document new fixtures both in the seed JS files and in this guide so Testcontainers + Docker environments stay aligned.

## Configuration quick reference

- `appsettings.json` and `appsettings.Development.json` contain Mongo/Redis connection strings and feature toggles.
- `src/ScreepsDotNet.Backend.Http/mods.sample.json` doubles as a CLI/HTTP manifest sample (`--modfile`).
- Shared defaults live in `src/ScreepsDotNet.Backend.Core/Seeding/SeedDataDefaults.cs`; reuse it when writing new seeders/tests.

For deeper design docs (market/world specs, driver rewrite notes, etc.) see the files under `docs/specs/`.
