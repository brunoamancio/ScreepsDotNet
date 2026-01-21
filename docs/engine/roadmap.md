# Engine Roadmap (E1-E8)

**Last Updated:** January 21, 2026

This document tracks the Engine subsystem roadmap and implementation status. For detailed handler tracking, see `e2.md`. For E5 blockers, see `e5.md`.

---

## Milestone Overview

| ID | Status | Title | Exit Criteria | Dependencies |
|----|--------|-------|---------------|--------------|
| E1 | ✅ | Map Legacy Engine Surface | Node engine API inventory documented (`e1.md`) | Node engine repo, driver notes |
| E2 | ✅ | Data & Storage Model | Driver snapshot/mutation contracts in place, Engine consuming them. Handlers for all intent types. | Driver contracts, Screeps schemas |
| E3 | ✅ | Intent Gathering & Validation | `IIntentPipeline` + validators with unit tests mirroring Node fixtures | Driver runtime outputs, constants |
| E4 | ✅ | Simulation Kernel (Room Processor) | Passive regeneration systems (source, mineral) implemented. Construction site decay verified as non-existent. | E2, E3 |
| E5 | ⚠️ Phase 1 ✅ | Global Systems | Phase 1: User GCL/power tracking complete. Global mutations (`IGlobalMutationWriter`) operational. Phase 2-4: Market, NPC spawns, keeper rooms, nuker operations. | E4 foundation |
| E6 | 📋 | Engine Loop Orchestration | `EngineHost` coordinates ticks; main/runner/processor loops call managed engine | Driver queue service, telemetry sink |
| E7 | 📋 | Compatibility & Parity Validation | Lockstep testing vs. Node engine, automated divergence detection | Prior steps, legacy engine repo |
| E8 | 📋 | Observability & Tooling | Engine metrics flow to telemetry, diagnostics commands, operator playbooks | D8 logging stack, scheduler hooks |

---

## E1: Map Legacy Engine Surface ✅

**Status:** Complete

**Deliverables:**
- ✅ Node engine API inventory documented in `e1.md`
- ✅ All game mechanics catalogued (creep actions, structure logic, combat, market, etc.)

---

## E2: Data & Storage Model ✅ Complete (2026-01-21)

**Status:** Complete (all features implemented)

**Key Deliverables:**
- ✅ Driver contracts and snapshot providers
- ✅ Engine consumes only driver abstractions (no direct DB access)
- ✅ 11/11 handler families implemented (240/240 tests)
- ✅ Room mutation writers and memory persistence
- ✅ 4 features unblocked by E5 Phase 1 global mutations:
  - ✅ PWR_GENERATE_OPS power ability (with overflow drop creation)
  - ✅ User power balance tracking (PowerSpawn)
  - ✅ User GCL updates (Controller)
  - ✅ Boost effects GCL component (Controller)

**Exit Criteria:**
- All room-level intent handlers complete ✅
- Engine isolated from storage layer ✅
- E5 global mutations implemented ✅ (Phase 1 complete)
- 4 blocked features implemented ✅ (2.5 hours actual)

**Details:** See `e2.md` for handler breakdown and deferred features, `e5.md` for E5 blockers, `data-model.md` for contracts

---

## E3: Intent Gathering & Validation ✅ Complete

**Status:** E3.1 ✅ Complete | E3.2 ✅ Complete | E3.3 ✅ Complete | E3.4 ✅ Complete

**Completed Deliverables:**
- ✅ `IIntentValidator` and `IIntentPipeline` interfaces
- ✅ 5 validator implementations: Range (28 tests), Resource (18 tests), Permission (20 tests), State (15 tests), Schema (15 tests)
- ✅ Validation constants (ValidationRanges, ValidationErrorCode, ResourceRequirements, PermissionRules, StateRequirements)
- ✅ 96/96 validator tests passing
- ✅ DI registration infrastructure
- ✅ `IntentValidationPipeline` orchestrates all validators
- ✅ `IntentValidationStep` integrated into RoomProcessor (runs first)
- ✅ 354/354 tests passing (114 validation + 240 E2 regression)
- ✅ Observability infrastructure: `ValidationStatisticsSink` with 10/10 tests passing
- ✅ Statistics tracking: valid/rejected counts, error code distribution, intent type distribution

