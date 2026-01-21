# Engine Operator Playbooks

**Last Updated:** 2026-01-21

**Purpose:** Debugging workflows and troubleshooting guides for ScreepsDotNet Engine operators. Use these playbooks to diagnose and resolve common Engine issues.

---

## Quick Diagnostics

```bash
# Check overall engine health
screeps-cli engine status

# Inspect specific room
screeps-cli engine room-state W1N1

# Check validation failure patterns
screeps-cli engine validation-stats
```

**HTTP Equivalent:**
```bash
# Authenticate first
TOKEN=$(curl -s -X POST http://localhost:5210/api/auth/steam-ticket \
  -H "Content-Type: application/json" \
  -d '{"ticket":"your-ticket","useNativeAuth":false}' | jq -r .token)

# Check engine status
curl -H "X-Token: $TOKEN" http://localhost:5210/api/game/engine/status | jq .

# Get room state
curl -H "X-Token: $TOKEN" "http://localhost:5210/api/game/engine/room-state?room=W1N1" | jq .

# Get validation stats
curl -H "X-Token: $TOKEN" http://localhost:5210/api/game/engine/validation-stats | jq .
```

---

## Playbook 1: High Rejection Rate

### Symptom
Validation rejection rate > 10% (indicated by high `rejectedIntentsCount` in validation stats)

### Diagnosis Steps

1. **Get current validation statistics:**
   ```bash
   screeps-cli engine validation-stats
   ```

2. **Identify top error codes:**
   - Look for the "Top Rejection Errors" table
   - Note which error codes appear most frequently

3. **Check top rejected intent types:**
   - Examine the "Rejections by Intent Type" section
   - Identify which intents are failing most often

4. **Examine room state:**
   ```bash
   screeps-cli engine room-state W1N1 --json
   ```
   - Check object counts (are there too many/few objects?)
   - Verify intent distribution looks reasonable

### Common Causes

#### OUT_OF_RANGE (Most Common)
**Symptom:** Creeps attempting actions beyond their interaction range

**Root Causes:**
- Pathfinding not completing before action intents
- Creeps moving while trying to interact with distant objects
- Incorrect range calculations in user code

**Resolution:**
1. Check intent processing order in RoomProcessor:
   - Movement intents should process before action intents
   - Verify `IntentValidationStep` runs after position updates
2. Review user code for range checks before issuing intents
3. Enable intent tracing to see exact failure points:
   ```bash
   # Reset stats and monitor fresh data
   screeps-cli engine validation-stats --reset
   # Wait a few ticks
   screeps-cli engine validation-stats
   ```

#### NOT_ENOUGH_RESOURCES
**Symptom:** Insufficient energy/resources for requested actions

**Root Causes:**
- Spawn energy depletion faster than replenishment
- Transfer intents not executing before harvest/build
- Resource accounting mismatch between intent validation and execution

**Resolution:**
1. Check spawn energy levels:
   ```bash
   screeps-cli engine room-state W1N1 | grep -A5 "spawn"
   ```
2. Verify transfer intents execute in correct order
3. Review resource ledger in mutation writer (check for double-spending)
4. Examine user code energy management logic

#### INVALID_TARGET
**Symptom:** Target object doesn't exist or is wrong type

**Root Causes:**
- Stale object IDs in user code (object destroyed since last tick)
- Type mismatches (e.g., trying to attack a container)
- Intent targeting objects in different rooms

**Resolution:**
1. Check object lifecycle in room state:
   ```bash
   screeps-cli engine room-state W1N1 --json > room-state.json
   # Examine objects collection for missing IDs
   ```
2. Verify intent validation checks object existence before type validation
3. Review user code to ensure object ID refresh logic is correct
4. Check for cross-room intent targeting (should be rejected early)

### Expected Outcome
- Rejection rate drops below 5%
- Top error codes shift from validation errors to expected edge cases
- User code fixes deployed to avoid common validation failures

---

## Playbook 2: Slow Room Processing

### Symptom
Engine telemetry shows `ProcessingTimeMs > 100ms` for specific rooms

### Diagnosis Steps

1. **Identify slow rooms:**
   ```bash
   screeps-cli engine status
   # Note "Avg processing time (ms)" - if > 50ms, investigate
   ```

