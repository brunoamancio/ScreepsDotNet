# Engine Parity Harness

Node.js test harness for engine parity validation. Executes test fixtures against the official Screeps engine and compares outputs with the .NET Engine implementation.

## Purpose

Validate that the .NET Engine produces **identical simulation results** to the Node.js engine for **ALL implemented game mechanics** (E1-E6, E8 complete). This ensures 100% behavioral parity for gameplay correctness, covering ~95% of game mechanics.

**What's Tested:**
- ✅ E1-E6: All core gameplay (movement, harvest, combat, controller, global systems)
- ✅ E8: Observability (telemetry, diagnostics)

**What's NOT Tested (Not Implemented Yet):**
- ❌ E9: NPC AI logic (keeper/invader pathfinding, targeting, combat)
- ⚠️ E2 Deferred: Event logs, notifications, stats recording (non-gameplay)

**Note:** This harness is built as part of the **E7 milestone** (infrastructure), but tests **all engine features** from all milestones, not just E7. E9 AI will be added to the parity suite when implemented.

## Status

**Phase 1 Complete ✅:** Node.js test harness ready
- ✅ Repository setup
- ✅ Fixture loader (JSON → MongoDB)
- ✅ Processor executor (mocked infrastructure)
- ✅ Output serializer (mutations, stats, action logs)
- ✅ CLI wrapper (`run-fixture.js`)

**Next:** Phase 2 - .NET test runner (4-5 hours)

## Quick Start

### 1. Install Dependencies

```bash
cd tools/parity-harness
npm install
```

### 2. Clone Official Screeps Repositories

```bash
npm run clone:engine
# or
cd engine/scripts && ./clone-repos.sh
```

This will clone the official Screeps repositories (engine, driver, common) into `../screeps-modules/` (gitignored at root level).

### 3. Start MongoDB

The test runner requires MongoDB 7:

```bash
# From repo root
docker compose -f src/docker-compose.yml up -d mongo
```

### 4. Run a Fixture

```bash
cd engine/test-runner
node run-fixture.js ../examples/harvest_basic.json --output harvest.node.json
```

Or from parity-harness root:

```bash
npm run test:engine engine/examples/harvest_basic.json -- --output harvest.node.json
```

## Directory Structure

```
engine/
├── README.md                    # This file
├── scripts/
│   └── clone-repos.sh           # Clones official Screeps repos
├── test-runner/
│   ├── fixture-loader.js        # Loads JSON fixtures into MongoDB
│   ├── processor-executor.js    # Executes Node.js processor
│   ├── output-serializer.js     # Serializes output to JSON
│   └── run-fixture.js           # CLI wrapper (main entry point)
├── fixtures/                    # Test fixtures (Phase 4)
│   ├── Movement/
│   ├── Harvest/
│   ├── Build/
│   └── ... (organized by mechanic)
└── examples/
    └── harvest_basic.json       # Example fixture
```

## Fixture Format

Fixtures are JSON files defining room state, intents, and users:

```json
{
  "gameTime": 100,
  "room": "W1N1",
  "shard": "shard0",
  "terrain": "...",
  "objects": [
    {
      "_id": "creep1",
      "type": "creep",
      "x": 10,
      "y": 10,
      "user": "user1",
      "body": [{"type": "work"}, {"type": "move"}],
      "store": {"energy": 50},
      "hits": 100,
      "hitsMax": 100
    },
    {
      "_id": "source1",
      "type": "source",
      "x": 11,
      "y": 10,
      "energy": 3000,
      "energyCapacity": 3000
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
      "gcl": {"level": 1, "progress": 0, "progressTotal": 1000000},
      "power": 0,
      "cpu": 100
    }
  }
}
```

## Output Format

The test runner produces JSON output with:

```json
{
  "mutations": {
    "patches": [
      {"objectId": "creep1", "store": {"energy": 54}},
      {"objectId": "source1", "energy": 2996}
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
    "creep1": {/* full object state */},
    "source1": {/* full object state */}
  },
  "metadata": {
    "room": "W1N1",
    "gameTime": 100,
    "timestamp": "2026-01-22T01:00:00.000Z"
  }
}
```

## CLI Usage

