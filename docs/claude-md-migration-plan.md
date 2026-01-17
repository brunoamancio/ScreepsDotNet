# AGENTS.md → CLAUDE.md Migration Plan

## Rationale for CLAUDE.md

**CLAUDE.md** is the standard convention for providing context to Claude Code specifically. Benefits:

1. **Clear Purpose** - Immediately signals "this is for AI agents"
2. **Community Standard** - Follows emerging convention in Claude Code projects
3. **Optimized Format** - Written specifically for AI consumption and understanding
4. **Separation of Concerns** - Human docs in docs/, AI context in CLAUDE.md
5. **Better Discoverability** - Claude Code looks for CLAUDE.md by convention

## Proposed File Structure

```
ScreepsDotNet/
├── CLAUDE.md                                    # Root: Solution-wide context
├── README.md                                    # Human-facing overview
├── docs/                                        # Human-facing detailed docs
│   ├── getting-started.md
│   ├── backend.md
│   └── ...
└── src/
    ├── ScreepsDotNet.Driver/
    │   ├── CLAUDE.md                           # Driver-specific context
    │   └── docs/                                # Driver design docs
    ├── ScreepsDotNet.Engine/
    │   ├── CLAUDE.md                           # Engine-specific context
    │   └── docs/                                # Engine design docs
    └── native/
        └── pathfinder/
            └── CLAUDE.md                        # Pathfinder-specific context
```

## Content Philosophy: AI-First Design

### AGENTS.md Philosophy (Current)
- Written for human OR AI
- Mix of reference and instruction
- Assumes reader might skip around
- Links heavily to other docs

### CLAUDE.md Philosophy (Proposed)
- **Assume AI reader** - More explicit, less assumed context
- **Self-contained** - Include critical context inline, not just links
- **Pattern-heavy** - Show examples of good/bad code
- **Imperative tone** - "Do X", "Never do Y", "Always check Z"
- **Structured for scanning** - AI can quickly find relevant sections

## Root CLAUDE.md Template

```markdown
# [Project Name] - Claude Context

## Mission
[2-3 sentences: What is this project? What problem does it solve?]

## Critical Rules (Read First)
- Always use Context7 MCP for library/API documentation
- Never commit without running `dotnet format style`
- Never modify files in `ScreepsNodeJs/` (separate git repo)
- Always run `git status` from `ScreepsDotNet/` directory
- Stop `dotnet run` before `dotnet build` to avoid locked DLLs

## Project Structure
[Inline, not linked - AI needs this context immediately]

```
src/
├── ScreepsDotNet.Backend.Core/      # DTOs, contracts, seeding defaults
├── ScreepsDotNet.Backend.Http/      # ASP.NET Core API host
├── ScreepsDotNet.Backend.Cli/       # Spectre.Console CLI tool
├── ScreepsDotNet.Storage.MongoRedis/# MongoDB/Redis repositories
├── ScreepsDotNet.Driver/            # Runtime coordinator, queue, processor
│   └── CLAUDE.md                    # Driver-specific context
├── ScreepsDotNet.Engine/            # Simulation kernel rewrite
│   └── CLAUDE.md                    # Engine-specific context
└── native/pathfinder/               # Native C++ pathfinder
    └── CLAUDE.md                    # Pathfinder-specific context
```

## Coding Standards (Enforced)

### .NET Conventions (Solution-wide)
- Nullable reference types enabled everywhere
- Implicit usings via `Directory.Build.props` - never add `using System;` manually
- Use `var` when type is obvious: `var user = await _repo.GetByIdAsync(id);`
- Keep explicit types for primitives: `int count = 0;` not `var count = 0;`

### Code Style Patterns
**✅ Good:**
```csharp
// Expression-bodied members for one-liners
public string GetUserName() => _user.Name;

// Collection expressions for empty collections
var items = [];

// Lock type for synchronization
private readonly Lock _lock = new();
```

**❌ Bad:**
```csharp
// Don't use old syntax when newer is available
public string GetUserName() { return _user.Name; }

