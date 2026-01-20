# E5 – Global Systems

**Status:** Not Started (Placeholder for E2.3 cross-references)

This document tracks the E5 "Global Systems" work, which includes global mutations (user GCL, credits, resources), power effect tracking, market operations, NPC spawns, and shard messaging.

---

## Purpose

E5 implements global state mutations that affect users, shards, and cross-room systems. This is distinct from E2.3's room-level intent handlers.

**Key differences from E2.3:**
- **E2.3:** Room-level mutations (objects, room info, action logs)
- **E5:** Global-level mutations (user stats, global resources, cross-room effects)

---

## Blocked E2.3 Features

The following E2.3 features are **blocked** by E5 implementation and should be implemented **after** E5 global mutation infrastructure is in place:

### 1. User GCL Updates (Controller Intents)
**Blocking E2.3 Feature:** `ControllerIntentStep.ProcessUpgrade()` GCL accumulation
**E2.3 Reference:** `docs/engine/e2.3-plan.md` → "Controller Intents (Deferred - PARITY-BLOCKING)"
**Parity Status:** ✅ **PARITY-CRITICAL** (required for E7 validation)

**What's blocked:**
- Node.js calls `bulkUsers.inc(target.user, 'gcl', boostedEffect)` on **EVERY** upgrade
- Both boosted AND non-boosted GCL gains are blocked
- Affects controller progress → user GCL progression

**What's needed from E5:**
1. Implement `IGlobalMutationWriter` interface with `IncrementUserGcl(userId, amount)` method
2. Add global mutation batching infrastructure (similar to room-level `IRoomMutationWriter`)
3. Wire global mutations into processor context
4. Flush global mutations to user documents at end of tick

**Implementation effort after E5:** 1-2 hours (just call `context.GlobalMutationWriter.IncrementUserGcl()`)

---

### 2. Boost Effects (GCL Component)
**Blocking E2.3 Feature:** `ControllerIntentStep.ProcessUpgrade()` boosted GCL gains
**E2.3 Reference:** `docs/engine/e2.3-plan.md` → "Controller Intents (Deferred - PARITY-BLOCKING)"
**Parity Status:** ⚠️ **PARTIALLY COMPLETE** (controller progress done, GCL blocked)