2. **Get room state for slow room:**
   ```bash
   screeps-cli engine room-state W1N1 --json > slow-room.json
   ```

3. **Check object counts:**
   ```bash
   cat slow-room.json | jq '.objects | length'
   cat slow-room.json | jq '.objects | group_by(.type) | map({type: .[0].type, count: length})'
   ```

4. **Check intent counts:**
   ```bash
   cat slow-room.json | jq '.intents | length'
   cat slow-room.json | jq '.intents | group_by(.intentType) | map({type: .[0].intentType, count: length})'
   ```

5. **Enable per-step timing (if supported in E8.1+):**
   ```bash
   # In appsettings.json, set:
   # "Engine": { "CollectStepTimings": true }
   # Restart server and check telemetry for step breakdown
   ```

### Common Bottlenecks

#### High Object Count (>200 objects)
**Symptom:** Room has excessive objects, slowing down state serialization

**Root Causes:**
- Too many construction sites
- Excessive ramparts/walls
- Memory leak in object creation

**Resolution:**
1. Check construction site limits:
   ```bash
   cat slow-room.json | jq '[.objects[] | select(.type == "constructionSite")] | length'
   ```
2. Review rampart/wall counts (should be < 50 per room)
3. Verify object cleanup logic in Engine processors
4. Consider adding object count limits in user code

#### Excessive Intent Volume (>100 intents/tick)
**Symptom:** IntentValidationStep takes >30ms

**Root Causes:**
- User code issuing duplicate intents
- Too many creeps in single room (>50)
- Complex pathfinding creating many move intents

**Resolution:**
1. Check intent distribution:
   ```bash
   screeps-cli engine validation-stats
   # Look for intent types with unusually high counts
   ```
2. Review user code for intent deduplication
3. Consider creep count limits per room (recommend <30 for performance)
4. Optimize pathfinding to reduce move intent churn

#### Nuker Operations
**Symptom:** Rooms with nukers have 2-3x higher processing time

**Root Causes:**
- Nuker range calculations are O(n²) with room objects
- Nuke effect processing is computationally expensive

**Resolution:**
1. Verify nuker count per room (should be ≤1)
2. Check if nuke effects are stacking (multiple active nukes)
3. Review `NukerHandlers` for optimization opportunities
4. Consider caching nuker range calculations

### Expected Outcome
- Room processing time drops below 50ms average
- No single room exceeds 100ms consistently
- Step timing breakdown (if enabled) shows no single step >30ms

---

## Playbook 3: Memory Leaks

### Symptom
Engine heap usage grows unbounded over time, eventually causing OutOfMemoryException

### Diagnosis Steps

1. **Monitor heap usage over time:**
   ```bash
   # Check Driver runtime telemetry (if integrated)
   # Look for HeapUsedBytes growing without bound
   ```

2. **Check room state sizes:**
   ```bash
   for room in W1N1 W2N1 W1N2; do
     size=$(screeps-cli engine room-state $room --json | wc -c)
     echo "$room: $size bytes"
   done
   ```

3. **Identify rooms with excessive state:**
   - Rooms >1MB may indicate memory leaks
   - Compare room sizes over multiple ticks

4. **Check mutation writer queue sizes:**
   ```bash
   # Enable debug logging
   export DOTNET_LOG_LEVEL=Debug
   # Look for "Mutation queue size: X" in logs
   ```

### Common Causes

#### Unbounded Intent Accumulation
**Symptom:** Intent queue grows infinitely without processing

**Root Causes:**
- Intent validation failures not clearing intents
- Mutation writer not flushing correctly
- Dead-letter intents accumulating in queue

**Resolution:**
1. Verify mutation writer `Reset()` called after each flush:
   ```csharp
   // In RoomProcessor.ProcessAsync finally block
   writer.Reset();
   ```
2. Check for exceptions during flush (may leave intents in queue)
3. Review intent lifecycle: validate → execute → clear
4. Add telemetry for intent queue depth

#### Room State Caching Issues
**Symptom:** Old room states not being released from memory

**Root Causes:**
- `IRoomStateProvider` not invalidating stale states
- Room state snapshots retained in processor steps
- Circular references preventing GC

**Resolution:**
1. Verify `IRoomStateProvider.Invalidate()` called after processing:
   ```csharp
   // After processing complete
   roomStateProvider.Invalidate(roomName);
   ```
