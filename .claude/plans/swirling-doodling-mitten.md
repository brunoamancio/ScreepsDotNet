# Power Creep Abilities Implementation Plan

**Status:** Ready to implement
**Estimated Effort:** 8-11 days
**Created:** January 21, 2026

## Overview

Implement the final E2.3 handler family: Power Creep Abilities. This completes the E2.3 "Wire engine consumption" milestone by porting all 19 power abilities from Node.js to .NET. Once complete, E2.3 will be 100% finished, and E2 (Data & Storage Model) will transition to E3 (Intent Gathering & Validation).

## Context

### Current State
✅ **Infrastructure Ready:**
- `PowerInfo.cs` - All 19 abilities defined with ranges, cooldowns, durations, ops costs, effects
- `PowerEffectSnapshot` - Schema ready (Power, Level, EndTime) in `RoomObjectSnapshot.Effects`
- `PowerCreepIntentStep` - Global processor handles lifecycle (create, delete, rename, suicide, spawn, upgrade)
- `IntentKeys.Power` - Intent key constant already defined
- `PowerAbilityCooldownStep` - Exists but only handles shield decay and spawn cooldown (NOT power effects)

⚠️ **Missing Components:**
- `PowerAbilityStep` - Room processor step to handle `usePower` intents
- Power effect decay logic - Remove expired effects from structures each tick
- Power effect consumption - Existing handlers (lab, power spawn, factory) need to check and consume effects

### Node.js Implementation
- Single file: `engine/src/processor/intents/power-creeps/usePower.js` (~310 lines)
- Big switch statement handling all 19 abilities
- Validation: power enabled room, cooldown, ops cost, range, target type
- Two patterns:
  1. **Effect-based** (17 abilities): Apply PowerEffectSnapshot to target structure
  2. **Direct action** (2 abilities): `generateOps` (add ops to creep), `shield` (create rampart), `operateExtension` (fill extensions)

### E5 Dependencies
The following abilities require E5 (Global Systems) implementation:
1. **generateOps** - Requires `IGlobalMutationWriter.IncrementUserPower(userId, amount)` to track global power balance
2. **operateController** (possibly) - May require `IGlobalMutationWriter.IncrementUserGcl(userId, amount)` if effect increases GCL gain (need to verify Node.js behavior)

**Decision:** Implement all abilities now, defer generateOps to E5. We already have 4 other features blocked by E5, one more makes no difference.

## Goals

1. ✅ Implement `PowerAbilityStep` with full validation and effect application
2. ✅ Implement power effect decay logic
3. ✅ Update existing handlers to consume power effects (lab, power spawn, factory)
4. ✅ Comprehensive test coverage (~50-60 tests)
5. ✅ Update E2.3 plan to reflect 100% completion
6. ✅ Document E5 deferrals clearly

## Non-Goals

❌ **Event log emission** - Cross-cutting concern, deferred (non-parity-critical)
❌ **Global power balance** - Blocked by E5 (generateOps only)
❌ **Stats recording** - Can be added later (non-parity-critical)

## Implementation Plan

### Phase 1: Power Effect Decay (2-3 hours)

**Goal:** Remove expired power effects from structures each tick.

**Tasks:**
1. Create `PowerEffectDecayStep` (room processor step)
   - Iterate all `RoomObjectSnapshot.Effects` dictionaries
   - Remove effects where `effect.EndTime <= gameTime`
   - Emit patches for modified objects
   - Register in `ServiceCollectionExtensions` BEFORE `PowerAbilityStep` (decay happens before new effects)

2. Write tests (3-4 tests):
   - Effect expires → removed from structure
   - Effect not expired → remains on structure
   - Multiple effects → only expired ones removed
   - No effects → no patch emitted

**Files:**
- `src/ScreepsDotNet.Engine/Processors/Steps/PowerEffectDecayStep.cs` (NEW)
- `src/ScreepsDotNet.Engine.Tests/Processors/Steps/PowerEffectDecayStepTests.cs` (NEW)
- `src/ScreepsDotNet.Engine/ServiceCollectionExtensions.cs` (MODIFY - add before PowerAbilityCooldownStep)

