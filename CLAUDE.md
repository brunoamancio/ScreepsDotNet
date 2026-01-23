# ScreepsDotNet - Claude Context

## Mission

Modern .NET rewrite of the Screeps private server backend. Exposes legacy HTTP + CLI surface while gradually replacing Node.js driver/engine with managed code. Goal: Full-featured private server with better performance, maintainability, and extensibility.

## Critical Rules Checklist

✅ **ALWAYS:**
- Use Context7 MCP for library/API documentation without being asked
- **Use cloned Screeps repos for legacy code reference** - Official Screeps engine/driver/common repos are cloned to `tools/parity-harness/engine/screeps-modules/` during first parity test run. Read Node.js source directly instead of guessing behavior. If repos don't exist, parity tests will auto-clone them on first run.
- Use `var` for all variable declarations (never explicit types)
- Use collection expressions `[]` (never `new List<T>()`)
- Use primary constructors (never classic constructor syntax)
- Use file-scoped namespaces (`namespace Foo;` not `namespace Foo { }`)
- Suffix async methods with "Async"
- Specify accessibility modifiers explicitly (public/private/internal/etc)
- Use `is null` not `== null` for null checks
- Use pattern matching (`if (obj is User user)` not `as` + null check)
- Use trailing commas in multi-line collections (NOT on last item or closing braces)
- Keep lines under 185 characters (don't wrap unnecessarily)
- Use positive conditions in ternary operators (never negate: `condition ? true : false` not `!condition ? false : true`)
- Assign ternary expressions to a variable before returning - applies to ALL ternaries (simple, multi-line, nested, complex)
- Run `dotnet format style --exclude-diagnostics IDE0051 IDE0052 IDE0060` before committing
- Run `git status` from `ScreepsDotNet/` directory (not repo root)
- Use Testcontainers for integration tests (never local Docker state)
- Update plan documents after completing work that follows a documented plan (keep plans in sync with implementation)
- Document deferred features in ALL related plans when skipping functionality during implementation (track what was deferred, why, and where it should be implemented later)
- **Maintain legacy parity at all times** - When adding new features or refactoring, preserve backward compatibility and keep legacy code paths functional (use optional parameters, feature flags, or conditional execution)

❌ **NEVER:**
- Modify files in `ScreepsNodeJs/` (separate git repository)
- Run `dotnet build` while `dotnet run` is active (DLL lock issues)
- Add `using System;` or other implicit usings manually
- Use `BsonDocument` in repositories (use typed POCOs)
- Use `this.` or static class qualifiers for members in same class
- Wrap lines under 185 characters
- Return ternary expressions directly (`return x ? a : b;` → always assign first: `var result = x ? a : b; return result;`)
- Use negated conditions in ternary operators (flip condition and swap values instead)
- Forget `#pragma warning disable IDE0051, IDE0052` for constants used ONLY in attribute parameters
- Run `dotnet format style` without exclusions (always use `--exclude-diagnostics IDE0051 IDE0052 IDE0060`)

## Quick Start

```bash
# 1. Navigate to solution directory
cd ScreepsDotNet

# 2. Start MongoDB + Redis with seed data
docker compose -f src/docker-compose.yml up -d

# 3. Verify seeds loaded
docker compose -f src/docker-compose.yml logs -f mongo

# 4. Run tests
dotnet test src/ScreepsDotNet.slnx

# 5. Start HTTP backend
dotnet run --project src/ScreepsDotNet.Backend.Http/ScreepsDotNet.Backend.Http.csproj
```

**Quick smoke test:**
```http
GET http://localhost:5210/api/server/info
```

## Project Structure

**Solution:** `src/ScreepsDotNet.slnx` (XML-based)
**Working Directory:** Repository root (always run git commands from the ScreepsDotNet directory)

**Key config files:**
- `.editorconfig`: `src/.editorconfig` (coding style - ERROR/WARNING levels)
- `.DotSettings`: `src/ScreepsDotNet.slnx.DotSettings` (ReSharper settings)
- `Directory.Build.props`: `src/Directory.Build.props` (implicit usings, MSBuild)
- Docker Compose: `src/docker-compose.yml` (MongoDB + Redis)

```
ScreepsDotNet/
├── CLAUDE.md                        # This file - AI context
├── docs/                            # Human documentation
│   ├── getting-started.md
│   ├── backend.md
│   ├── http-endpoints.md
│   ├── cli.md
│   ├── driver.md
│   └── common-tasks.md              # Step-by-step guides (moved from CLAUDE.md)
├── ScreepsNodeJs/                   # ⚠️ Separate git repo - DO NOT MODIFY
└── src/
    ├── ScreepsDotNet.slnx           # ⚠️ SOLUTION FILE
    ├── .editorconfig                # ⚠️ Coding style rules
    ├── ScreepsDotNet.slnx.DotSettings  # ⚠️ ReSharper settings
    ├── Directory.Build.props        # ⚠️ Implicit usings, MSBuild
    ├── docker-compose.yml           # ⚠️ MongoDB + Redis
    ├── ScreepsDotNet.Backend.Core/
    ├── ScreepsDotNet.Backend.Http/
    ├── ScreepsDotNet.Backend.Cli/
    ├── ScreepsDotNet.Storage.MongoRedis/
    ├── ScreepsDotNet.Driver/
    │   └── CLAUDE.md                # ⚠️ Driver-specific context
    ├── ScreepsDotNet.Engine/
    │   └── CLAUDE.md                # ⚠️ Engine-specific context
    └── native/pathfinder/
        └── CLAUDE.md                # ⚠️ Pathfinder-specific context
```

## Coding Standards - Quick Reference

**Source of truth:** `src/.editorconfig` and `src/ScreepsDotNet.slnx.DotSettings`
**Detailed examples:** See `.claude/docs/coding-standards-reference.md` (imported below)

| Rule | IDE Code | Quick Example |
|------|----------|---------------|
| Use var | IDE0007 | `var x = 0;` not `int x = 0;` |
| Collection expressions | IDE0028 | `var list = [];` not `new List<T>()` |
| Primary constructors | - | `public class Foo(IBar bar) { }` not classic syntax |
| File-scoped namespaces | IDE0161 | `namespace Foo;` not `namespace Foo { }` |
| Async suffix | ASYNC001 | `GetUserAsync()` not `GetUser()` |
| Pattern matching | IDE0019/20 | `if (obj is User user)` not `as` + null check |
| Null checks | IDE0041 | `is null` not `== null` |
| Expression-bodied members | IDE0022 | `=> expr` on new line, not block syntax |
| Trailing commas | IDE0260 | Arrays/enums: yes; dictionaries/objects: not on last item or braces |
| Positive ternary conditions | - | `success ? a : b` not `!success ? b : a` |
| Assign ternary before return | - | `var r = x ? a : b; return r;` not `return x ? a : b;` |
| Repository POCOs | - | `IMongoCollection<UserDocument>` not `BsonDocument` |
| Testing | - | Testcontainers, not local Docker state |
| Lock primitives | - | `Lock _lock = new();` not `object` |
| Target-typed new | IDE0090 | `new()` not `new Type()` when type is clear from context |
| IDE0051/52 pragma | - | Add pragma for constants in attribute params |
| Dictionary access | - | Use `GetValueOrDefault(key, default)` not `TryGetValue` ternary |
| Tuple deconstruction | IDE0042 | Always deconstruct tuples: `var (id, payload) = Single(...)`, discard unused: `var (_, payload)` |
| Method/record signature length | - | Keep on one line unless exceeds 185 characters (applies to method signatures and record primary constructors) |
| Enums vs constants | - | Use `enum` for integer sets (place in `Types/`), not `static class` with `const int` |

**Implicit usings** (configured in `Directory.Build.props` - never add manually):
1. `System`
2. `System.Collections.Generic`
3. `System.IO`
4. `System.Linq`
5. `System.Net.Http`
6. `System.Threading`
7. `System.Threading.Tasks`

**All other System.* usings are explicit** (must be added): `System.Text`, `System.Text.Json`, `System.Text.RegularExpressions`, etc.

**Constant organization in endpoint classes:**
1. Value constants → 2. Validation/error messages → 3. Endpoint names → 4. Query parameter names → 5. Default string values → 6. Numeric arrays → 7. Numeric limits → 8. Complex defaults (last)

@.claude/docs/coding-standards-reference.md

## Storage Architecture

**Overview:** ScreepsDotNet uses MongoDB 7 (persistent state) and Redis 7 (queues, caching). Implementation in `ScreepsDotNet.Storage.MongoRedis`.

**Detailed documentation:** See [docs/storage/](docs/storage/) for complete reference:
- [overview.md](docs/storage/overview.md) - Architecture, collections, Redis keys
- [mongodb.md](docs/storage/mongodb.md) - Schemas, repository patterns, bulk writers
- [redis.md](docs/storage/redis.md) - Queue patterns, caching, pub/sub
- [seeding.md](docs/storage/seeding.md) - Seed data, test fixtures, reset workflows

### Quick Reference

**MongoDB Collections:** users, users.code, users.memory, rooms, rooms.objects, rooms.terrain, market.orders, servers
**Redis Keys:** roomsQueue, runtimeQueue, gameTime
**Seed Scripts:** `src/docker/mongo-init/` (auto-run when mongo-data volume is empty)

**Reset workflows:**
```bash
# Full reset (Mongo + Redis)
docker compose -f src/docker-compose.yml down -v && docker compose -f src/docker-compose.yml up -d

# Mongo only (faster)
docker volume rm screepsdotnet_mongo-data && docker compose -f src/docker-compose.yml up -d mongo

# Verify seeds ran
docker compose -f src/docker-compose.yml logs -f mongo
```

## Development Workflow

### Daily Development
```bash
cd ScreepsDotNet
docker compose -f src/docker-compose.yml up -d
dotnet run --project src/ScreepsDotNet.Backend.Http/ScreepsDotNet.Backend.Http.csproj

# Before committing
cd src && dotnet format style --exclude-diagnostics IDE0051 IDE0052 IDE0060 ScreepsDotNet.slnx
cd src && dotnet test --filter "Category!=Parity"  # Skip parity tests (faster)
# Or run all tests: cd src && dotnet test ScreepsDotNet.slnx
git status  # Verify ScreepsNodeJs/ is not included
```

### Manual Testing
**HTTP routes:** Use `.http` files in `src/ScreepsDotNet.Backend.Http/`
**CLI commands:** `./src/cli.sh storage list-users` (Linux/Mac) or `pwsh ./src/cli.ps1 storage list-users` (Windows)

### Parity Testing
**Quick Start:**
```bash
# Run parity tests comparing .NET Engine vs Node.js engine
dotnet test --filter Category=Parity

# First run automatically:
# - Detects nvm (if installed), activates Node.js 10.13.0-12.x version
# - Checks Node.js 10.13.0 to 12.x ONLY (rejects Node 13+)
# - Checks Docker running (fails if not running)
# - Clones official Screeps repos (30-60s)
# - Runs npm install (20-30s)
# - Starts MongoDB via Testcontainers
# - Runs 7 parity tests

# Regular tests (skip parity for speed)
dotnet test --filter "Category!=Parity"  # 533 tests in ~250ms
```

**Prerequisites (install once):**
- **Node.js 10.13.0 to 12.x** - Download from https://nodejs.org/dist/latest-v12.x/
  - ⚠️ **Node 13+ NOT supported** - Screeps engine is incompatible
  - 💡 Recommended: Use nvm - `nvm install 12.22.12`
- **Docker** - Download from https://www.docker.com/get-started

**nvm Support:**
- Tests auto-detect nvm (Linux/Mac/Windows)
- Tests find highest Node version in range 10.13.0-12.x
- Tests automatically activate via `nvm use`
- Tests reject Node 13+ with helpful error

**Documentation:** `docs/engine/mongodb-parity-setup.md`

### Test Categories

Tests are organized by category using `[Trait("Category", "...")]` attributes:

| Category | Count | Description | Run Command |
|----------|-------|-------------|-------------|
| **Unit** | 637 | Pure unit tests (no external dependencies, fast) | `dotnet test --filter "Category!=Integration&Category!=Parity"` |
| **Integration** | 240 | Tests using Testcontainers (MongoDB, Redis, WebApplicationFactory) | `dotnet test --filter Category=Integration` |
| **Smoke** | 3 | Quick smoke tests (health, server info, version) | `dotnet test --filter Category=Smoke` |
| **Parity** | 7 | Parity tests comparing .NET vs Node.js engine | `dotnet test --filter Category=Parity` |

**Common workflows:**
```bash
# Fast feedback loop - unit tests only (~250ms)
dotnet test --filter "Category!=Integration&Category!=Parity"

# Integration tests (~5-10s with Testcontainers)
dotnet test --filter Category=Integration

# Quick smoke test (~1-2s)
dotnet test --filter Category=Smoke

# Parity tests (~30-60s first run, requires Node.js 10.13.0-12.x)
dotnet test --filter Category=Parity

# All tests except parity (~5-10s)
dotnet test --filter "Category!=Parity"

# All tests including parity (~35-70s)
dotnet test
```

**Category Guidelines:**
- `Unit` - Implicit for tests without categories, no external dependencies
- `Integration` - Tests that use `IClassFixture<>` with Testcontainers
- `Smoke` - Subset of Integration tests for basic sanity checks (health, version, server info)
- `Parity` - Engine parity tests (also tagged as Integration)

### Debugging
**HTTP backend:** Breakpoint in `src/ScreepsDotNet.Backend.Http/Endpoints/` → F5 → Send request via `.http` file
**CLI:** Breakpoint in `src/ScreepsDotNet.Backend.Cli/Commands/` → F5 with launch args
**Tests:** `dotnet test src/ScreepsDotNet.slnx` or `--filter "FullyQualifiedName~TestName"`
**Docker logs:** `docker compose -f src/docker-compose.yml logs -f mongo`

**Test Output Capturing (xunit v3):**
All test projects support `Console.WriteLine()` for debugging via `[assembly: CaptureConsole]` and `[assembly: CaptureTrace]`:

```csharp
[Fact]
public void MyTest()
{
    Console.WriteLine("Debug info appears in test output!");
    Assert.True(true);
}
```

View output: `dotnet test --filter "MyTest" --logger "console;verbosity=detailed"`

See `.claude/docs/testing-output-capture.md` and `src/ScreepsDotNet.Engine.Tests/_Examples/` for examples.

### Plan Maintenance

When working on tasks that follow a documented plan (e.g., `.claude/plans/*.md`, `docs/engine/*.md`):

1. **During implementation:** Check off completed items in the plan as you finish them
2. **After completion:** Update the plan to reflect the final state:
   - Test counts (ensure actual matches planned)
   - Deferred features (document what was intentionally skipped and why)
   - Success criteria (mark as complete or note blockers)
   - Dates (update "Last Updated" timestamp)
3. **When deferring features:** Document deferrals in ALL related plans:
   - **Current plan:** Add "Deferred Features" section with impact assessment, what's missing, and why
   - **Parent/roadmap plans:** Update progress tracking and move deferred items to appropriate sections (e.g., E2.3 plan when working on controller intents)
   - **Future plans:** Note dependencies so deferred work isn't forgotten when related features are implemented
   - **Example:** Deferring GCL updates in controller intents → document in implementation plan AND E2.3 plan AND note in future global processor work
4. **When finding issues:** If the plan is incorrect or outdated, fix it immediately to prevent confusion

**Example plan locations:**
- Implementation plans: `.claude/plans/` (agent-generated, task-specific)
- Feature roadmaps: `docs/engine/e2.md`, `docs/driver/*.md` (long-term tracking)

**Why this matters:** Plans serve as the source of truth for what was built, what remains, and what was deferred. Keeping them accurate ensures future work doesn't duplicate effort or miss requirements.

### Code Navigation (Serena Plugin)

**If Serena plugin is available,** use it for semantic code navigation instead of text-based search (Grep/Glob).

**Activate project (if not already active):**
```typescript
mcp__plugin_serena_serena__activate_project({ project: "screeps-rewrite" })
// Or use the project name registered in Serena configuration
```

**Common operations:**
```typescript
// Find all references to a symbol
mcp__plugin_serena_serena__find_referencing_symbols({
  name_path: "TryGetBooleanProperty",
  relative_path: "src/ScreepsDotNet.Driver/Extensions/JsonElementExtensions.cs"
})

// Find symbol definition (with or without body)
mcp__plugin_serena_serena__find_symbol({
  name_path_pattern: "UserService/GetUserAsync",
  include_body: true
})

// Get symbol overview of a file
mcp__plugin_serena_serena__get_symbols_overview({
  relative_path: "src/ScreepsDotNet.Backend.Http/Endpoints/UserEndpoints.cs",
  depth: 1
})

// Search for pattern in codebase
mcp__plugin_serena_serena__search_for_pattern({
  substring_pattern: "IMongoCollection",
  relative_path: "src/ScreepsDotNet.Storage.MongoRedis"
})
```

**When to use Serena:**
- Finding all references to a method/class
- Understanding symbol relationships and dependencies
- Navigating large codebases semantically
- Refactoring (find all usages before changing)

**When to use Grep/Glob instead:**
- Simple text search across files
- Searching for strings/comments (non-code)
- Pattern matching that doesn't require semantic understanding

### Common Tasks
See `docs/common-tasks.md` for step-by-step guides:
- Add HTTP endpoint
- Add CLI command
- Add storage collection
- Update seed data
- Troubleshoot common issues

## Subsystem Navigation

This file provides **solution-wide** context. For subsystem-specific details:

### Driver (Runtime Coordination)
**AI Context:** `src/ScreepsDotNet.Driver/CLAUDE.md` ✅
**Roadmap:** `docs/driver/roadmap.md` (D1-D10 complete ✅)

**When to read:**
- Adding processor logic (intent handlers)
- Modifying runtime execution (V8/ClearScript)
- Queue/scheduler changes
- Pathfinder integration
- Telemetry/observability
- Bulk mutation patterns

**Key topics:**
- D1-D10 milestone docs in `docs/driver/` (all complete ✅)
- Code patterns (✅/❌ bulk writers, telemetry, DI)
- Common tasks (add processor handler, wire telemetry, debug runtime)
- Integration contracts (Engine consumes driver abstractions)

### Engine (Simulation Kernel)
**AI Context:** `src/ScreepsDotNet.Engine/CLAUDE.md` ✅
**Roadmap:** `docs/engine/roadmap.md` (E1-E6 complete ✅, E7-E9 pending)

**When to read:**
- Adding intent handlers (creep, structure, controller, combat)
- Porting Node.js engine mechanics
- Testing parity with legacy engine
- Working with game simulation logic
- Understanding Engine↔Driver data flow

**Key topics:**
- E1-E9 milestone docs in `docs/engine/` (E1-E6 complete ✅)
- 🚨 CRITICAL: NEVER access Mongo/Redis directly (use Driver abstractions)
- Code patterns (✅ IRoomStateProvider vs ❌ IMongoDatabase)
- Common tasks (add intent handler, test parity, debug mutations)

### Native Pathfinder (C++ P/Invoke)
**File:** `src/native/pathfinder/CLAUDE.md` ✅

**When to read:**
- Rebuilding native binaries (Linux/Windows/macOS × x64/arm64)
- Updating parity baselines (Node.js vs .NET comparison)
- Adding regression fixtures
- Debugging cross-platform build failures
- Verifying GitHub release binaries

**Key topics:**
- Build instructions per platform (./build.sh, build.ps1)
- Parity testing workflow (100% match with Node.js pathfinder)
- Baseline refresh process (dotnet test /p:RefreshPathfinderBaselines=true)
- CI/CD pipeline (GitHub Actions builds all RIDs)
- P/Invoke patterns (C ABI ↔ C# marshaling)

### Parity Harness (Node.js Test Runner)
**AI Context:** `tools/parity-harness/CLAUDE.md` ✅
**Roadmap:** `docs/engine/e7.md` (Phase 1 complete ✅, Phase 5 pending)

**When to read:**
- Running parity tests against official Node.js Screeps engine
- Adding new intent processors to Node.js harness
- Debugging fixture execution or output serialization
- Updating official Screeps repository versions
- Troubleshooting parity divergences
- Integrating Node.js harness with .NET parity tests

**Key topics:**
- Phase 1 complete: fixture loader, processor executor, output serializer, CLI wrapper
- Official Screeps repo cloning (engine, driver, common from GitHub)
- Version pinning strategy (versions.json)
- Mock infrastructure patterns (bulk writers, stats, event log)
- JSON fixture format (shared between Node.js and .NET)
- Common tasks (run fixture, add intent type, update repos, debug execution)

## Current Focus (High-Level)

1. **Backend HTTP/CLI** - Shard-aware write APIs, intent/bot tooling parity
2. **Driver (D6-D10)** - See `src/ScreepsDotNet.Driver/CLAUDE.md` for details
3. **Engine (E2-E8)** - See `docs/engine/roadmap.md` for detailed roadmap
4. **Documentation** - Keep docs in sync with feature changes

**Roadmaps:**
- Engine: `docs/engine/roadmap.md` (E1-E8 milestones)
- Driver: `src/ScreepsDotNet.Driver/CLAUDE.md` (D1-D10 inline)
- Pathfinder: `src/native/pathfinder/CLAUDE.md` (build/test instructions)

## Documentation Map

### For AI Context
- **This file** - Solution-wide patterns and workflows
- `src/ScreepsDotNet.Driver/CLAUDE.md` - Driver subsystem
- `src/ScreepsDotNet.Engine/CLAUDE.md` - Engine subsystem
- `src/native/pathfinder/CLAUDE.md` - Pathfinder subsystem
- `.claude/docs/coding-standards-reference.md` - Condensed coding examples

### For Human Readers
- `README.md` - Project overview
- `docs/getting-started.md` - Setup tutorial
- `docs/common-tasks.md` - Step-by-step development guides
- **Backend:** `docs/backend/overview.md` - HTTP API coverage | `docs/backend/http-api.md` - Route reference | `docs/backend/cli.md` - CLI commands
- **Driver:** `docs/driver/roadmap.md` - D1-D10 milestones (complete ✅) | `docs/driver/d1-d10.md` - Individual milestone docs
- **Engine:** `docs/engine/roadmap.md` - E1-E9 milestones | `docs/engine/e1-e9.md` - Individual milestone docs | `docs/engine/data-model.md` - Data contracts
- **Storage:** `docs/storage/overview.md` - MongoDB/Redis architecture | `docs/storage/mongodb.md` - Collection schemas | `docs/storage/redis.md` - Queue patterns | `docs/storage/seeding.md` - Seed data

## When Stuck

1. Check subsystem CLAUDE.md (Driver, Engine, Pathfinder) for coding patterns
2. Check `docs/` for plan tracking and design documentation:
   - `docs/driver/roadmap.md` - Driver milestones (D1-D10 complete ✅)
   - `docs/engine/roadmap.md` - Engine milestones (E1-E6 complete ✅)
   - `docs/storage/` - MongoDB/Redis patterns
3. Use Context7 MCP for library/API documentation
4. Search codebase for similar patterns (`rg "pattern" -n src/`)
5. Check test files for usage examples
6. Ask user for clarification

## Maintenance

**Update this file when:**
- Solution-wide coding standards change
- New projects added to solution
- Storage schema changes (major)
- Development workflow changes
- Critical rules change

**Keep it focused:**
- This is for working context, not tutorials
- Tutorials belong in `docs/`
- Subsystem coding patterns belong in subsystem CLAUDE.md
- Plan tracking belongs in `docs/engine/` (roadmap, e2.3-plan, e5-plan, etc.)
- Target: Under 500 lines total

**Last Updated:** 2026-01-21