2. Check for `RoomState` references held beyond processing scope
3. Review processor step implementations for state retention
4. Use weak references for cached room states if needed

#### Validation Statistics Not Resetting
**Symptom:** `IValidationStatisticsSink` grows unbounded

**Root Causes:**
- Validation stats not reset after telemetry export
- Error code/intent type dictionaries growing without limits

**Resolution:**
1. Verify `ValidationStatisticsSink.Reset()` called after each tick:
   ```csharp
   // In RoomProcessor after telemetry emit
   validationStatsSink?.Reset();
   ```
2. Check for concurrent access issues (thread safety)
3. Add maximum dictionary size limits (cap at 1000 entries)
4. Periodically clear statistics:
   ```bash
   screeps-cli engine validation-stats --reset
   ```

### Expected Outcome
- Heap usage stabilizes below 500MB for typical workloads
- No unbounded growth over 24+ hour runs
- GC pressure remains reasonable (Gen0 collections <1000/hour)

---

## Playbook 4: Intent Processing Errors

### Symptom
Intents fail to execute despite passing validation (mutations not applied)

### Diagnosis Steps

1. **Check mutation writer patches:**
   ```bash
   # Enable debug logging for mutation writers
   export DOTNET_LOG_LEVEL=Debug
   # Look for "Applying X patches to room Y" in logs
   ```

2. **Verify intent validation vs. execution mismatch:**
   ```bash
   screeps-cli engine validation-stats
   # Compare TotalIntentsValidated vs. actual mutations applied
   ```

3. **Examine room state before/after processing:**
   ```bash
   # Capture state at tick N
   screeps-cli engine room-state W1N1 --json > before.json
   # Wait one tick
   screeps-cli engine room-state W1N1 --json > after.json
   # Diff to see what changed
   diff <(jq -S . before.json) <(jq -S . after.json)
   ```

### Common Causes

#### Handler Not Registered
**Symptom:** Intent type has no corresponding handler

**Root Causes:**
- New intent type added without registering handler
- Handler DI registration missing
- Intent key mismatch (e.g., "moveTo" vs. "move")

**Resolution:**
1. Check handler registration in `ServiceCollectionExtensions`:
   ```csharp
   services.AddSingleton<IIntentHandler<MoveIntent>, MoveHandler>();
   ```
2. Verify intent key matches handler's `IntentType` property
3. Review intent validation schema matches handler expectations
4. Add integration test for new intent type end-to-end

#### Mutation Not Flushing
**Symptom:** Mutations queued but not persisted to storage

**Root Causes:**
- `FlushAsync()` not called or throws exception
- Transaction failure in bulk writer
- Storage connection issues (MongoDB/Redis)

**Resolution:**
1. Verify flush sequence in RoomProcessor:
   ```csharp
   await writer.FlushAsync(token).ConfigureAwait(false);
   await globalWriter.FlushAsync(token).ConfigureAwait(false);
   ```
2. Check for exceptions during flush (should log errors)
3. Verify storage health:
   ```bash
   curl http://localhost:5210/health
   # Should return "Healthy"
   ```
4. Review MongoDB/Redis logs for connection errors

#### Validation-Execution Race Condition
**Symptom:** Intent valid at validation time, invalid at execution time

**Root Causes:**
- Object state changed between validation and execution
- Concurrent modification by another intent
- Resource depletion mid-tick

**Resolution:**
1. Verify validation happens immediately before execution
2. Check for resource ledger updates between validation and execution
3. Review intent ordering (e.g., transfer before build)
4. Add retry logic for transient failures (e.g., resource temporarily unavailable)

### Expected Outcome
- All validated intents execute successfully (or fail with expected errors)
- Mutation patch counts match validated intent counts
- No silent intent failures (all errors logged)

---

## Playbook 5: Validation Error Reference

### ValidationErrorCode Enumeration

