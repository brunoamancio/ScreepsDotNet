# Parity Harness - Claude Context

**Purpose:** Node.js test harness for executing Screeps engine fixtures using the official implementation and comparing outputs with ScreepsDotNet.Engine.

**Status:** ✅ Phase 1 Complete (2026-01-22) - Harness operational, .NET integration pending (Phase 5)

---

## When to Read This File

You should read this file when:
- Running parity tests against official Node.js Screeps engine
- Adding new intent processors to the harness
- Debugging fixture execution or output serialization
- Updating official Screeps repository versions
- Troubleshooting parity divergences
- Integrating Node.js harness with .NET parity tests (Phase 5)

**For .NET parity test development,** see `src/ScreepsDotNet.Engine.Tests/Parity/` and `src/ScreepsDotNet.Engine/CLAUDE.md`.

---

## Architecture Overview

### Three-Layer Testing Approach

```
┌─────────────────────────────────────────────┐
│  JSON Fixture (Shared Format)               │
│  - Room state, objects, intents, users      │
└─────────────────────────────────────────────┘
                    │
        ┌───────────┴───────────┐
        ▼                       ▼
┌──────────────────┐    ┌──────────────────┐
│ Node.js Runner   │    │ .NET Runner      │
│ (This Harness)   │    │ (ParityTests)    │
│                  │    │                  │
│ 1. Load Fixture  │    │ 1. Load Fixture  │
│ 2. Execute       │    │ 2. Execute       │
│    Processors    │    │    Processors    │
│ 3. Capture       │    │ 3. Capture       │
│    Mutations     │    │    Mutations     │
│ 4. Serialize     │    │ 4. Serialize     │
│    to JSON       │    │    to JSON       │
└──────────────────┘    └──────────────────┘
        │                       │
        └───────────┬───────────┘
                    ▼
        ┌─────────────────────────┐
        │ Comparison Engine       │
        │ (ParityComparator)      │
        │ - Diff mutations        │
        │ - Report divergences    │
        └─────────────────────────┘
```

### Key Components

**1. Fixture Loader** (`engine/test-runner/fixture-loader.js`)
- Loads JSON fixtures into MongoDB test database
- Clears existing data for clean state
- Validates required fields (gameTime, room, shard)

**2. Processor Executor** (`engine/test-runner/processor-executor.js`)
- Mocks Screeps driver infrastructure (bulk writers, stats, event log)
- Loads official Screeps engine processors from `screeps-modules/engine/`
- Executes intents in order
- Captures mutations, stats, and action logs

**3. Output Serializer** (`engine/test-runner/output-serializer.js`)
- Queries final room state from MongoDB
- Serializes mutations (patches, upserts, removals)
- Extracts action logs from `_actionLog` fields
- Formats output to match .NET structure

**4. CLI Wrapper** (`engine/test-runner/run-fixture.js`)
- Entry point: `node run-fixture.js fixture.json --output output.json`
- Orchestrates: load → execute → serialize → output
- Error handling and cleanup

---

## Directory Structure

```
tools/parity-harness/
├── CLAUDE.md                  # This file (AI context)
├── README.md                  # Human documentation
├── QUICKSTART.md              # Setup guide
├── package.json               # npm configuration
├── versions.json              # Version pinning
├── .gitignore                 # Local ignores
├── screeps-modules/           # Cloned official repos (gitignored)
│   ├── engine/               # github.com/screeps/engine
│   ├── driver/               # github.com/screeps/driver
│   └── common/               # github.com/screeps/common
└── engine/                    # Engine parity harness
    ├── README.md
    ├── scripts/
    │   └── clone-repos.sh    # Clone/update official repos
    ├── test-runner/
    │   ├── fixture-loader.js
    │   ├── processor-executor.js
    │   ├── output-serializer.js
    │   ├── run-fixture.js    # CLI entry point
    │   └── package.json
    ├── fixtures/              # Test fixtures (future)
    └── examples/
        └── harvest_basic.json
```

---

## Coding Standards

### Node.js Code Patterns

**✅ Good:**
```javascript
// Use async/await (not callbacks)
async function loadFixture(fixturePath) {
  const client = await MongoClient.connect(MONGO_URL);
  return { client, db };
}

// Use const/let (never var)
const fixtures = await loadFixtures();
let count = 0;

// Use template literals for strings
console.log(`Loaded ${count} objects`);

// Use arrow functions for short callbacks
objects.forEach(obj => {
  roomObjects[obj._id] = obj;
});

// Use Object.keys/entries for iteration
for (const [userId, userData] of Object.entries(fixture.users)) {
  // ...
}
```

