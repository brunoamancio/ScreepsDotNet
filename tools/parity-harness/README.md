# Screeps Parity Test Harness

Multi-layer behavioral parity testing framework for ScreepsDotNet. Validates that the .NET implementation produces identical outputs to the official Node.js implementation across all layers of the stack.

## Architecture

```
ScreepsDotNet Stack          Official Screeps Stack
┌─────────────────────┐      ┌──────────────────────┐
│  Backend (HTTP/CLI) │◄────►│  screeps/screeps     │  ← Backend Parity
├─────────────────────┤      ├──────────────────────┤
│  Driver (Runtime)   │◄────►│  screeps/driver      │  ← Driver Parity
├─────────────────────┤      ├──────────────────────┤
│  Engine (Simulation)│◄────►│  screeps/engine      │  ← Engine Parity (E7)
├─────────────────────┤      ├──────────────────────┤
│  Common (Constants) │◄────►│  screeps/common      │  ← Common Parity
└─────────────────────┘      └──────────────────────┘
```

## Layers

### 1. Engine Parity 🚧 In Progress

**Location:** `engine/`
**Status:** Phase 1 complete ✅ (Node.js harness ready)
**Purpose:** Validate game simulation mechanics (intents → room mutations) for **ALL engine features**
**Effort:** 21-28 hours
**Priority:** 🔴 **HIGH** (most critical for gameplay correctness)

Test that .NET Engine produces identical room states, mutations, and stats when processing the same intents and room data as the Node.js engine. Validates **all implemented milestones** (E1-E6, and future E8-E9 as completed).

**Coverage (Implemented Milestones - ~100% of gameplay):**
- ✅ **E1-E2:** All 11 intent handler families (movement, harvest, build, combat, transfer, etc.)
- ✅ **E3:** Intent validation (range checks, resource checks, permission checks)
- ✅ **E4:** Simulation kernel (passive regen, decay, TTL, fatigue, cooldowns)
- ✅ **E5:** Global systems (GCL updates, power processing, keeper lair spawning, nuke landing)
- ✅ **E6:** Engine orchestration (IEngineHost integration)
- ✅ **E8:** Observability (telemetry emission, diagnostics)
- ✅ **E9:** NPC AI logic (keeper/invader pathfinding, targeting, combat)

**NOT Covered:**
- ⚠️ **E2 Deferred:** Event logs, notifications, stats recording (non-gameplay features, non-parity-critical)

**Note:** E7 is the *milestone name* for building this parity infrastructure, not a feature set being tested

[**→ Engine Parity Documentation**](engine/README.md)

---

### 2. Driver Parity 📋 Not Yet Implemented

**Location:** `driver/`
**Status:** Planned
**Purpose:** Validate runtime coordination and bulk operations
**Effort:** 10-15 hours
**Priority:** 🟡 **MEDIUM** (after Engine complete)

Test that .NET Driver processes queues, coordinates ticks, and executes bulk mutations identically to the Node.js driver.

**Coverage:**
- Queue processing (rooms queue, runtime queue)
- Bulk mutation operations (batch size, ordering)
- Runtime lifecycle (startup, tick execution, shutdown)
- Stats aggregation, event log management

[**→ Driver Parity Documentation**](driver/README.md)

---

### 3. Backend Parity 📋 Not Yet Implemented

**Location:** `backend/`
**Status:** Planned
**Purpose:** Validate HTTP API and CLI output
**Effort:** 8-12 hours
**Priority:** 🟢 **LOW** (after Engine and Driver)

Test that .NET Backend HTTP endpoints and CLI commands produce identical responses to the Node.js backend.

**Coverage:**
- HTTP endpoint responses (JSON structure, status codes)
- CLI command outputs (stdout, stderr, exit codes)
- Query parameter handling, error messages
- Request validation

[**→ Backend Parity Documentation**](backend/README.md)

---

### 4. Common Parity 📋 Not Yet Implemented

**Location:** `common/`
**Status:** Planned
**Purpose:** Validate constants, formulas, and utilities
**Effort:** 4-6 hours
**Priority:** 🟢 **LOW** (can run in parallel)

Test that .NET Common library constants, formulas, and utilities match the Node.js common library exactly.

**Coverage:**
- Game constants (resource types, structure types)
- Formula calculations (energy costs, build times, damage)
- Utility functions (distance, position validation, range)
- Data structures (CostMatrix, PathfinderGoal)

[**→ Common Parity Documentation**](common/README.md)

---

## Directory Structure

```
tools/parity-harness/
├── README.md                    # This file - multi-layer overview
├── .gitignore                   # Excludes screeps-modules/, node_modules/
├── package.json                 # npm scripts for each layer
├── versions.json                # Version pinning per layer
├── screeps-modules/             # Cloned official repos (gitignored)
│   ├── engine/
│   ├── driver/
│   └── common/
├── engine/                      # E7: Engine parity harness
│   ├── README.md                # Engine-specific documentation
│   ├── scripts/                 # clone-repos.sh (Linux/Mac), clone-repos.ps1 (Windows)
│   ├── test-runner/             # Node.js test harness
│   ├── fixtures/                # Test fixtures (to be created in Phase 4)
│   └── examples/                # Example fixtures
├── driver/                      # Driver parity harness (placeholder)
│   └── README.md
├── backend/                     # Backend parity harness (placeholder)
│   └── README.md
└── common/                      # Common parity harness (placeholder)
    └── README.md
```

## Quick Start (Engine Parity)

### 1. Install Dependencies

```bash
cd tools/parity-harness
npm install
```

### 2. Clone Official Screeps Repositories