// Don't use old collection syntax
var items = new List<string>();

// Don't use object for locks
private readonly object _lock = new object();
```

### Repository Patterns
- Always use `IMongoCollection<TDocument>` with typed POCOs
- Avoid `BsonDocument` except in migration utilities
- Use `[BsonElement("fieldName")]` for schema mapping

### Testing Patterns
- Unit tests: Use fakes, no external dependencies
- Integration tests: Always use Testcontainers fixtures
- Never rely on local Docker state in tests

## Development Workflow

### Starting Work
```bash
# 1. Ensure infrastructure is running
docker compose -f src/docker-compose.yml up -d

# 2. Start the service you're working on
dotnet run --project src/ScreepsDotNet.Backend.Http/ScreepsDotNet.Backend.Http.csproj

# 3. Or explore CLI
dotnet run --project src/ScreepsDotNet.Backend.Cli/ScreepsDotNet.Backend.Cli.csproj -- --help
```

### Before Committing
```bash
# 1. Format code
dotnet format style

# 2. Run tests
dotnet test src/ScreepsDotNet.slnx

# 3. Verify git status from correct directory
cd ScreepsDotNet
git status  # Should NOT show ScreepsNodeJs/ changes
```

## Storage Architecture

### MongoDB Collections
```javascript
// Key collections (screeps database)
users               // User accounts, auth, badges
rooms               // Room state, objects, terrain
rooms.objects       // Game objects (creeps, structures, etc.)
rooms.terrain       // Terrain data (walls, plains, swamp)
rooms.history       // Historical room snapshots
market.orders       // Market orders
users.code          // User code branches
users.memory        // User memory + segments
users.notifications // User notification queue
users.power_creeps  // Power creep definitions
```

### Redis Keys
```
roomsQueue          # Room processing queue
runtimeQueue        # User runtime execution queue
```

### Seeding
Seeds run automatically when `mongo-data` volume is empty:
- `src/docker/mongo-init/seed-server-data.js` - Server metadata
- `src/docker/mongo-init/seed-users.js` - test-user, ally-user, starter rooms

**Reset workflow:**
```bash
# Full reset (Mongo + Redis)
docker compose -f src/docker-compose.yml down -v
docker compose -f src/docker-compose.yml up -d