**❌ Bad:**
```javascript
// Don't use callbacks
loadFixture(path, function(err, result) { /* ... */ });

// Don't use var
var count = 0;

// Don't use string concatenation
console.log('Loaded ' + count + ' objects');

// Don't use function keyword for short callbacks
objects.forEach(function(obj) { /* ... */ });
```

### Error Handling

**✅ Good:**
```javascript
try {
  const result = await executeProcessor(db, fixture);
  return result;
} catch (error) {
  console.error('Error executing processor:', error.message);
  throw error;  // Re-throw for caller to handle
} finally {
  await client.close();  // Always cleanup
}
```

**❌ Bad:**
```javascript
// Don't swallow errors silently
try {
  await executeProcessor(db, fixture);
} catch (error) {
  // Nothing - error lost!
}

// Don't forget cleanup
const result = await executeProcessor(db, fixture);
return result;  // Client never closed
```

---

## Common Tasks

### Task 1: Run a Single Fixture

```bash
cd tools/parity-harness/engine/test-runner
node run-fixture.js ../examples/harvest_basic.json --output harvest.node.json
```

**Output:** JSON file with mutations, stats, action logs, final state

### Task 2: Add Support for New Intent Type

**Step 1:** Update intent processor mapping in `processor-executor.js`:

```javascript
const INTENT_PROCESSOR_MAP = {
  'harvest': 'creeps/harvest',
  'attack': 'creeps/attack',
  'yourNewIntent': 'creeps/your-new-intent',  // Add mapping
  // ...
};
```

**Step 2:** Verify processor file exists in official Screeps repo:

```bash
ls screeps-modules/engine/src/processor/creeps/your-new-intent.js
```

**Step 3:** Test with fixture containing new intent:

```bash
node run-fixture.js test-fixture.json
```

### Task 3: Update Official Screeps Repositories

**Check current versions:**
```bash
cd screeps-modules/engine
git log -1 --oneline
```

**Update to latest:**
```bash
cd ../../  # Back to parity-harness/
npm run setup  # Re-runs clone-repos.sh (pulls latest)
```

**Pin to specific commit (for stable builds):**

Edit `versions.json`:
```json
{
  "pinningEnabled": true,
  "pins": {
    "engine": "abc123def456...",
    "driver": "789ghi012jkl...",
    "common": "345mno678pqr..."
  }
}
```

Then run `npm run setup` to checkout pinned commits.

### Task 4: Debug Fixture Execution

**Enable verbose MongoDB logging:**
```bash
MONGO_URL=mongodb://localhost:27017 node run-fixture.js fixture.json
```

**Check MongoDB state after execution:**
```bash
mongosh screeps-parity-test
db.rooms.objects.find({ room: 'W1N1' }).pretty()
```

**Add debug logging to processor-executor.js:**
```javascript
console.log('Object before intent:', object);
intentProcessor(object, intent, scope);
console.log('Object after intent:', object);
console.log('Mutations captured:', bulkMutations);
```

### Task 5: Create New Test Fixture

**Pattern:**
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
      "user": "user1",
      "body": [{"type": "work", "hits": 100}],
      "store": {"energy": 50},
      "storeCapacity": 100,
      "hits": 100,
      "hitsMax": 100
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

**Checklist:**
- ✅ All objects have `_id`, `type`, `x`, `y`, `user` (if owned)
- ✅ Intents are nested: `users → objects → intents array`
- ✅ Intent names match `INTENT_PROCESSOR_MAP` keys
- ✅ User data includes GCL, power, CPU

---

## Troubleshooting

### Issue: "Cannot find module '../screeps-modules/engine'"

**Cause:** Official Screeps repositories not cloned

**Fix:**
```bash
cd tools/parity-harness
npm run setup
```

### Issue: "MongoDB connection failed"

**Cause:** MongoDB not running or wrong URL

**Fix:**
```bash
# Start MongoDB with Docker
docker run -d -p 27017:27017 mongo:7

# Or set custom URL
MONGO_URL=mongodb://custom-host:27017 node run-fixture.js fixture.json
```

### Issue: "Unknown intent type: customIntent"

**Cause:** Intent not mapped in `INTENT_PROCESSOR_MAP`

**Fix:** Add mapping in `processor-executor.js` (see Task 2 above)

### Issue: Output JSON doesn't match .NET format

**Cause:** Serialization mismatch

**Fix:** 
1. Check `output-serializer.js` structure
2. Compare with .NET `ParityOutput` record
3. Ensure field names match exactly (camelCase)

### Issue: Processor execution throws error

**Cause:** Official Screeps processor expects different scope structure

**Fix:**
1. Check error message for missing scope properties
2. Update `executeProcessor()` scope object
3. Verify official engine version (may have changed)

---

## Integration with .NET Tests (Phase 5 - Pending)

### Current State (Phase 1-4 Complete)

