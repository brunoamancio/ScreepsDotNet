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
| E5 | ✅ | Global Systems | All phases complete: User GCL/power tracking, keeper lairs, nuker operations. Global mutations (`IGlobalMutationWriter`) operational. | E4 foundation |
| E6 | 📋 | Engine Loop Orchestration | `EngineHost` coordinates ticks; main/runner/processor loops call managed engine | Driver queue service, telemetry sink |
| E7 | 📋 | Compatibility & Parity Validation | Lockstep testing vs. Node engine, automated divergence detection | Prior steps, legacy engine repo |
| E8 | 📋 | Observability & Tooling | Engine metrics flow to telemetry, diagnostics commands, operator playbooks | D8 logging stack, scheduler hooks |
| E9 | 📋 | NPC AI Logic | Keeper and invader AI implemented with pathfinding, targeting, and combat logic | E5 Phase 3 (spawning), E6-E8 complete |

---

## E1: Map Legacy Engine Surface ✅

**Status:** Complete

**Deliverables:**
- ✅ Node engine API inventory documented in `e1.md`
- ✅ All game mechanics catalogued (creep actions, structure logic, combat, market, etc.)

---

## E2: Data & Storage Model ✅ Complete (2026-01-21)

**Status:** Complete - 11/11 handler families, 240 tests

**Summary:**
- Driver contracts and snapshot providers
- Engine isolated from storage layer (no direct DB access)
- All room-level intent handlers implemented
- E5 Phase 1 unblocked 4 E2 features (PWR_GENERATE_OPS, user GCL/power tracking)

**Details:** See `e2.md` for handler breakdown, `data-model.md` for contracts

---

## E3: Intent Gathering & Validation ✅ Complete

**Status:** Complete - 5 validators, 96 tests

**Summary:**
- `IntentValidationPipeline` with 5 validators (Range, Resource, Permission, State, Schema)
- `ValidationStatisticsSink` for observability
- Integrated into RoomProcessor, no E2 regressions

**Deferred:**
- 📋 Parity validation against Node.js (E7)
- 📋 Export statistics to telemetry (E8)
- 📋 Remove inline validation from E2 handlers (optional cleanup)

**Details:** See `e3.md` for implementation details

---

## E4: Simulation Kernel (Room Processor) ✅ Complete

**Status:** Complete - Passive regeneration systems operational

**Summary:**
- RoomProcessor orchestration with 20 processor steps
- Passive regeneration: Source energy (15 tests), Mineral (12 tests)
- Construction site decay verified as non-existent in Node.js engine
- NPC spawning, power banks, and nuker features moved to E5

**Details:** See `e4.md` for implementation details

---

## E5: Global Systems ✅ Complete (2026-01-21)

**Status:** All Phases Complete ✅

**Summary:**
- Phase 1: Global mutation infrastructure (`IGlobalMutationWriter`) with user GCL/power tracking
- Phase 2: Power effect tracking (moved to E2.3)
- Phase 3: Keeper lair spawning (`KeeperLairStep`) with 8 tests
- Phase 4: Nuker operations (`NukerIntentStep` + `NukeLandingStep`) with 20 tests

**Deferred:**
- 📋 Keeper AI logic (pathfinding, targeting) - deferred to E9

**Details:** See `e5.md` for detailed implementation notes

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

## E9: NPC AI Logic 📋

**Status:** Not Started

**Summary:**
- Keeper AI: pathfinding, target selection, combat (5 hours)
- Invader AI: basic movement and attack patterns (2-3 hours, if needed)
- Memory field support (`memory_sourceId`, `memory_move`)
- Path caching and reuse (50-tick cache)

**Dependencies:**
- E5 Phase 3 complete (keeper spawning) ✅
- E6 orchestration (AI runs in processor loop)
- E7 parity framework (for testing AI behavior)
- E8 observability (for debugging AI decisions)

**Details:** See `e9.md` for detailed implementation plan

---

## Summary

**Overall Engine Progress:** E1-E5 complete ✅, E6-E9 pending

**Completed Milestones:**
- ✅ E1: Legacy engine surface mapped
- ✅ E2: Data & storage model complete (all 240 tests passing)
- ✅ E3: Intent validation pipeline complete (354 tests passing)
- ✅ E4: Simulation kernel complete (passive regeneration systems)
- ✅ E5 Phase 1: User GCL/power tracking complete (unblocked all E2 features)
- ✅ E5 Phase 2: Power effect tracking complete (moved to E2.3)
- ✅ E5 Phase 3: Keeper lair spawning complete (8 tests, legacy parity confirmed)
- ✅ E5 Phase 4: Nuker operations complete (20 tests, legacy parity confirmed)

**Test Status:** 754/754 passing (428 Engine + 70 Driver + 54 CLI + 202 HTTP)

**Remaining Work:**
- 📋 E6: Engine loop orchestration (depends on: E4/E5 complete ✅, Driver queue service, Telemetry sink)
- 📋 E7: Parity validation (depends on: E1-E6 complete)
- 📋 E8: Observability & tooling (depends on: D8 logging stack, Scheduler hooks, E6 orchestration)
- 📋 E9: NPC AI logic (depends on: E5 Phase 3 ✅, E6-E8 complete)

**Next Milestone:** E6 (Engine Loop Orchestration)

**Reference Documents:**
- E1 (Legacy surface mapping): `e1.md`
- E2 (Handlers and deferred features): `e2.md`
- E3 (Intent validation overview): `e3.md`
  - E3.1 (Validation infrastructure): `e3.1.md` ✅
  - E3.2 (Validator implementation): `e3.2.md` ✅
- E5 (Global systems): `e5.md`
- E9 (NPC AI logic): `e9.md`
- Data model design: `data-model.md`
- Coding patterns: `../../src/ScreepsDotNet.Engine/CLAUDE.md`