```bash
npm run clone:engine
```

This clones the official Screeps repositories into `screeps-modules/` (gitignored).

### 3. Start MongoDB

```bash
# From repo root
docker compose -f src/docker-compose.yml up -d mongo
```

### 4. Run Engine Parity Test

```bash
npm run test:engine examples/harvest_basic.json -- --output harvest.node.json
```

Or directly:

```bash
node engine/test-runner/run-fixture.js engine/examples/harvest_basic.json --output harvest.node.json
```

## Multi-Room Support

### Overview

The harness now supports multi-room fixtures for testing cross-room operations like Terminal.send. Both single-room and multi-room fixtures are automatically detected and processed correctly.

### Single-Room vs Multi-Room Fixtures

**Single-Room Format** (original):
```json
{
  "gameTime": 100,
  "room": "W1N1",
  "shard": "shard0",
  "terrain": "000...",
  "objects": [...],
  "intents": {
    "user1": {
      "creep1": [...]
    }
  },
  "users": {...}
}
```

**Multi-Room Format** (new):
```json
{
  "gameTime": 100,
  "shard": "shard0",
  "rooms": {
    "W1N1": {
      "terrain": "000...",
      "objects": [...]
    },
    "W2N2": {
      "terrain": "000...",
      "objects": [...]
    }
  },
  "intents": {
    "W1N1": {
      "user1": {
        "terminal1": [...]
      }
    }
  },
  "users": {...}
}
```

**Key Differences:**
- Multi-room uses `rooms` dictionary instead of single `room` field
- Intents nested by room: `intents[roomName][userId][objectId]`
- Output mutations grouped by room

### Running Multi-Room Tests

Test multi-room fixture detection:
```bash
node test-multi-room.js
```

Run Terminal.send parity test:
```bash
node engine/test-runner/run-fixture.js \
  ../../src/ScreepsDotNet.Engine.Tests/Parity/Fixtures/terminal_send.json \
  --mongo mongodb://localhost:27017
```

### Supported Cross-Room Operations

- ✅ Terminal.send (MarketIntentStep)
- ✅ Observer.observeRoom (when implemented in E8)
- ✅ Inter-shard portals (future)
- ✅ Any cross-room game mechanics

### Implementation Details

The multi-room implementation:
1. **Auto-detects** fixture format based on `rooms` field presence
2. **Loads all rooms** into a single flat object map (matching Screeps engine behavior)
3. **Processes intents** across all rooms
4. **Groups mutations** by room in output
5. **Backward compatible** - single-room fixtures still work

Modified files:
- `engine/test-runner/fixture-loader.js` - Multi-room fixture loading
- `engine/test-runner/processor-executor.js` - Multi-room intent processing
- `engine/test-runner/output-serializer.js` - Multi-room output grouping

## Version Pinning

The `versions.json` file controls which official Screeps repository versions to use per layer:

```json
{
  "engine": {
    "pinningEnabled": false,
    "pins": { "engine": "master", "driver": "master", "common": "master" },
    "lastValidated": "2026-01-22",
    "notes": "Using latest repos for engine parity."
  },
  "driver": { "enabled": false },
  "backend": { "enabled": false },
  "common": { "enabled": false }
}
```

- **`pinningEnabled: false`** - Always use latest `master` branch
- **`pinningEnabled: true`** - Use specific commit SHAs from `pins`

## Implementation Status

| Layer | Status | Phase | Progress |
|-------|--------|-------|----------|
| **Engine** | 🚧 In Progress | Phase 1/6 complete | Node.js harness ready ✅ |
| **Driver** | 📋 Not Started | Planning | Documented, no code yet |
| **Backend** | 📋 Not Started | Planning | Documented, no code yet |
| **Common** | 📋 Not Started | Planning | Documented, no code yet |

## Roadmap

**Current Focus:** E7 Milestone (Build Engine Parity Infrastructure)
1. ✅ **Phase 1:** Node.js test harness (complete)
2. ⏳ **Phase 2:** .NET test runner (4-5 hours)
3. ⏳ **Phase 3:** Comparison engine (3-4 hours)
4. ⏳ **Phase 4:** Parity test suite (6-8 hours, 40-60 fixtures covering E1-E6)
5. ⏳ **Phase 5:** Automation & CI (2-3 hours)
6. ⏳ **Phase 6:** Documentation (1-2 hours)

**What E7 Delivers:** Infrastructure to validate ALL engine features (E1-E6 complete, E8-E9 as implemented)

**Next Layers:** Driver → Backend → Common (after E7 infrastructure complete)

## Related Documentation

**Engine Parity:**
- [Engine Parity Harness](engine/README.md) - Implementation guide
- [E7 Milestone Plan](../../docs/engine/e7.md) - Infrastructure implementation plan
- [Engine Roadmap](../../docs/engine/roadmap.md) - E1-E9 milestones (features being tested)

**Other Layers:**
- [Driver Parity](driver/README.md) - Driver parity strategy
- [Backend Parity](backend/README.md) - Backend parity strategy
- [Common Parity](common/README.md) - Common parity strategy

**Official Screeps Repositories:**
- Engine: https://github.com/screeps/engine
- Driver: https://github.com/screeps/driver
- Common: https://github.com/screeps/common

---

**Created:** 2026-01-22
**Last Updated:** 2026-01-22
**Current Focus:** E7 Milestone - Building Engine Parity Infrastructure (Phase 1 complete ✅)
**Tests Cover:** E1-E6, E8 complete (~95% of gameplay) | E9 not implemented yet (AI logic will be added when complete)