**Deferred Features:**
- 📋 Parity validation against Node.js (deferred to E7 - Compatibility & Parity Validation)
- 📋 Export statistics to telemetry (deferred to E8 - Observability & Tooling)
- 📋 Remove inline validation from E2 handlers (optional cleanup after E3 proven stable in production)

**Dependencies:**
- E2 95% complete (handler infrastructure in place) ✅
- Driver runtime outputs ✅
- Constants for validation rules ✅

**Exit Criteria:**
- ✅ All 5 validators implemented and tested (96 tests)
- ✅ All validation constants defined
- ✅ IntentValidationPipeline orchestrates validators
- ✅ All E2 tests continue passing after integration (354/354)
- ✅ Observability infrastructure implemented (10 tests)
- 📋 Parity with Node.js validation (deferred to E7)
- 📋 Validation overhead <5ms per room (measure in production)

**Details:** See `e3.md` for detailed implementation plan, `e3.1.md` and `e3.2.md` for completed work

---

## E4: Simulation Kernel (Room Processor) ✅ Complete

**Status:** Complete (January 21, 2026)

**Completed Deliverables:**
- ✅ RoomProcessor orchestration (from E2/E3 work)
- ✅ 11/11 intent handler families (240/240 tests)
- ✅ Passive regeneration systems:
  - ✅ Source energy regeneration (15 tests)
  - ✅ Mineral regeneration with density changes (12 tests)
- ✅ Legacy verification: Construction site decay **does not exist** in Node.js engine
- ✅ 20 processor steps registered in correct order
- ✅ All 707 tests passing (381 Engine + 202 Backend.Http + 70 Driver + 54 Backend.Cli)

**Key Finding:**
Construction site decay was listed in original plan but **does not exist in legacy Screeps**. Verified by examining Node.js source:
- File: `/ScreepsNodeJs/engine/src/processor/intents/construction-sites/tick.js` (empty function body)
- No decay logic anywhere in Node codebase
- Construction sites persist indefinitely until manually removed or completed

**Deferred Features (to E5):**
- ❌ NPC spawning (invaders, source keepers) - Requires global coordination and shard-wide timing
- ❌ Power bank decay - Part of keeper room mechanics (E5 global systems)
- ❌ Nuker launch - Likely an intent handler (E2 scope) or cross-room coordination (E5)

These features require global mutation infrastructure (`IGlobalMutationWriter`) and cross-room coordination that will be implemented in E5.

**Dependencies Met:**
- E2 (data model) ✅
- E3 (intent validation) ✅

**Blocks:**
- E5 (Global Systems) - room processor is stable
- E6 (Orchestration) - simulation kernel complete
- E7 (Parity) - mechanics implemented for comparison

**Details:** See `e4.md` for implementation details and verification notes

---

## E5: Global Systems ⚠️ Phase 1-3 Complete (2026-01-21)

**Status:** Phase 1 Complete ✅ | Phase 2 Complete ✅ | Phase 3 Complete ✅ | Phase 4 Not Started 📋

**Completed Deliverables (Phase 1):**
- ✅ Global mutation infrastructure (`IGlobalMutationWriter`) - 3 methods added: `IncrementUserGcl`, `IncrementUserPower`, `DecrementUserPower`
- ✅ User GCL/power balance tracking - wired into `GlobalMutationDispatcher` with MongoDB `$inc` operations
- ✅ Unblocked 4 E2.3 features:
  - ✅ Controller GCL updates (Node.js parity: `bulkUsers.inc(user, 'gcl', progressGain)`)
  - ✅ Boost effects GCL component (included in controller upgrades)
  - ✅ PWR_GENERATE_OPS ability (1:1 power-to-ops ratio, overflow drops)
  - ✅ Power spawn balance tracking (Node.js parity: `bulkUsers.inc(user, 'power', amount)`)
- ✅ 19 new GlobalMutationWriter tests (all passing)
- ✅ 726/726 tests passing (100% success rate)

**Completed Phases:**
- ✅ Phase 1: User GCL/power tracking (2026-01-21) - unblocked all E2 features
- ✅ Phase 2: Power effect tracking - moved to E2.3 (already complete)
- ✅ Phase 3: Keeper lair spawning (2026-01-21):
  - `KeeperLairStep` processor with 8/8 tests passing
  - Spawns source keeper creeps (user ID "3") with 5000 HP
  - 300-tick spawn timer, triggers on missing/weak keeper
  - Body: 17 TOUGH + 13 MOVE + 10 ATTACK + 10 RANGED_ATTACK
  - Legacy parity confirmed with Node.js engine
  - 734/734 tests passing (408 Engine + 70 Driver + 54 CLI + 202 HTTP)

