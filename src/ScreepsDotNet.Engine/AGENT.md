# ScreepsDotNet.Engine – AGENT LOG

## Mission
Rebuild the legacy Screeps engine (simulation kernel + processor) in .NET so it can run the same logic that the JavaScript engine currently executes, while remaining API-compatible with the new C# driver/runtime stack.

## Ground Rules
- Follow the repo-wide conventions (implicit usings, primary constructors, expression-bodied members, `Lock` type, collection expressions, no redundant `System.*` usings).
- Keep this file updated whenever a step moves from Pending → In Progress → Complete.
- Reference supporting docs (design notes, specs) directly instead of copying large excerpts.
- If a step produces artifacts (notes, benchmarks, spikes), save them under `docs/engine/` and link them here.

## High-Level Deliverables
1. Managed port of the simulation kernel (room processing, object lifecycle, intents).
2. Engine service surface that mirrors the Node driver/engine contract (tick orchestration, hooks, IPC shims).
3. Integration with existing .NET subsystems (runtime service, pathfinder, telemetry) strictly via the driver layer—**the engine must never call Mongo/Redis or storage adapters directly.**
4. Validation suite covering tick parity, CPU usage, and storage side effects against the legacy engine.

## Execution Plan

| ID  | Status   | Title | Summary & Exit Criteria | Key Dependencies |
|-----|----------|-------|-------------------------|------------------|
| E1  | Completed  | Map Legacy Engine Surface | Inventory the endpoints/APIs the Node engine exposes (IPC, `processor`, `engine`, `loop` modules). Produce a contract doc mapping each entry point to desired .NET equivalents. *Exit:* `docs/engine/legacy-surface.md` complete and referenced here. | Existing Node engine repo, driver compatibility notes. |
| E2  | In Progress  | Data & Storage Model | Define managed representations for room objects, users, power creeps, and bulk ops that sit on top of driver contracts (no direct DB access). *Exit:* Driver snapshot/mutation contracts in place + engine consuming them. See [`docs/engine/data-model.md`](../../docs/engine/data-model.md).<br/>**Progress:** Added `RoomStateProvider`/`GlobalStateProvider`, `RoomMutationWriterFactory`, and `UserMemorySink`; engine `ServiceCollectionExtensions.AddEngineCore()` wires these wrappers so all future engine code stays on driver abstractions. | Driver contracts, Screeps schemas. |
| E3  | Pending  | Intent Gathering & Validation | Implement services that ingest runtime intents (from driver queues) and validate them like the legacy checker. *Exit:* `IIntentPipeline` + validators with unit tests mirroring Node fixtures. | Driver runtime outputs, constants project. |
| E4  | Pending  | Simulation Kernel (Room Processor) | Port the tick logic: energy, creep actions, structures, combat, power, market. Break work into modules (movement, combat, controller, power). *Exit:* Managed processor produces identical room diffs vs. Node baseline for a representative fixture set. | E2, E3, Pathfinder service. |
| E5  | Pending  | Global Systems | Recreate global subsystems (market, high-level intents, shard messaging, NPC spawns). *Exit:* Services hooked into processor loop with integration tests covering market orders, NPC waves, respawns. | E4 foundation. |
| E6  | Pending  | Engine Loop Orchestration | Build the `EngineHost` that coordinates ticks, interacts with queues, publishes telemetry, and exposes hooks for the driver/runtime layers. *Exit:* Main/runner/processor loops can call into the managed engine instead of the legacy Node engine. | Driver queue service, runtime telemetry sink. |
| E7  | Pending  | Compatibility & Parity Validation | Run the legacy engine + new engine in lockstep using the regression harness (fixtures, tick recordings). *Exit:* Report documenting parity gaps (if any) plus automated tests that fail when divergence occurs. | Prior steps, legacy engine repo. |
| E8  | Pending  | Observability & Tooling | Ensure engine metrics (tick duration, CPU, storage ops) flow into the telemetry pipeline, add diagnostics commands, and document operational playbooks. *Exit:* Docs + dashboards (or placeholders) ready for operators. | D8 logging stack, scheduler hooks. |

## Tracking & Reporting
- When starting a step, add sub-bullets beneath the table detailing the concrete tasks, links to docs/PRs, and current blockers.
- Reference this file from root-level status reports so other agents know where to look for engine progress.
- For any experimentation that requires scripts or spikes, place them under `src/ScreepsDotNet.Engine/sandbox/` (gitignored) unless they become permanent.

## Immediate Next Steps
1. Finish the **E2.3 handler backlog** documented in [`docs/engine/e2.3-plan.md`](../../docs/engine/e2.3-plan.md): next ports are controller intents (upgrade/reserve/attack), resource I/O (`transfer`/`withdraw`/`pickup`/`drop`), lab boosts/reactions, structure energy routing (links/terminals/factories/power-spawns), and power creep abilities. Each step must emit action-log/stats updates through `RoomStatsSink`.
2. Keep extending the telemetry/observability flow: now that `RoomStatsPipeline` exports spawn/harvest/tombstone counters, verify new stats from the upcoming handlers arrive in the Mongo history + telemetry listeners before marking Step 5 complete.
3. Start outlining **E3 – Intent Gathering & Validation** requirements (contracts, validator surfaces) so once the remaining E2.3 steps land we can immediately hook the managed validator into the driver runtime.

Keep this AGENT log authoritative; other documentation should link back here for canonical status.***
