# Screeps Parity Testing Harness

Multi-layer parity testing infrastructure for validating ScreepsDotNet against official Screeps implementations.

## Overview

This harness enables lockstep testing between .NET and Node.js implementations by:
1. Loading test fixtures into both engines
2. Executing processors in parallel
3. Comparing outputs field-by-field
4. Reporting divergences with full context

## Directory Structure

```
parity-harness/
├── README.md              # This file
├── package.json           # npm configuration
├── versions.json          # Version pinning for official repos
├── screeps-modules/       # Cloned official Screeps repos (gitignored)
│   ├── engine/           # github.com/screeps/engine
│   ├── driver/           # github.com/screeps/driver
│   └── common/           # github.com/screeps/common
└── engine/                # E7: Engine parity harness
    ├── README.md         # Engine harness documentation
    ├── scripts/          # Setup and automation scripts
    │   └── clone-repos.sh
    ├── test-runner/      # Node.js test harness
    │   ├── fixture-loader.js
    │   ├── processor-executor.js
    │   ├── output-serializer.js
    │   └── run-fixture.js
    ├── fixtures/         # Test fixtures (JSON)
    └── examples/         # Example fixtures
```

## Quick Start

### 1. Setup Official Screeps Repositories

```bash
cd tools/parity-harness
npm install
npm run setup
```

This clones the official Screeps repositories (engine, driver, common) into `screeps-modules/`.

### 2. Run a Single Fixture

```bash
cd engine/test-runner
node run-fixture.js path/to/fixture.json --output output.json
```

### 3. Compare with .NET Output

From the repository root:

```bash
dotnet test --filter "FullyQualifiedName~ParityTests"
```

## Version Pinning

Configure version pinning in `versions.json`:

- `pinningEnabled: false` - Always use latest (master/main branches)
- `pinningEnabled: true` - Use pinned commit SHAs from `pins` object

## Requirements

- Node.js 10.13.0+ (as required by official Screeps packages)
- MongoDB 7 (for fixture execution)
- npm 6+

## Layer Organization

- **engine/** - E7 Engine parity (this milestone)
- **driver/** - Driver parity (future)
- **backend/** - Backend parity (future)
- **common/** - Common parity (future)

## Documentation

- **E7 Plan:** `docs/engine/e7.md` - Complete implementation plan
- **Roadmap:** `docs/engine/roadmap.md` - E7 milestone tracking
- **Tests:** `src/ScreepsDotNet.Engine.Tests/Parity/` - .NET parity tests

## License

MIT (follows ScreepsDotNet license)