**Remaining Phases:**
- 📋 Phase 4: Nuker operations (4-6 hours, split: E2 intent + E4 passive)

**Dependencies:** E4 foundation ✅

**Details:** See `e5.md` for detailed implementation notes and completion summary

---

## E6: Engine Loop Orchestration 📋

**Status:** Not Started

**Planned Deliverables:**
- `EngineHost` coordinates tick execution
- Main/runner/processor loops call managed engine instead of Node.js
- Tick scheduling and coordination
- Error handling and recovery

**Dependencies:**
- ✅ E2 complete (all intent handlers operational)
- ✅ E3 complete (intent validation)
- ✅ E4 complete (room processor infrastructure)
- ✅ E5 Phase 1 complete (user stats tracking)
- 📋 Driver queue service (D4 complete, integration pending)
- 📋 Telemetry sink (D8/E8 observability work)

---

## E7: Compatibility & Parity Validation 📋

**Status:** Not Started

**Planned Deliverables:**
- Lockstep testing framework (run same fixture through both engines)
- Automated divergence detection
- Parity test suite covering all mechanics
- Regression detection on schema changes
- E3 validator parity tests (deferred from E3.2)

**Prerequisites:**
- ✅ All E2 features complete (including 4 E5-blocked features)
- ✅ E3 validators implemented
- ✅ E4 simulation kernel complete
- ✅ E5 Phase 1 complete (user stats tracking)
- 📋 E5 Phase 2-4 complete (optional - keeper rooms, nukers)
- 📋 E6 orchestration complete
- ✅ Legacy Node.js engine repo access

**Details:** See `e2.md` for parity-critical feature status, `e3.md` for deferred E3 parity validation

---

## E8: Observability & Tooling 📋

**Status:** Not Started

**Planned Deliverables:**
- Engine metrics flow to telemetry
- Export E3 validation statistics to telemetry (deferred from E3.4)
- Diagnostics commands (inspect room state, intent queue, etc.)
- Operator playbooks for debugging
- Performance profiling tools

**Dependencies:**
- D8 logging stack
- Scheduler hooks
- E6 orchestration

---

## Summary

**Overall Engine Progress:** E1-E4 complete ✅, E5 Phase 1-3 complete ✅, E6-E8 pending

**Completed Milestones:**
- ✅ E1: Legacy engine surface mapped
- ✅ E2: Data & storage model complete (all 240 tests passing)
- ✅ E3: Intent validation pipeline complete (354 tests passing)
- ✅ E4: Simulation kernel complete (passive regeneration systems)
- ✅ E5 Phase 1: User GCL/power tracking complete (unblocked all E2 features)
- ✅ E5 Phase 2: Power effect tracking complete (moved to E2.3)
- ✅ E5 Phase 3: Keeper lair spawning complete (8 tests, legacy parity confirmed)

**Test Status:** 734/734 passing (408 Engine + 70 Driver + 54 CLI + 202 HTTP)

**Remaining Work:**
- 📋 E5 Phase 4: Nuker operations (4-6 hours estimated)
- 📋 E6: Engine loop orchestration (depends on: E4/E5 complete ✅, Driver queue service, Telemetry sink)
- 📋 E7: Parity validation (depends on: E1-E6 complete)
- 📋 E8: Observability & tooling (depends on: D8 logging stack, Scheduler hooks, E6 orchestration)
6. Complete E6 (Orchestration) → enables managed engine deployment
7. Complete E7 (Parity Validation) → lockstep testing vs Node.js
8. E8 (Observability) can proceed in parallel with E6/E7

**Next Milestone:** E5 Phase 4 (Nuker Operations) OR E6 (Engine Loop Orchestration)

**Reference Documents:**
- E1 (Legacy surface mapping): `e1.md`
- E2 (Handlers and deferred features): `e2.md`
- E3 (Intent validation overview): `e3.md`
  - E3.1 (Validation infrastructure): `e3.1.md` ✅
  - E3.2 (Validator implementation): `e3.2.md` ✅
- E5 (Global systems blockers): `e5.md`
- Data model design: `data-model.md`
- Coding patterns: `../../src/ScreepsDotNet.Engine/CLAUDE.md`