```bash
node run-fixture.js <fixture-path> [options]

Arguments:
  <fixture-path>        Path to JSON fixture file

Options:
  --output <path>       Write output to file (default: stdout)
  --mongo <url>         MongoDB connection URL (default: mongodb://localhost:27017)
  --help, -h            Show help message

Examples:
  node run-fixture.js ../examples/harvest_basic.json
  node run-fixture.js ../examples/harvest_basic.json --output harvest.node.json
  node run-fixture.js ../examples/harvest_basic.json --mongo mongodb://custom:27017
```

## Supported Intents

The processor executor supports 40+ intent types from all implemented milestones:

**Creep Intents:**
- Movement: `move`, `moveTo`, `pull`
- Harvest: `harvest`, `pickup`, `drop`
- Build/Repair: `build`, `repair`, `dismantle`
- Combat: `attack`, `rangedAttack`, `rangedMassAttack`, `heal`, `rangedHeal`
- Controller: `upgradeController`, `claimController`, `attackController`, `reserveController`
- Resources: `transfer`, `withdraw`
- Misc: `say`, `suicide`, `generateSafeMode`

**Structure Intents:**
- Labs: `runReaction`, `boostCreep`, `unboostCreep`
- Links: `transferEnergy`
- Nukers: `launchNuke`
- Power Spawns: `processPower`
- Spawns: `spawnCreep`, `renewCreep`, `recycleCreep`
- Terminals: `send`
- Towers: `tower-attack`, `tower-heal`, `tower-repair`
- Factories: `produce`
- Observers: `observeRoom`
- Controllers: `activateSafeMode`, `unclaim`

## Version Pinning

The `../versions.json` file controls which Screeps repository versions to use:

```json
{
  "engine": {
    "pinningEnabled": false,
    "pins": {
      "engine": "master",
      "driver": "master",
      "common": "master"
    },
    "lastValidated": "2026-01-22",
    "notes": "Using latest repos. Update pins after validating upstream changes."
  }
}
```

- **`pinningEnabled: false`** - Always use latest `master` branch
- **`pinningEnabled: true`** - Use specific commit SHAs from `pins`

## Troubleshooting

### Error: "Cannot find module '../screeps-modules/engine'"

**Solution:** Run `npm run clone:engine` to clone the official Screeps repositories.

### Error: "MongoNetworkError: connect ECONNREFUSED"

**Solution:** Ensure MongoDB is running:

```bash
docker compose -f src/docker-compose.yml up -d mongo
```

### Error: "Fixture missing required field: X"

**Solution:** Check fixture JSON structure. All fixtures must have:
- `room` (string)
- `gameTime` (number)
- `objects` (array)
- `users` (object)

### Warning: "No processor found for intent: X"

**Solution:** The intent name may not be mapped in `processor-executor.js`. Check the `intentMap` and add missing intents.

## Development

### Adding Support for New Intents

Edit `test-runner/processor-executor.js` and add mapping to `intentMap`:

```javascript
const intentMap = {
    'myNewIntent': 'intents/path/to/processor',
    // ...
};
```

### Updating to Latest Screeps Repos

```bash
cd scripts
./clone-repos.sh  # Will pull latest changes if repos exist
```

## Implementation Phases (E7 Milestone)

Building infrastructure to test all engine features:

| Phase | Time | Status |
|-------|------|--------|
| 1. Node.js Test Harness | 5-6h | ✅ **Complete** |
| 2. .NET Test Runner | 4-5h | ⏳ Pending |
| 3. Comparison Engine | 3-4h | ⏳ Pending |
| 4. Parity Test Suite (40-60 fixtures covering E1-E6) | 6-8h | ⏳ Pending |
| 5. Automation & CI | 2-3h | ⏳ Pending |
| 6. Documentation | 1-2h | ⏳ Pending |

## Related Documentation

- **E7 Milestone Plan:** `../../../docs/engine/e7.md` - Infrastructure implementation plan
- **Engine Roadmap:** `../../../docs/engine/roadmap.md` - E1-E9 milestones (features being tested)
- **E1-E6 Plans:** `../../../docs/engine/e1.md` through `e6.md` - Features covered by parity tests
- **.NET Test Runner:** `../../../src/ScreepsDotNet.Engine.Tests/Parity/` (Phase 2)
- **Multi-Layer Parity:** `../README.md` - Overview of all parity layers

---

**Created:** 2026-01-22
**Last Updated:** 2026-01-22
**Part of:** E7 Milestone – Building Engine Parity Infrastructure
**Tests Cover:** E1-E6, E8 complete (~95% of gameplay) | E9 not implemented yet (AI logic will be added when complete)
