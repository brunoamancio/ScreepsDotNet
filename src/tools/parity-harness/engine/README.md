# Engine Parity Harness

Node.js test runner for executing Screeps engine fixtures and comparing with ScreepsDotNet.Engine.

## Purpose

This harness loads JSON test fixtures, executes them using the official Screeps Node.js engine, and serializes the output for comparison with the .NET implementation.

## Components

### 1. Fixture Loader (`test-runner/fixture-loader.js`)

Loads JSON fixtures into MongoDB:
- Room state (objects, terrain, intents)
- Game time, user state
- Expected outputs (optional)

### 2. Processor Executor (`test-runner/processor-executor.js`)

Executes official Screeps engine processors:
- Creates mock infrastructure (bulk writers, stats, event log)
- Invokes intent processors
- Captures mutations and stats

### 3. Output Serializer (`test-runner/output-serializer.js`)

Serializes execution results to JSON:
- Mutations (patches, upserts, removals)
- Stats changes
- Action logs
- Final room state

### 4. CLI Wrapper (`test-runner/run-fixture.js`)

Command-line interface:

```bash
node run-fixture.js path/to/fixture.json --output output.json
```

## Fixture Format

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

```json
{
  "mutations": {
    "patches": [
      {"objectId": "creep1", "store": {"energy": 54}},
      {"objectId": "source1", "energy": 296}
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
  }
}
```

## Requirements

- MongoDB 7 running on localhost:27017 (or configured via environment)
- Official Screeps repositories cloned (run `npm run setup` from `tools/parity-harness/`)
- Node.js 10.13.0+

## Usage

### Single Fixture

```bash
cd test-runner
node run-fixture.js ../examples/harvest_basic.json --output ../fixtures/harvest_basic.node.json
```

### With Custom MongoDB

```bash
MONGO_URL=mongodb://localhost:27017 node run-fixture.js fixture.json
```

## Integration with .NET Tests

The .NET parity tests in `src/ScreepsDotNet.Engine.Tests/Parity/` use `NodeJsHarnessRunner` to execute this harness and compare outputs.

## Troubleshooting

### Error: Cannot find module '../screeps-modules/engine'

Run `npm run setup` from `tools/parity-harness/` to clone official repositories.

### Error: MongoDB connection failed

Ensure MongoDB 7 is running on localhost:27017 or configure MONGO_URL environment variable.

### Error: Processor not found

Verify the intent name mapping in `processor-executor.js` matches the official engine structure.

## Documentation

- **E7 Plan:** `docs/engine/e7.md` - Complete implementation plan (Phase 1)
- **Parity Tests:** `src/ScreepsDotNet.Engine.Tests/Parity/Tests/` - .NET test suite
- **Fixture Builder:** `src/ScreepsDotNet.Engine.Tests/Parity/Infrastructure/ParityFixtureBuilder.cs` - Programmatic fixture creation