| Error Code | Description | Common Causes | User Fix |
|------------|-------------|---------------|----------|
| `OK` | Intent is valid | N/A | N/A |
| `INVALID_TARGET` | Target object doesn't exist or wrong type | Stale object ID, type mismatch | Refresh object IDs before intent |
| `OUT_OF_RANGE` | Target too far from creep | Movement not complete, range calc error | Check creep position before action |
| `NOT_OWNER` | User doesn't own target object | Attempting to control enemy structure | Verify ownership before intent |
| `NOT_ENOUGH_RESOURCES` | Insufficient energy/resources | Energy depleted, transfer not complete | Check store levels before action |
| `INVALID_ARGS` | Intent parameters invalid | Missing required args, type mismatch | Validate args in user code |
| `BUSY` | Target busy with another action | Spawn already spawning, tower attacking | Check busy flag before intent |
| `FULL` | Target storage full | Container/storage at capacity | Check `store.getFreeCapacity()` |
| `TIRED` | Creep fatigued | Movement on swamp/road, carry weight | Wait for fatigue to clear |
| `NO_BODYPART` | Creep missing required body part | No WORK for build, no ATTACK for attack | Check body composition |
| `NOT_IN_RANGE` | Alias for `OUT_OF_RANGE` | Same as OUT_OF_RANGE | Same as OUT_OF_RANGE |
| `GCL_NOT_ENOUGH` | Insufficient Global Control Level | Trying to claim room beyond GCL | Upgrade GCL first |
| `RCL_NOT_ENOUGH` | Insufficient Room Control Level | Structure requires higher RCL | Upgrade controller first |
| `CONSTRUCTION_SITE_LIMIT` | Too many construction sites | >100 sites already exist | Cancel old sites |
| `COOLDOWN` | Structure on cooldown | Nuker/lab/factory cooling down | Wait for cooldown to expire |

### Example Validation Failures

#### Example 1: OUT_OF_RANGE (Harvest)
```javascript
// User code (JavaScript)
creep.harvest(source);  // Fails if creep not adjacent to source

// Intent validation
{
  "intentType": "harvest",
  "target": "source-id",
  "error": "OUT_OF_RANGE",
  "reason": "Creep at (25, 25), source at (30, 30), range 7 > 1"
}
```

**Fix:**
```javascript
// Move to source first
if (!creep.pos.isNearTo(source)) {
  creep.moveTo(source);
} else {
  creep.harvest(source);
}
```

#### Example 2: NOT_ENOUGH_RESOURCES (Transfer)
```javascript
// User code
creep.transfer(spawn, RESOURCE_ENERGY, 500);  // Fails if creep has <500 energy

// Intent validation
{
  "intentType": "transfer",
  "target": "spawn-id",
  "resourceType": "energy",
  "amount": 500,
  "error": "NOT_ENOUGH_RESOURCES",
  "reason": "Creep has 200 energy, requested 500"
}
```

**Fix:**
```javascript
// Check energy before transfer
const amount = Math.min(creep.store[RESOURCE_ENERGY], 500);
if (amount > 0) {
  creep.transfer(spawn, RESOURCE_ENERGY, amount);
}
```

#### Example 3: INVALID_TARGET (Attack)
```javascript
// User code
creep.attack(targetId);  // Fails if targetId doesn't exist

// Intent validation
{
  "intentType": "attack",
  "target": "hostile-creep-id",
  "error": "INVALID_TARGET",
  "reason": "Object hostile-creep-id not found in room"
}
```

**Fix:**
```javascript
// Refresh object before attack
const hostile = Game.getObjectById(targetId);
if (hostile && hostile.hits > 0) {
  creep.attack(hostile);
}
```

---

## Playbook 6: CLI Quick Reference

### Engine Status
```bash
# Get overall engine statistics
screeps-cli engine status

# JSON output (for scripting)
screeps-cli engine status --json | jq .

# Example output
# ┌─────────────────────────┬─────────┐
# │ Rooms processed         │ 1,234   │
# │ Avg processing time (ms)│ 42.50   │
# │ Total intents validated │ 56,789  │
# │ Rejection rate          │ 3.45%   │
# │ Top error code          │ OUT_OF_RANGE │
# └─────────────────────────┴─────────┘
```

### Room State
```bash
# Get room state snapshot
screeps-cli engine room-state W1N1

# JSON output (for scripting)
screeps-cli engine room-state W1N1 --json > room-state.json

# Example output
# ┌──────────────┬────────┐
# │ Room         │ W1N1   │
# │ Game time    │ 12345  │
# │ Object count │ 87     │
# │ Intent count │ 42     │
# │ Creeps       │ 12     │
# │ Spawns       │ 1      │
# │ Extensions   │ 10     │
# └──────────────┴────────┘

# Extract specific data
screeps-cli engine room-state W1N1 --json | jq '.objects | length'
screeps-cli engine room-state W1N1 --json | jq '.intents | group_by(.intentType) | length'
```

