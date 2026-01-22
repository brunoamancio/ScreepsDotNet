# Driver Parity Harness

**Status:** 📋 Not Yet Implemented

## Purpose

Test behavioral parity between .NET Driver and legacy Node.js driver for:
- Queue processing (rooms queue, runtime queue)
- Bulk mutation operations
- Runtime coordination
- Stats aggregation
- Event log management

## Scope

**In Scope:**
- Queue consumer behavior
- Bulk writer operations (batch size, ordering)
- Runtime lifecycle (startup, tick execution, shutdown)
- Inter-component coordination (Driver ↔ Engine)

**Out of Scope:**
- Engine simulation logic (covered by Engine parity)
- HTTP API responses (covered by Backend parity)
- Constants/formulas (covered by Common parity)

## Estimated Effort

- **Time:** 10-15 hours
- **Priority:** 🟡 MEDIUM (after Engine parity complete)
- **Fixtures:** 15-20 test cases

## Fixture Categories

1. **Queue Processing (5 fixtures)**
   - Rooms queue consumption
   - Runtime queue consumption
   - Priority ordering
   - Batch processing
   - Error handling

2. **Bulk Mutations (4 fixtures)**
   - Update batching
   - Insert batching
   - Delete batching
   - Transaction rollback

3. **Runtime Coordination (4 fixtures)**
   - Tick lifecycle
   - Engine invocation
   - Stats aggregation
   - Error propagation

4. **Edge Cases (2-4 fixtures)**
   - Empty queues
   - Concurrent operations
   - Partial failures

## Dependencies

**Must Be Complete First:**
- ✅ D1-D10: Driver milestones complete
- ✅ E7: Engine parity validation complete

**Infrastructure Required:**
- MongoDB 7 (queue/state storage)
- Redis 7 (queues, caching)
- Node.js 10.13.0+ (legacy driver)
- .NET 9+ (ScreepsDotNet.Driver)

## Implementation Strategy

Similar to Engine parity harness:
1. Define fixtures (JSON: room queue state, intents, expected mutations)
2. Build Node.js test runner (execute legacy driver)
3. Build .NET test runner (execute ScreepsDotNet.Driver)
4. Compare outputs (queue states, bulk mutations, stats)
5. Report divergences

## Related Documentation

- **Driver Roadmap:** `docs/driver/roadmap.md`
- **Engine Parity:** `../engine/README.md` (E7)
- **Backend Parity:** `../backend/README.md`

---

**Created:** 2026-01-22
**Part of:** Multi-Layer Parity Testing Strategy