**What's blocked:**
- Boost multipliers (GH: 1.5x, GH2O: 1.8x, XGH2O: 2.0x) apply to controller progress ✅ **COMPLETE**
- Boost multipliers apply to GCL gains ❌ **BLOCKED** (same issue as #1)

**What's needed from E5:**
- Same infrastructure as #1 (IGlobalMutationWriter)

**Implementation effort after E5:** Included in #1 (same code path)

---

### 3. PWR_OPERATE_LAB Power Effect
**Blocking E2.3 Feature:** `LabIntentStep.ProcessRunReaction()` power effect multipliers
**E2.3 Reference:** `docs/engine/e2.3-plan.md` → "Lab Reactions & Boosts (Deferred)"
**Parity Status:** ❌ **NON-PARITY-CRITICAL** (nice-to-have, not required for E7)

**What's blocked:**
- Power creeps can use `PWR_OPERATE_LAB` to boost reaction amount from 5 to higher values
- Requires power effect tracking system (effect duration, magnitude, decay)

**What's needed from E5:**
1. Implement power effect decay logic (decrement `endTime` each tick)
2. Add effect lookup helpers to `RoomProcessorContext`
3. Document power effect lifecycle (apply → track → decay → remove)

**Implementation effort after E5:** 1-2 hours
- Check for `PWR_OPERATE_LAB` effect in `ProcessRunReaction`
- Increase reaction amount by effect magnitude (default 5 → 5 + effect)
- 2-3 tests (with effect vs without)

---

## E5 Deliverables (When Implemented)

### 1. Global Mutation Infrastructure
**Analogous to:** `IRoomMutationWriter` (room-level mutations)

**Interface:**
```csharp
public interface IGlobalMutationWriter
{
    // User stats
    void IncrementUserGcl(string userId, int amount);
    void IncrementUserCredits(string userId, int amount);
    void DecrementUserCredits(string userId, int amount);

    // User resources (for intershard operations)
    void IncrementUserResource(string userId, string resourceType, int amount);
    void DecrementUserResource(string userId, string resourceType, int amount);

    // Flush mutations to storage
    Task FlushAsync(CancellationToken token = default);
}
```

**Wiring:**
- Add `IGlobalMutationWriter` to `RoomProcessorContext` constructor
- Inject into `EngineHost` / `MainLoopGlobalProcessor`
- Flush after all room processors complete

---

### 2. Power Effect Tracking
**Analogous to:** Action logs (debugging feature)

**What's needed:**
1. Power effect decay processor (runs each tick, decrements `endTime`, removes expired effects)
2. Effect lookup helpers in `RoomProcessorContext`:
   ```csharp
   bool TryGetEffect(RoomObjectSnapshot obj, PowerTypes power, out PowerEffectSnapshot effect);
   ```
3. Document effect lifecycle in `docs/engine/power-effects.md`

**Schema:** Already exists in `RoomObjectSnapshot.Effects` (no schema changes needed)

---

### 3. Market Operations (Lower Priority)
**Note:** Market infrastructure already exists (`MarketIntentStep`), but global market order matching and NPC order generation are deferred.

---

### 4. NPC Spawns (Lower Priority)
**Note:** NPC spawn logic (invaders, source keepers) is room-level but uses global timers.

---

## Implementation Priority (When Starting E5)

### Phase 1: Global Mutations (Unblocks E2.3)
**Effort:** 2-3 days
**Unblocks:** User GCL updates, boost effects (GCL component)

1. Implement `IGlobalMutationWriter` interface
2. Add global mutation batching (similar to `BulkRoomMutationWriter`)
3. Wire into `RoomProcessorContext`
4. Add tests for GCL accumulation
5. **Go back to E2.3:** Implement controller GCL updates

---

### Phase 2: Power Effect Tracking (Unblocks E2.3 Lab Effects)
**Effort:** 1-2 days
**Unblocks:** PWR_OPERATE_LAB (lab reaction boosts)

1. Implement power effect decay processor
2. Add effect lookup helpers
3. Document power effect lifecycle
4. **Go back to E2.3:** Implement PWR_OPERATE_LAB in lab reactions

---

### Phase 3: Market & NPC (Post-E2.3)
**Effort:** 3-5 days
**Unblocks:** Nothing in E2.3 (market/NPC are independent features)

---

## Cross-References

**Master Roadmap:** `src/ScreepsDotNet.Engine/CLAUDE.md` → "Roadmap (E1-E8)"
- E5 roadmap entry with status, exit criteria, dependencies

**E2.3 Plan:** `docs/engine/e2.3-plan.md`
- See "Controller Intents (Deferred - PARITY-BLOCKING)" for GCL details
- See "Lab Reactions & Boosts (Deferred)" for power effect details
- See "Deferred Features - Complexity Ranking" for effort estimates

**Driver CLAUDE.md:** `src/ScreepsDotNet.Driver/CLAUDE.md`
- Global mutations may require driver-level bulk writers (similar to D5)

---

## Success Criteria (When E5 is Complete)

1. ✅ `IGlobalMutationWriter` implemented and wired into processor context
2. ✅ User GCL updates work (controller upgrades accumulate GCL)
3. ✅ Boost effects apply to GCL gains (boosted upgrades give more GCL)
4. ✅ Power effect decay logic implemented
5. ✅ PWR_OPERATE_LAB boosts lab reaction amounts
6. ✅ All E2.3 blocked features unblocked and implemented
7. ✅ Tests pass for global mutations and power effects

---

**Last Updated:** January 20, 2026 (Created as placeholder for E2.3 cross-references)