✅ **Node.js harness operational** - Can execute fixtures and produce JSON output  
✅ **.NET parity tests operational** - 79 tests using `ParityFixtureBuilder`  
❌ **Integration incomplete** - Node.js harness not called from .NET tests

### Phase 5 TODO

**1. JSON Fixture Loader in .NET**
- Implement `FixtureLoader.cs` to parse JSON fixtures into `RoomState` DTOs
- Support shared fixture format (Node.js and .NET can both consume)

**2. NodeJsHarnessRunner Integration**
- Wire `NodeJsHarnessRunner.cs` to execute `run-fixture.js` via `Process.Start()`
- Parse JSON output into `ParityOutput` record
- Handle errors and timeouts

**3. Parity Test Updates**
- Convert existing tests from `ParityFixtureBuilder` to JSON fixtures
- Add baseline outputs from Node.js harness
- Use `ParityComparator` to detect divergences

**4. CI/CD Workflow**
- Add GitHub Actions workflow to run parity tests
- Clone official Screeps repos in CI
- Compare outputs and upload divergence reports

---

## Design Decisions

### Why Mock Driver Infrastructure?

The official Screeps engine expects driver infrastructure (bulk writers, stats sink, event log). Since we're testing the engine in isolation, we create lightweight mocks that:
- ✅ Capture mutations instead of writing to database
- ✅ Capture stats instead of persisting
- ✅ Provide scope structure processors expect
- ❌ Don't implement full driver functionality

**Trade-off:** Simplified infrastructure vs full production behavior. Acceptable because we're testing engine logic, not driver integration.

### Why Clone Official Repos Instead of npm Install?

Official Screeps packages on npm may be outdated or incomplete. Cloning from GitHub ensures:
- ✅ Access to latest engine code
- ✅ Ability to pin specific commits for stability
- ✅ Ability to inspect/debug processor source
- ✅ Version pinning via `versions.json`

**Trade-off:** More complex setup vs guaranteed latest code. Worth it for parity validation.

### Why MongoDB Instead of In-Memory State?

Official Screeps processors expect MongoDB for:
- Room object queries
- Terrain lookups
- User data retrieval

**Trade-off:** Requires MongoDB instance vs simpler in-memory state. Necessary for processor compatibility.

### Why JSON Output Instead of Direct Comparison?

Serializing to JSON allows:
- ✅ Offline comparison (run Node.js harness separately)
- ✅ Baseline storage (version control friendly)
- ✅ Debugging (inspect outputs manually)
- ✅ Future use (compare multiple .NET versions against same baseline)

**Trade-off:** Extra serialization step vs flexibility. Worth it for debugging and version tracking.

---

## Cross-References

**Parent Documentation:**
- `../../docs/engine/e7.md` - E7 implementation plan (Phase 1-6)
- `../../docs/engine/roadmap.md` - E7 milestone tracking
- `../../CLAUDE.md` - Solution-wide context

**.NET Parity Tests:**
- `../../src/ScreepsDotNet.Engine.Tests/Parity/` - .NET test suite
- `../../src/ScreepsDotNet.Engine.Tests/Parity/Infrastructure/ParityFixtureBuilder.cs` - Programmatic fixture builder
- `../../src/ScreepsDotNet.Engine.Tests/Parity/Infrastructure/ParityComparator.cs` - Output comparison engine
- `../../src/ScreepsDotNet.Engine/CLAUDE.md` - Engine subsystem context

**Official Screeps:**
- Engine: https://github.com/screeps/engine
- Driver: https://github.com/screeps/driver
- Common: https://github.com/screeps/common

---

## Quick Reference

### File Purposes

| File | Purpose | When to Modify |
|------|---------|----------------|
| `fixture-loader.js` | Load JSON into MongoDB | Add new fixture fields |
| `processor-executor.js` | Execute engine processors | Add new intent types, update scope |
| `output-serializer.js` | Serialize to JSON | Change output format |
| `run-fixture.js` | CLI entry point | Add CLI flags, change workflow |
| `clone-repos.sh` | Clone official repos | Update repo URLs, add new repos |
| `versions.json` | Version pinning | Pin/unpin versions |

### Commands Quick Reference

```bash
# Setup
npm install          # Install dependencies
npm run setup        # Clone official Screeps repos

# Run fixture
cd engine/test-runner
node run-fixture.js ../examples/harvest_basic.json --output output.json

# Update repos
npm run setup        # Pull latest or checkout pinned versions

# Debug
MONGO_URL=mongodb://localhost:27017 node run-fixture.js fixture.json
mongosh screeps-parity-test  # Inspect database
```

---

**Last Updated:** 2026-01-22  
**Phase:** 1 of 6 (Node.js Test Harness) ✅  
**Status:** Complete - Ready for Phase 5 integration  
**Maintainer:** See `../../docs/engine/e7.md` for roadmap