**Success Criteria:**
- [ ] 4/4 tests passing
- [ ] Effects decay correctly each tick
- [ ] No patches emitted when no effects expire

---

### Phase 2: PowerAbilityStep Core Infrastructure (1 day)

**Goal:** Implement validation logic and effect application framework.

**Tasks:**
1. Create `PowerAbilityStep` (room processor step)
   - Process `IntentKeys.Power` intents from `CreepIntentEnvelope`
   - Extract power type from intent (IntentFieldValueKind.Number)
   - Extract target ID from intent (IntentFieldValueKind.Text)
   - Validate:
     - Room has power enabled (`roomController.isPowerEnabled`)
     - Room not in enemy safe mode (`roomController.safeMode <= gameTime OR roomController.user == powerCreep.user`)
     - Power exists in `PowerInfo.Abilities`
     - Power creep has ability and level > 0 (`creep.Powers[power].Level > 0`)
     - Power not on cooldown (`creep.Powers[power].CooldownTime <= gameTime`)
     - Creep has sufficient ops
     - Target exists (if range-based ability)
     - Target in range (if range-based ability)
     - Target doesn't have higher-level effect of same power
   - Calculate ops cost (single value or level-based array)
   - Deduct ops from creep store
   - Set power cooldown on creep
   - Record action log (power id, target x/y)

2. Add helper methods:
   - `TryGetPowerType(IntentRecord)` - Extract power from intent
   - `TryGetTargetId(IntentRecord)` - Extract target ID
   - `CalculateOpsCost(PowerAbilityInfo, level)` - Get ops for level
   - `CalculateDuration(PowerAbilityInfo, level)` - Get duration for level
   - `IsTargetInRange(powerCreep, target, range)` - Distance check
   - `HasHigherLevelEffect(target, power, level, gameTime)` - Check existing effect

3. Add action log support:
   - Create `RoomObjectActionLogUsePower(int Power, int X, int Y)` in `Driver/Contracts/RoomMutationBatch.cs`
   - Add to `RoomObjectActionLogPatch` record

4. Write foundational tests (8-10 tests):
   - Valid power use → ops deducted, cooldown set, effect applied
   - Power not enabled → no effect
   - Enemy safe mode → no effect
   - Power on cooldown → no effect
   - Insufficient ops → no effect
   - Target out of range → no effect
   - Target has higher-level effect → no effect (Node.js line 48-50)
   - Unknown power → no effect

**Files:**
- `src/ScreepsDotNet.Engine/Processors/Steps/PowerAbilityStep.cs` (NEW)
- `src/ScreepsDotNet.Engine.Tests/Processors/Steps/PowerAbilityStepTests.cs` (NEW)
- `src/ScreepsDotNet.Driver/Contracts/RoomMutationBatch.cs` (MODIFY - add UsePower action log)
- `src/ScreepsDotNet.Engine/ServiceCollectionExtensions.cs` (MODIFY - register after PowerEffectDecayStep)

**Success Criteria:**
- [ ] 10/10 foundational tests passing
- [ ] All validation logic working
- [ ] Ops deduction and cooldown setting working
- [ ] Action log recording working

---

### Phase 3: Effect-Based Abilities (3-4 days)

**Goal:** Implement 16 abilities that apply PowerEffectSnapshot to targets.

**Abilities (Grouped by Similarity):**

**Group 1: Structure Operate Effects (8 abilities)** - Simple type checks + effect application
- `operateSpawn` (PWR_OPERATE_SPAWN) → target.type == 'spawn'
- `operateTower` (PWR_OPERATE_TOWER) → target.type == 'tower'
- `operateStorage` (PWR_OPERATE_STORAGE) → target.type == 'storage'
- `operateLab` (PWR_OPERATE_LAB) → target.type == 'lab'
- `operateObserver` (PWR_OPERATE_OBSERVER) → target.type == 'observer'
- `operateTerminal` (PWR_OPERATE_TERMINAL) → target.type == 'terminal'
- `operatePower` (PWR_OPERATE_POWER) → target.type == 'powerSpawn'
- `operateController` (PWR_OPERATE_CONTROLLER) → target.type == 'controller'

