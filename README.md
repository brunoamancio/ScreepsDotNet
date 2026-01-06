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
   - Mongo seed script `docker/mongo-init/seed-users.js`, which ensures `test-user` exists together with example controller/spawn records.

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
   - `ScreepsDotNet.Backend.Http/CoreEndpoints.http` provides `/health` and `/api/server/info` requests.

5. **Run automated tests (unit + integration):**
   ```powershell
   dotnet test
   ```
   - Unit tests swap repositories with fast fakes (no Docker dependencies).
   - Integration tests spin up disposable Mongo + Redis containers via [Testcontainers](https://github.com/testcontainers/testcontainers-dotnet) and exercise the real storage adapters/endpoints (including `/api/user/respawn`). Docker Desktop must be running for these tests to pass.

## Storage Notes

- User data (profile, notify prefs, branches, memory, console queue) lives in Mongo collections:
  - `users` – canonical player documents (`seed-users.js` keeps `test-user` up-to-date).
  - `users.code`, `users.memory`, `users.console` – lazily created by the new repositories when the HTTP endpoints mutate state.
  - `users.money` – rolling credit transactions surfaced via `/api/user/money-history`.
- `rooms.objects` – source of controller/spawn information for `/api/user/world-*` endpoints.
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