### Validation Statistics
```bash
# Get validation stats
screeps-cli engine validation-stats

# Reset stats (clears counters)
screeps-cli engine validation-stats --reset

# JSON output
screeps-cli engine validation-stats --json | jq .

# Example output
# ┌──────────────────┬────────┐
# │ Total validated  │ 1,234  │
# │ Valid intents    │ 1,189  │
# │ Rejected intents │ 45     │
# │ Rejection rate   │ 3.65%  │
# └──────────────────┴────────┘
#
# Top Rejection Errors:
# ┌──────────────────┬────────┐
# │ Error Code       │ Count  │
# ├──────────────────┼────────┤
# │ OUT_OF_RANGE     │ 28     │
# │ NOT_ENOUGH_RESOURCES │ 12 │
# │ INVALID_TARGET   │ 5      │
# └──────────────────┴────────┘

# Track validation over time
screeps-cli engine validation-stats --reset
sleep 60  # Wait one minute
screeps-cli engine validation-stats
```

### Common Workflows

#### Diagnose High Rejection Rate
```bash
# 1. Check current stats
screeps-cli engine validation-stats

# 2. Identify problematic room
screeps-cli engine status

# 3. Inspect room state
screeps-cli engine room-state W1N1 --json > W1N1.json

# 4. Analyze intent distribution
cat W1N1.json | jq '.intents | group_by(.intentType) | map({type: .[0].intentType, count: length})'

# 5. Reset and monitor fresh data
screeps-cli engine validation-stats --reset
# ... wait a few ticks ...
screeps-cli engine validation-stats
```

#### Monitor Room Processing Performance
```bash
# 1. Get baseline
screeps-cli engine status

# 2. Check slow rooms
for room in W1N1 W2N1 W1N2; do
  echo "=== $room ==="
  screeps-cli engine room-state $room
done

# 3. Compare object counts
for room in W1N1 W2N1 W1N2; do
  count=$(screeps-cli engine room-state $room --json | jq '.objects | length')
  echo "$room: $count objects"
done

# 4. Track over time
while true; do
  screeps-cli engine status | grep "Avg processing time"
  sleep 60
done
```

---

## Playbook 7: HTTP Quick Reference

### Prerequisites

```bash
# Authenticate first (get token)
TOKEN=$(curl -s -X POST http://localhost:5210/api/auth/steam-ticket \
  -H "Content-Type: application/json" \
  -d '{"ticket":"your-steam-ticket","useNativeAuth":false}' | jq -r .token)

# Verify token
echo $TOKEN
```

### Engine Status Endpoint

**GET /api/game/engine/status**

```bash
# Get engine statistics
curl -H "X-Token: $TOKEN" \
  http://localhost:5210/api/game/engine/status | jq .

# Example response:
# {
#   "totalRoomsProcessed": 1234,
#   "averageProcessingTimeMs": 42.5,
#   "totalIntentsValidated": 56789,
#   "validIntentsCount": 54678,
#   "rejectedIntentsCount": 2111,
#   "rejectionRate": 0.0372,
#   "topErrorCode": "OUT_OF_RANGE",
#   "topRejectedIntentType": "move"
# }

# Extract specific fields
curl -s -H "X-Token: $TOKEN" \
  http://localhost:5210/api/game/engine/status | jq '.averageProcessingTimeMs'

# Monitor over time
watch -n 5 'curl -s -H "X-Token: '$TOKEN'" \
  http://localhost:5210/api/game/engine/status | jq .averageProcessingTimeMs'
```

### Room State Endpoint

**GET /api/game/engine/room-state?room={roomName}**

```bash
# Get room state
curl -H "X-Token: $TOKEN" \
  "http://localhost:5210/api/game/engine/room-state?room=W1N1" | jq .

# Example response:
# {
#   "roomName": "W1N1",
#   "gameTime": 12345,
#   "info": null,
#   "objects": { "id1": {...}, "id2": {...} },
#   "users": { "userId": {...} },
#   "intents": null,
#   "terrain": { "0,0": {...} },
#   "flags": []
# }

# Count objects by type
curl -s -H "X-Token: $TOKEN" \
  "http://localhost:5210/api/game/engine/room-state?room=W1N1" \
  | jq '.objects | to_entries | group_by(.value.type) | map({type: .[0].value.type, count: length})'

# Extract specific object
curl -s -H "X-Token: $TOKEN" \
  "http://localhost:5210/api/game/engine/room-state?room=W1N1" \
  | jq '.objects["specific-object-id"]'
```