**Group 2: Disruption Effects (4 abilities)** - Simple type checks + effect application
- `disruptSpawn` (PWR_DISRUPT_SPAWN) → target.type == 'spawn'
- `disruptTower` (PWR_DISRUPT_TOWER) → target.type == 'tower'
- `disruptSource` (PWR_DISRUPT_SOURCE) → target.type == 'source'
- `disruptTerminal` (PWR_DISRUPT_TERMINAL) → target.type == 'terminal'

**Group 3: Regeneration Effects (2 abilities)** - Special validation + effect application
- `regenSource` (PWR_REGEN_SOURCE) → target.type == 'source'
- `regenMineral` (PWR_REGEN_MINERAL) → target.type == 'mineral', mineralAmount > 0, no nextRegenerationTime

**Group 4: Fortify (1 ability)** - Special validation + effect application
- `fortify` (PWR_FORTIFY) → target.type == 'rampart' || target.type == 'constructedWall'

**Group 5: Factory (1 ability)** - Special logic (set level, then apply effect)
- `operateFactory` (PWR_OPERATE_FACTORY) → target.type == 'factory', set target.level if not set

**Implementation Pattern:**
```csharp
private static void ProcessUsePower(
    RoomProcessorContext context,
    RoomObjectSnapshot powerCreep,
    IntentRecord record,
    Dictionary<string, Dictionary<PowerTypes, PowerEffectSnapshot>> effectsLedger,
    Dictionary<string, Dictionary<string, int>> storeLedger,
    Dictionary<string, RoomObjectPowerCooldownPatch> powerCooldownLedger,
    Dictionary<string, RoomObjectActionLogPatch> actionLogLedger,
    HashSet<string> modifiedObjects)
{
    // Validation...

    switch (powerType)
    {
        case PowerTypes.OperateSpawn:
            if (!string.Equals(target.Type, RoomObjectTypes.Spawn, StringComparison.Ordinal))
                return;
            ApplyEffect = true;
            break;

        case PowerTypes.OperateFactory:
            if (!string.Equals(target.Type, RoomObjectTypes.Factory, StringComparison.Ordinal))
                return;
            // Special logic: set factory level if not set (Node.js line 264-266)
            if (target.Level is null || target.Level == 0) {
                context.MutationWriter.Patch(target.Id, new RoomObjectPatchPayload { Level = creepPower.Level });
            } else if (target.Level != creepPower.Level) {
                return;  // Level mismatch
            }
            ApplyEffect = true;
            break;

        // ... more cases
    }

    if (applyEffect) {
        var duration = CalculateDuration(powerInfo, creepPower.Level);
        var endTime = gameTime + duration;

        // Remove old effect of same power (Node.js line 279)
        if (!effectsLedger.TryGetValue(target.Id, out var effects)) {
            effects = new Dictionary<PowerTypes, PowerEffectSnapshot>(target.Effects);
        }

        effects[powerType] = new PowerEffectSnapshot(
            Power: powerType,
            Level: creepPower.Level,
            EndTime: endTime
        );

        effectsLedger[target.Id] = effects;
        modifiedObjects.Add(target.Id);
    }
}
```

**Tasks:**
1. Implement switch statement with all 16 abilities
2. Write 2 tests per ability (~32 tests):
   - Valid use → effect applied
   - Invalid target type → no effect
   - (Special cases for regenMineral, operateFactory)

**Files:**
- `src/ScreepsDotNet.Engine/Processors/Steps/PowerAbilityStep.cs` (MODIFY - add switch cases)
- `src/ScreepsDotNet.Engine.Tests/Processors/Steps/PowerAbilityStepTests.cs` (MODIFY - add tests)

**Success Criteria:**
- [ ] 32/32 effect-based ability tests passing
- [ ] All 16 abilities apply effects correctly
- [ ] Special cases (regenMineral, operateFactory) handled

---

