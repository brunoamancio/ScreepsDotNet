# Common Parity Harness

**Status:** 📋 Not Yet Implemented

## Purpose

Test behavioral parity between .NET Common library and legacy Node.js common library for:
- Game constants (resource types, structure types, intent types)
- Formula calculations (energy costs, build times, damage formulas)
- Utility functions (distance, position validation, range checks)
- Data structures (CostMatrix, PathfinderGoal)

## Scope

**In Scope:**
- Constant values (RESOURCE_ENERGY, STRUCTURE_SPAWN, etc.)
- Formula accuracy (bodyCost, buildTime, upgradeControllerPower, etc.)
- Utility function behavior (getDirection, getRoomName, etc.)
- Data structure methods (CostMatrix serialization, deserialization)

**Out of Scope:**
- Game simulation logic (covered by Engine parity)
- Database operations (covered by Driver parity)
- HTTP/CLI behavior (covered by Backend parity)

## Estimated Effort

- **Time:** 4-6 hours
- **Priority:** 🟢 LOW (can run in parallel with other layers)
- **Fixtures:** 20-30 test cases

## Fixture Categories

1. **Constants (5 fixtures)**
   - Resource types (all 29 types)
   - Structure types (all 23 types)
   - Intent types (all 40+ intents)
   - Body part types (all 7 types)
   - Game configuration (CREEP_LIFE_TIME, etc.)

2. **Formulas (10 fixtures)**
   - Body cost calculations
   - Build time calculations
   - Damage formulas (attack, ranged, heal)
   - Energy consumption (harvest, build, upgrade)
   - Cooldown calculations (spawn, lab, tower)

3. **Utility Functions (5 fixtures)**
   - Position utilities (getDirection, getRange)
   - Room name parsing (parseRoomName, getRoomNameFromXY)
   - Path finding helpers (CostMatrix operations)
   - Range checks (inRange, isInRoom)

4. **Data Structures (5 fixtures)**
   - CostMatrix (serialize, deserialize, get, set)
   - PathfinderGoal (equality, range validation)
   - Store (resource operations)

## Dependencies

**Must Be Complete First:**
- ✅ ScreepsDotNet.Common implementation complete

**Infrastructure Required:**
- Node.js 10.13.0+ (legacy common library)
- .NET 9+ (ScreepsDotNet.Common)
- No MongoDB/Redis needed (pure computation)

## Implementation Strategy

Simplest parity layer (no database, no fixtures):

1. **Unit Test Approach:**
   - Write parameterized xunit tests
   - Each test calls both .NET and Node.js implementations
   - Compare outputs directly

2. **Test Structure:**
   ```csharp
   [Theory]
   [InlineData(RESOURCE_ENERGY, 300)]
   [InlineData(RESOURCE_POWER, 3000)]
   public void ConstantValues_MatchNodeJs(string resourceType, int expectedValue)
   {
       var dotnetValue = ScreepsConstants.GetResourceValue(resourceType);
       var nodejsValue = ExecuteNodeJs($"require('screeps-common').RESOURCE_{resourceType}");
       Assert.Equal(nodejsValue, dotnetValue);
   }
   ```

3. **Node.js Execution:**
   - Use Process.Start() to execute Node.js scripts
   - Capture stdout/stderr
   - Parse JSON results
   - Compare with .NET values

## Related Documentation

- **Common Library:** `src/ScreepsDotNet.Common/`
- **Constants:** `src/ScreepsDotNet.Common/Constants/`
- **Formulas:** (to be implemented)
- **Official Screeps Common:** https://github.com/screeps/common

---

**Created:** 2026-01-22
**Part of:** Multi-Layer Parity Testing Strategy
