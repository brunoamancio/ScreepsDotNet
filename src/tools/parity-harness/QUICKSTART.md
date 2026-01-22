# Node.js Parity Harness - Quick Start Guide

## Overview

The Node.js parity harness executes Screeps engine fixtures using the official Node.js implementation and outputs JSON for comparison with the .NET engine.

**Status:** ✅ Phase 1 Complete (2026-01-22)

## Prerequisites

- Node.js 10.13.0+ (as required by official Screeps packages)
- MongoDB 7 (running on localhost:27017 or configured via MONGO_URL)
- npm 6+

## Setup (One-Time)

### 1. Install Dependencies

```bash
cd tools/parity-harness
npm install
```

### 2. Clone Official Screeps Repositories

```bash
npm run setup
```

This runs `engine/scripts/clone-repos.sh` which clones:
- `screeps/engine` from GitHub
- `screeps/driver` from GitHub  
- `screeps/common` from GitHub

All cloned into `screeps-modules/` (gitignored).

### 3. Verify Setup

```bash
ls -la screeps-modules/
# Should show: engine/, driver/, common/
```

## Running Fixtures

### Single Fixture Execution

```bash
cd engine/test-runner
node run-fixture.js ../examples/harvest_basic.json --output harvest_basic.node.json
```

**Output:**
- Fixture loaded into MongoDB test database
- Processor executed with mocked infrastructure
- Mutations, stats, and final state serialized to JSON
- JSON written to `harvest_basic.node.json`

### Custom MongoDB URL

```bash
MONGO_URL=mongodb://custom-host:27017 node run-fixture.js fixture.json
```

## Fixture Format

Fixtures are JSON files with the following structure:

```json
{
  "gameTime": 100,
  "room": "W1N1",
  "shard": "shard0",
  "terrain": "",
  "objects": [
    {
      "_id": "creep1",
      "type": "creep",
      "x": 10,
      "y": 10,
      "body": [{"type": "work", "hits": 100}],
      "store": {"energy": 50}
    }
  ],
  "intents": {
    "user1": {
      "creep1": [
        {"intent": "harvest", "id": "source1"}
      ]
    }
  },
  "users": {
    "user1": {
      "gcl": {"level": 1, "progress": 0},
      "power": 0,
      "cpu": 100
    }
  }
}
```

See `engine/examples/harvest_basic.json` for a complete example.

## Output Format

```json
{
  "mutations": {
    "patches": [
      {"objectId": "creep1", "store": {"energy": 54}}
    ],
    "upserts": [],
    "removals": []
  },
  "stats": {
    "user1.energyHarvested": 4
  },
  "actionLogs": {
    "creep1": {"harvest": {"x": 11, "y": 10}}
  },
  "finalState": {
    "creep1": { /* full object state */ }
  }
}
```

## Troubleshooting

### Error: "Cannot find module '../screeps-modules/engine'"

Run `npm run setup` to clone official repositories.

### Error: "MongoDB connection failed"

Ensure MongoDB 7 is running:
```bash
docker run -d -p 27017:27017 mongo:7
```

Or set `MONGO_URL` environment variable.

### Error: "Unknown intent type"

Check `processor-executor.js` `INTENT_PROCESSOR_MAP` to ensure the intent is mapped to a processor file path.

## Next Steps (Phase 5: Integration)

1. **JSON Fixture Loader in .NET:** Implement `FixtureLoader.cs` to load JSON fixtures into `RoomState` DTOs
2. **NodeJsHarnessRunner Wiring:** Update `NodeJsHarnessRunner.cs` to execute `run-fixture.js` and parse JSON output
3. **Parity Test Updates:** Modify parity tests to compare .NET output with Node.js baseline
4. **CI/CD Integration:** Add GitHub Actions workflow to run parity tests automatically

## Documentation

- **E7 Plan:** `../../docs/engine/e7.md` - Complete implementation plan
- **Parity Tests:** `../../src/ScreepsDotNet.Engine.Tests/Parity/` - .NET test suite
- **Fixture Builder:** `../../src/ScreepsDotNet.Engine.Tests/Parity/Infrastructure/ParityFixtureBuilder.cs`

## Version Pinning

Configure in `versions.json`:

- `pinningEnabled: false` - Always use latest (master branches) - **CURRENT**
- `pinningEnabled: true` - Use pinned commit SHAs from `pins` object

Update pins after validating upstream changes to ensure stable builds.

---

**Created:** 2026-01-22  
**Phase:** 1 of 6 (Node.js Test Harness) ✅  
**Status:** Complete - Ready for Phase 5 integration