### Phase 4: Direct Action Abilities (2-3 days)

**Goal:** Implement 2 abilities with direct mutations (no effects).

**Abilities:**

**4.1: operateExtension (PWR_OPERATE_EXTENSION)** - Complex logic
- Validates target is storage/terminal/factory/container
- Validates target owner == room controller owner
- Checks for disruptTerminal effect on target
- Finds all extensions owned by room controller
- Calculates energy to send: `min(target.store.energy, powerInfo.effect[level-1] * sum(extension.storeCapacityResource.energy))`
- Fills extensions in order of distance from target
- Deducts energy from target
- Node.js lines 104-132

**4.2: shield (PWR_SHIELD)** - Special rampart creation
- Validates no construction site at power creep position
- Removes construction site if exists
- Validates rampart can be built at position (terrain check)
- Creates rampart at power creep position with:
  - `hits = powerInfo.effect[level-1]` (5000/10000/15000/20000/25000)
  - `hitsMax = 0` (special flag for temporary shield)
  - `nextDecayTime = gameTime + powerInfo.duration` (50 ticks)
  - `effects = [{ power: PWR_SHIELD, level, endTime }]`
- Node.js lines 228-256

**Tasks:**
1. Implement `operateExtension`:
   - Find extensions in room (filter by type and owner)
   - Calculate max energy to send
   - Fill extensions (sort by distance from target)
   - Deduct energy from target

2. Implement `shield`:
   - Check for construction site at position, remove if exists
   - Validate rampart can be built (use existing blueprint validation)
   - Create new rampart with special properties
   - Add shield effect to new rampart

3. Write tests (10-12 tests):
   - `operateExtension` (6 tests):
     - Valid use → extensions filled, energy deducted from target
     - Target not storage/terminal/factory/container → no effect
     - Target has disruptTerminal effect → no effect
     - Target owner != controller owner → no effect
     - No energy in target → no effect
     - All extensions full → no effect
   - `shield` (4-6 tests):
     - Valid use → rampart created with correct hits
     - Construction site exists → removed, then rampart created
     - Invalid terrain → no rampart
     - Different levels → different hits values
     - Shield rampart has effect in Effects dictionary
     - Shield rampart has hitsMax = 0 (special flag)

**Files:**
- `src/ScreepsDotNet.Engine/Processors/Steps/PowerAbilityStep.cs` (MODIFY - add cases)
- `src/ScreepsDotNet.Engine.Tests/Processors/Steps/PowerAbilityStepTests.cs` (MODIFY - add tests)

**Success Criteria:**
- [ ] 12/12 direct action tests passing
- [ ] operateExtension fills extensions correctly
- [ ] shield creates temporary rampart correctly

---

### Phase 5: Deferred Ability (generateOps) (5 minutes)

**Goal:** Document E5 deferral for generateOps.

**Implementation:**
```csharp
case PowerTypes.GenerateOps:
    // TODO (E5): Requires IGlobalMutationWriter.IncrementUserPower(userId, amount)
    // Node.js lines 57-69:
    // - Adds ops to power creep store
    // - Drops overflow if exceeds capacity
    // Blocked by E5 (Global Systems) - requires global user power balance tracking
    return;
```

**Tasks:**
1. Add TODO comment in switch statement
2. Document in E2.3 plan under "Deferred Features"
3. Add to E5 plan under "Blocked E2.3 Features"

**Files:**
- `src/ScreepsDotNet.Engine/Processors/Steps/PowerAbilityStep.cs` (MODIFY - add TODO)
- `docs/engine/e2.3-plan.md` (MODIFY - add deferral)
- `docs/engine/e5-plan.md` (MODIFY - add blocker)

**Success Criteria:**
- [ ] TODO comment added
- [ ] E2.3 plan updated
- [ ] E5 plan updated

---

### Phase 6: Power Effect Consumption (1 day)

**Goal:** Update existing handlers to consume power effects.

**Handlers to Update:**

