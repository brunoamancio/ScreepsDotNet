# AGENT GUIDE

## Repository Layout

- `ScreepsNodeJs/` – original Node.js Screeps server (git repo moved inside); untouched except for reference.
- `ScreepsDotNet/` – new .NET solution containing:
  - `ScreepsDotNet.Backend.Core/` – cross-cutting contracts (configuration, models, repositories, services).
  - `ScreepsDotNet.Backend.Http/` – ASP.NET Core Web API host (currently health + server info endpoints).
  - `ScreepsDotNet.Storage.MongoRedis/` – MongoDB/Redis infrastructure (adapter + repositories) used by the HTTP host.
  - `.editorconfig`, `.globalconfig`, `.gitattributes`, `Directory.Build.props` – shared tooling settings.
  - `docker/` – supporting assets (Mongo init scripts, etc.).
  - `docker-compose.yml` – spins up MongoDB + Redis for local dev.

## Current Features

- `/health` – ASP.NET health checks with custom JSON output (Mongo/Redis probe).
- `/api/server/info` – reads metadata from Mongo `serverData` (seeded automatically).
- Core abstractions defined for server info, users, rooms, CLI sessions, storage status, and engine ticks.
- Mongo repositories implemented for server info, users, and owned rooms; ready for future endpoints.

## Local Development Workflow

1. **Dependencies:** .NET 10 SDK, Docker Desktop (for Mongo/Redis), PowerShell.
2. **Start infrastructure:**  
   ```powershell
   cd ScreepsDotNet
   docker compose up -d
   ```
   - Seeds Mongo automatically via `docker/mongo-init/seed-server-info.js`.
   - Uses Mongo 7.0 on `localhost:27017`, Redis 7.2 on `localhost:16379`.
3. **Run backend:**  
   ```powershell
   dotnet run --project ScreepsDotNet.Backend.Http/ScreepsDotNet.Backend.Http.csproj
   ```
4. **Run automated tests:**  
   ```powershell
   dotnet test
   ```
   - Integration tests live in `ScreepsDotNet.Backend.Http.Tests` and rely on `WebApplicationFactory<Program>` with faked storage dependencies, so no external services are required to run them.
5. **Manual smoke tests:**  
   - `GET http://localhost:5210/health`
   - `GET http://localhost:5210/api/server/info`
6. **Build:** ensure no running `dotnet run` locks DLLs before invoking `dotnet build`.

## Configuration

- `appsettings.json` & `appsettings.Development.json`:
  - `ServerInfo` defaults (used by configuration repository / tests).
  - `Storage:MongoRedis` connection strings + collection names.
- `docker-compose.yml` uses volumes `mongo-data` / `redis-data`. Run `docker compose down -v` to reseed.

## Coding Standards

- Nullable reference types enabled solution-wide.
- `.editorconfig` enforces strict code style (ReSharper settings check in `.DotSettings`).
- Health and endpoint logic extracted into dedicated classes to keep `Program.cs` minimal.
- Constants used for repeated strings (routes, content types, field names).
- JetBrains Rider/ReSharper rules are stored in `ScreepsDotNet.slnx.DotSettings` (solution-level). Keep this file updated when adding new inspections; don’t delete it since it enforces shared inspection severity.

## Pending / Next Steps

1. Build additional HTTP endpoints for users/rooms leveraging new repositories.
2. Add automated tests (unit + integration) spinning up Docker services.
3. Scaffold CLI host (`ScreepsDotNet.Backend.Cli`) when backend surfaces are stable.
4. Replace in-memory server-info provider once storage-backed provider is fully vetted.

## Tips for Agents

- Run `git status` inside `ScreepsDotNet` repo, not project root; `ScreepsNodeJs` is a separate git directory.
- Stop any running backend (`dotnet run`) before building to avoid locked assemblies.
- Use `docker compose logs -f mongo|redis` for debugging local data issues.
- Keep new config sections mirrored between default and development appsettings.