### Validation Stats Endpoint

**GET /api/game/engine/validation-stats**

```bash
# Get validation statistics
curl -H "X-Token: $TOKEN" \
  http://localhost:5210/api/game/engine/validation-stats | jq .

# Example response:
# {
#   "totalIntentsValidated": 1234,
#   "validIntentsCount": 1189,
#   "rejectedIntentsCount": 45,
#   "rejectionsByErrorCode": {
#     "OUT_OF_RANGE": 28,
#     "NOT_ENOUGH_RESOURCES": 12,
#     "INVALID_TARGET": 5
#   },
#   "rejectionsByIntentType": {
#     "move": 15,
#     "harvest": 10,
#     "transfer": 8
#   }
# }

# Get rejection rate
curl -s -H "X-Token: $TOKEN" \
  http://localhost:5210/api/game/engine/validation-stats \
  | jq '(.rejectedIntentsCount / .totalIntentsValidated) * 100'

# Top error codes
curl -s -H "X-Token: $TOKEN" \
  http://localhost:5210/api/game/engine/validation-stats \
  | jq '.rejectionsByErrorCode | to_entries | sort_by(.value) | reverse | .[0:5]'
```

### Validation Stats Reset Endpoint

**POST /api/game/engine/validation-stats/reset**

```bash
# Reset validation statistics
curl -X POST -H "X-Token: $TOKEN" \
  http://localhost:5210/api/game/engine/validation-stats/reset

# Response: {"ok": 1}

# Verify stats cleared
curl -H "X-Token: $TOKEN" \
  http://localhost:5210/api/game/engine/validation-stats | jq .
```

### Common Workflows

#### Monitor Engine Health
```bash
#!/bin/bash
# save as monitor-engine.sh

while true; do
  echo "=== Engine Health $(date) ==="

  # Get stats
  curl -s -H "X-Token: $TOKEN" \
    http://localhost:5210/api/game/engine/status \
    | jq '{avgProcessingMs, rejectionRate, totalIntents: .totalIntentsValidated}'

  echo ""
  sleep 10
done
```

#### Compare Room Performance
```bash
#!/bin/bash
# save as compare-rooms.sh

ROOMS=("W1N1" "W2N1" "W1N2" "W2N2")

for room in "${ROOMS[@]}"; do
  echo "=== $room ==="
  curl -s -H "X-Token: $TOKEN" \
    "http://localhost:5210/api/game/engine/room-state?room=$room" \
    | jq '{room, gameTime, objectCount: (.objects | length), intentCount: (.intents | length)}'
done
```

#### Track Validation Errors Over Time
```bash
#!/bin/bash
# save as track-validation.sh

# Reset stats
curl -s -X POST -H "X-Token: $TOKEN" \
  http://localhost:5210/api/game/engine/validation-stats/reset

# Track for 5 minutes
for i in {1..30}; do
  echo "=== Interval $i ($(date)) ==="
  curl -s -H "X-Token: $TOKEN" \
    http://localhost:5210/api/game/engine/validation-stats \
    | jq '{total: .totalIntentsValidated, rejected: .rejectedIntentsCount, rate: ((.rejectedIntentsCount / .totalIntentsValidated) * 100 | floor)}'
  sleep 10
done
```

---

## Additional Resources

### Related Documentation
- [E8 Milestone](./e8.md) - Observability & Tooling implementation details
- [Engine Roadmap](./roadmap.md) - Complete Engine milestone tracking
- [Validation System](./e3.md) - Intent validation architecture (E3)
- [Driver Telemetry](../driver/d8.md) - Runtime telemetry infrastructure (D8)

### Support
- **Issues:** File bug reports at GitHub Issues
- **Questions:** Ask in project Discord/Slack
- **Contributions:** Submit PRs for new playbooks or fixes

---

**End of Operator Playbooks** | **Last Updated:** 2026-01-21