**6.1: LabIntentStep (PWR_OPERATE_LAB)**
- In `ProcessRunReaction`, check for `PWR_OPERATE_LAB` effect
- Increase reaction amount from default 5 to `5 + effect.Level[level-1]` (2/4/6/8/10 = 7/9/11/13/15 total)
- Node.js behavior: `powerInfo.effect[level-1]` is added to base amount
- E2.3 plan line 121-124

**6.2: PowerSpawnIntentStep (PWR_OPERATE_POWER)**
- In `ProcessPowerSpawn`, check for `PWR_OPERATE_POWER` effect
- Increase processing amount from 1 to `1 + effect.Level[level-1]` (1/2/3/4/5 = 2/3/4/5/6 total)
- Node.js behavior: same pattern as operateLab
- E2.3 plan line 169-173

**6.3: FactoryIntentStep (PWR_OPERATE_FACTORY)**
- In `ProcessProduce`, check for `PWR_OPERATE_FACTORY` effect
- Use `Math.Max(factory.Level, effect.Level)` for level gating check
- Node.js behavior: factory level temporarily boosted to power creep's ability level
- E2.3 plan line 191-194

**Implementation Pattern:**
```csharp
// LabIntentStep.ProcessRunReaction
var baseAmount = 5;
var reactionAmount = baseAmount;

// Check for PWR_OPERATE_LAB effect
if (lab.Effects.TryGetValue(PowerTypes.OperateLab, out var effect))
{
    if (!PowerInfo.Abilities.TryGetValue(PowerTypes.OperateLab, out var powerInfo))
        return;

    if (effect.EndTime > gameTime && powerInfo.Effect is not null)
    {
        var effectBonus = powerInfo.Effect[effect.Level - 1];
        reactionAmount = baseAmount + effectBonus;
    }
}

// Use reactionAmount instead of hardcoded 5
```

**Tasks:**
1. Update LabIntentStep
2. Update PowerSpawnIntentStep
3. Update FactoryIntentStep
4. Write tests (9 tests total):
   - Lab: with effect (3 tests - level 1/3/5), without effect (1 test)
   - Power Spawn: with effect (3 tests - level 1/3/5), without effect (1 test)
   - Factory: with effect (1 test), without effect (1 test)

**Files:**
- `src/ScreepsDotNet.Engine/Processors/Steps/LabIntentStep.cs` (MODIFY)
- `src/ScreepsDotNet.Engine/Processors/Steps/PowerSpawnIntentStep.cs` (MODIFY)
- `src/ScreepsDotNet.Engine/Processors/Steps/FactoryIntentStep.cs` (MODIFY)
- `src/ScreepsDotNet.Engine.Tests/Processors/Steps/LabIntentStepTests.cs` (MODIFY)
- `src/ScreepsDotNet.Engine.Tests/Processors/Steps/PowerSpawnIntentStepTests.cs` (MODIFY)
- `src/ScreepsDotNet.Engine.Tests/Processors/Steps/FactoryIntentStepTests.cs` (MODIFY)

**Success Criteria:**
- [ ] 9/9 power effect consumption tests passing
- [ ] Lab reaction amount increases with operateLab effect
- [ ] Power spawn processing amount increases with operatePower effect
- [ ] Factory level gating uses operateFactory effect

---

### Phase 7: Integration & Documentation (0.5 days)

**Goal:** Wire everything together and update documentation.

**Tasks:**
1. Register `PowerEffectDecayStep` and `PowerAbilityStep` in `ServiceCollectionExtensions`
   - Order: PowerEffectDecayStep → PowerAbilityStep → PowerAbilityCooldownStep
   - Rationale: Decay old effects → Apply new effects → Decay shield hits

2. Run all Engine tests (173 + ~60 new = 233 total)
   - Verify no regressions in existing handlers
   - Verify all new tests passing

3. Update E2.3 plan (`docs/engine/e2.3-plan.md`):
   - Move "Power Creep Abilities" from "Remaining Handlers" to "Completed Handlers"
   - Add test counts (Power Abilities: 60/60 tests)
   - Update progress from 95% to 100%
   - Add deferral for generateOps under "Deferred Features"
   - Update "Next Steps" section