# Mongo only
docker volume rm screepsdotnet_mongo-data
docker compose -f src/docker-compose.yml up -d mongo
```

## Subsystems Overview

### Backend HTTP (`src/ScreepsDotNet.Backend.Http/`)
- ASP.NET Core API serving `/api/*` routes
- Mirrors legacy Screeps server HTTP surface
- Uses `.http` scratch files for manual testing
- See `docs/backend.md` for endpoint coverage

### Backend CLI (`src/ScreepsDotNet.Backend.Cli/`)
- Spectre.Console-based admin tool
- Commands: storage, world, bot, auth, map, system, user
- Supports `--format table|markdown|json`
- See `docs/cli.md` for command reference

### Storage (`src/ScreepsDotNet.Storage.MongoRedis/`)
- Shared repositories for MongoDB/Redis
- Used by HTTP, CLI, and Driver layers
- POCOs map to legacy schema
- Strongly typed, no raw `BsonDocument`

### Driver (`src/ScreepsDotNet.Driver/`)
- Runtime coordination and user code execution
- Queue infrastructure, worker scheduler
- Processor loop for intent application
- Native pathfinder integration
- **See `src/ScreepsDotNet.Driver/CLAUDE.md` for details**

### Engine (`src/ScreepsDotNet.Engine/`)
- Managed rewrite of simulation kernel
- Replaces legacy Node.js engine
- Currently in development (see roadmap)
- **See `src/ScreepsDotNet.Engine/CLAUDE.md` for details**

### Native Pathfinder (`src/native/pathfinder/`)
- C++ pathfinder from upstream Screeps
- P/Invoke bindings for .NET
- Multi-platform builds (linux, windows, osx × x64/arm64)
- **See `src/native/pathfinder/CLAUDE.md` for details**

## Common Tasks

### Adding a New HTTP Endpoint
1. Add route handler in `src/ScreepsDotNet.Backend.Http/Endpoints/`
2. Add service logic in `src/ScreepsDotNet.Backend.Core/Services/`
3. Add repository method if needed in `src/ScreepsDotNet.Storage.MongoRedis/`
4. Create `.http` scratch file for manual testing
5. Add integration test in `src/ScreepsDotNet.Backend.Tests/`
6. Update `docs/http-endpoints.md` with route documentation
7. Update `docs/backend.md` feature coverage list

### Adding a New CLI Command
1. Add command in `src/ScreepsDotNet.Backend.Cli/Commands/`
2. Follow Spectre.Console patterns (settings class + async execute)
3. Reuse services from `Backend.Core`
4. Support `--format table|markdown|json` for output
5. Add help text and examples
6. Update `docs/cli.md` command reference

### Adding a New Storage Collection
1. Define POCO in `src/ScreepsDotNet.Storage.MongoRedis/Repositories/Documents/`
2. Add repository interface + implementation
3. Register in DI container
4. Add seed data in `src/docker/mongo-init/*.js`
5. Update `SeedDataDefaults.cs` for Testcontainers
6. Add migration if schema changes

### Debugging
- HTTP: Set breakpoint, F5 in IDE, or `dotnet run` + attach debugger
- CLI: `dotnet run --project ... -- <command>` or F5 with launch args
- Tests: Use IDE test runner or `dotnet test --filter "FullyQualifiedName~YourTest"`
- Docker logs: `docker compose -f src/docker-compose.yml logs -f mongo|redis`

## Current Focus (High-Level)

1. **Backend HTTP/CLI** - Shard-aware write APIs, intent/bot tooling parity
2. **Driver** - Milestones D6-D10 (see `src/ScreepsDotNet.Driver/CLAUDE.md`)
3. **Engine** - Data model + processor porting (see `src/ScreepsDotNet.Engine/CLAUDE.md`)

## Documentation Map

**For AI context:**
- This file - Solution-wide context
- `src/ScreepsDotNet.Driver/CLAUDE.md` - Driver subsystem
- `src/ScreepsDotNet.Engine/CLAUDE.md` - Engine subsystem
- `src/native/pathfinder/CLAUDE.md` - Pathfinder subsystem

**For human readers:**
- `README.md` - Project overview
- `docs/getting-started.md` - Setup tutorial
- `docs/backend.md` - HTTP API coverage
- `docs/http-endpoints.md` - Route reference
- `docs/cli.md` - CLI command reference
- `docs/driver.md` - Driver design overview
- `docs/README.md` - Documentation ownership map

## Anti-Patterns to Avoid

❌ **Don't:**
- Add `using System;` or other implicit usings manually
- Use `new List<T>()` instead of `[]` for empty collections
- Use `object` for locks instead of `Lock`
- Access Mongo/Redis directly in Driver/Engine (use abstractions)
- Run tests against local Docker instead of Testcontainers
- Mix `ScreepsNodeJs/` changes with `ScreepsDotNet/` changes
- Add documentation in code comments instead of docs/
- Create TODO comments instead of tracking in roadmaps

✅ **Do:**
- Use Context7 MCP for library documentation
- Run `dotnet format style` before committing
- Use Testcontainers for integration tests
- Update docs when changing functionality
- Check `git status` from `ScreepsDotNet/` directory
- Follow the repository patterns shown above
- Ask for clarification when requirements are unclear

## When Stuck

1. Check subsystem CLAUDE.md (Driver, Engine, Pathfinder)
2. Check `docs/` for detailed design
3. Search codebase for similar patterns
4. Check test files for usage examples
5. Ask user for clarification

## Maintenance Notes

- Keep this file under 500 lines (move detail to subsystem files)
- Update when solution-wide patterns change
- Don't duplicate subsystem-specific info
- Keep examples current with actual code