4. Update E5 plan (`docs/engine/e5-plan.md`):
   - Add "Blocked E2.3 Features" #5: generateOps power ability
   - Note that it requires `IGlobalMutationWriter.IncrementUserPower(userId, amount)`

**Files:**
- `src/ScreepsDotNet.Engine/ServiceCollectionExtensions.cs` (MODIFY)
- `docs/engine/e2.3-plan.md` (MODIFY)
- `docs/engine/e5-plan.md` (MODIFY)

**Success Criteria:**
- [ ] All 233 Engine tests passing (173 existing + 60 new)
- [ ] ServiceCollectionExtensions wired correctly
- [ ] E2.3 plan shows 100% completion
- [ ] E5 plan documents generateOps deferral

---

## Test Coverage Summary

| Component | Tests | Description |
|-----------|-------|-------------|
| PowerEffectDecayStep | 4 | Effect expiration logic |
| PowerAbilityStep (Core) | 10 | Validation, ops deduction, cooldown |
| Effect-Based Abilities | 32 | 16 abilities × 2 tests each |
| Direct Action Abilities | 12 | operateExtension (6), shield (6) |
| Power Effect Consumption | 9 | Lab (4), PowerSpawn (4), Factory (1) |
| **Total** | **67** | **Comprehensive coverage** |

## File Change Summary

### New Files (4)
- `src/ScreepsDotNet.Engine/Processors/Steps/PowerEffectDecayStep.cs`
- `src/ScreepsDotNet.Engine/Processors/Steps/PowerAbilityStep.cs`
- `src/ScreepsDotNet.Engine.Tests/Processors/Steps/PowerEffectDecayStepTests.cs`
- `src/ScreepsDotNet.Engine.Tests/Processors/Steps/PowerAbilityStepTests.cs`

### Modified Files (8)
- `src/ScreepsDotNet.Engine/Processors/Steps/LabIntentStep.cs` (add PWR_OPERATE_LAB consumption)
- `src/ScreepsDotNet.Engine/Processors/Steps/PowerSpawnIntentStep.cs` (add PWR_OPERATE_POWER consumption)
- `src/ScreepsDotNet.Engine/Processors/Steps/FactoryIntentStep.cs` (add PWR_OPERATE_FACTORY consumption)
- `src/ScreepsDotNet.Engine.Tests/Processors/Steps/LabIntentStepTests.cs` (add effect tests)
- `src/ScreepsDotNet.Engine.Tests/Processors/Steps/PowerSpawnIntentStepTests.cs` (add effect tests)
- `src/ScreepsDotNet.Engine.Tests/Processors/Steps/FactoryIntentStepTests.cs` (add effect tests)
- `src/ScreepsDotNet.Driver/Contracts/RoomMutationBatch.cs` (add UsePower action log)
- `src/ScreepsDotNet.Engine/ServiceCollectionExtensions.cs` (register new steps)

### Documentation Updates (2)
- `docs/engine/e2.3-plan.md` (mark complete, add deferrals)
- `docs/engine/e5-plan.md` (add generateOps blocker)

## Success Criteria

✅ **Phase 1:** Power effect decay working (4/4 tests)
✅ **Phase 2:** Core validation and effect application (10/10 tests)
✅ **Phase 3:** All 16 effect-based abilities working (32/32 tests)
✅ **Phase 4:** operateExtension and shield working (12/12 tests)
✅ **Phase 5:** generateOps documented as E5 deferral
✅ **Phase 6:** Power effects consumed by existing handlers (9/9 tests)
✅ **Phase 7:** Integration complete, all 233 tests passing, documentation updated

**E2.3 Completion:** 100% (11/11 major handler families implemented)

## Deferred Features (Tracked in E2.3 Plan)

### Blocked by E5 (Global Systems)
1. **generateOps power ability** - Requires `IGlobalMutationWriter.IncrementUserPower(userId, amount)`
   - **Impact:** Power balance tracking blocked for this ability only
   - **Effort:** 1-2 hours once E5 Phase 1 complete
   - **Parity:** ✅ YES (required for E7 validation)

2. **operateController power effect** (TBD)
   - **Need to verify:** Does Node.js operateController effect increase GCL gain?
   - **If yes:** Requires `IGlobalMutationWriter.IncrementUserGcl(userId, amount)`
   - **If no:** No deferral needed, implement in Phase 3

### Non-Parity-Critical
3. **Event log emission** - usePower actions don't emit events to event log
   - **Impact:** Replay/visualization only
   - **Effort:** Part of holistic event log implementation
   - **Parity:** ❌ NO (can be implemented post-MVP)

4. **Stats recording** - Power ability usage not tracked in stats
   - **Impact:** Statistics only
   - **Effort:** 2-3 hours
   - **Parity:** ❌ NO (can be implemented post-MVP)

## Risk Mitigation

### Risk: Effect decay race condition
**Mitigation:** PowerEffectDecayStep runs BEFORE PowerAbilityStep in processor pipeline. New effects applied in tick N won't decay until tick N+1.

### Risk: Effect ledger memory usage
**Mitigation:** Effects stored as `Dictionary<PowerTypes, PowerEffectSnapshot>` per object (max 19 effects per structure). Dictionary overhead minimal (<1KB per structure).

### Risk: operateExtension complexity
**Mitigation:** Break into helper methods (FindExtensions, CalculateMaxEnergy, FillExtensions). Test each method independently.

### Risk: shield rampart collision with existing structures
**Mitigation:** Node.js removes construction site if exists (line 229-233). Use blueprint validation to check terrain.

### Risk: Test maintenance burden
**Mitigation:** Use parameterized tests for similar abilities (e.g., xUnit Theory for all 8 "operate" structure effects).

## References

### Node.js Implementation
- **Primary file:** `/home/th3b0y/screeps-rewrite/ScreepsNodeJs/engine/src/processor/intents/power-creeps/usePower.js`
- **Constants:** `/home/th3b0y/screeps-rewrite/ScreepsNodeJs/common/lib/constants.js` (POWER_INFO line 853+)
- **Intent registration:** `/home/th3b0y/screeps-rewrite/ScreepsNodeJs/engine/src/processor/intents/power-creeps/intents.js`

### .NET Implementation
- **PowerInfo:** `src/ScreepsDotNet.Common/Constants/PowerInfo.cs` (all 19 abilities defined)
- **PowerTypes:** `src/ScreepsDotNet.Common/Types/PowerTypes.cs` (enum)
- **PowerEffectSnapshot:** `src/ScreepsDotNet.Driver/Contracts/PowerEffectSnapshot.cs` (schema)
- **PowerCreepIntentStep:** `src/ScreepsDotNet.Engine/Processors/GlobalSteps/PowerCreepIntentStep.cs` (lifecycle)
- **PowerAbilityCooldownStep:** `src/ScreepsDotNet.Engine/Processors/Steps/PowerAbilityCooldownStep.cs` (shield decay, spawn cooldown)

### Documentation
- **E2.3 Plan:** `docs/engine/e2.3-plan.md` (handler backlog)
- **E5 Plan:** `docs/engine/e5-plan.md` (global systems blockers)
- **Engine CLAUDE.md:** `src/ScreepsDotNet.Engine/CLAUDE.md` (Engine patterns)
- **Driver CLAUDE.md:** `src/ScreepsDotNet.Driver/CLAUDE.md` (Driver abstractions)

## Notes

- **TDD Approach:** Write tests FIRST for each phase, then implement to make tests pass
- **Ledger Pattern:** Use dictionaries to accumulate mutations before emitting patches (same as Lab/Link/Factory handlers)
- **Effect Replacement:** Node.js removes old effect of same power type before applying new one (line 279: `_.remove(effects, {power: intent.power})`)
- **Higher-Level Effect Protection:** Node.js prevents lower-level power from overwriting higher-level effect (line 48-50)
- **Cooldown Timing:** Cooldown set AFTER successful power use, not before (line 291-297)
- **Action Log:** Record power ID and target position for debugging/replay (line 305-309)

**Last Updated:** January 21, 2026
