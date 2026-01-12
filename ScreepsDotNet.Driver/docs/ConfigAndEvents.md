# Config & Event Wiring Plan
_Last updated: January 11, 2026_

D7 addresses the driver’s global configuration surface (`driver.config.engine`) and the various events/hooks the legacy engine relies on.

## Scope
- Mirror the mutable configuration object exposed by `@screeps/driver`: `mainLoopMinDuration`, `mainLoopResetInterval`, `cpuMaxPerTick`, `cpuBucketSize`, `customIntentTypes`, `historyChunkSize`, `enableInspector`, etc.
- Provide an `EventEmitter` equivalent so engine stages can call `config.emit('mainLoopStage', stage, payload)` and user sandboxes can subscribe to `playerSandbox` events.
- Surface helper methods: `registerCustomObjectPrototype`, custom intent registration, etc.

## Design
### Interfaces
```csharp
public interface IDriverConfig
{
    event EventHandler<LoopStageEventArgs> MainLoopStage;
    event EventHandler<LoopStageEventArgs> RunnerLoopStage;
    event EventHandler<LoopStageEventArgs> ProcessorLoopStage;
    event EventHandler<PlayerSandboxEventArgs> PlayerSandbox;

    int MainLoopMinDurationMs { get; set; }
    int MainLoopResetIntervalMs { get; set; }
    int CpuMaxPerTick { get; set; }
    int CpuBucketSize { get; set; }
    int HistoryChunkSize { get; set; }
    bool UseSigintTimeout { get; set; }
    bool EnableInspector { get; set; }

    void RegisterCustomObjectPrototype(CustomObjectPrototype prototype);
    IReadOnlyCollection<CustomObjectPrototype> CustomObjectPrototypes { get; }

    void RegisterCustomIntentType(CustomIntentDefinition intent);
    IReadOnlyDictionary<string, CustomIntentDefinition> CustomIntentTypes { get; }
}
```
- `LoopStageEventArgs` replicates the `(stageName, payload)` pattern from Node.
- Provide additional events as needed (`init`, `preProcessObjectIntents`, `processObjectIntents`, etc.) based on engine usage.

### Implementation Notes
- Back the config object with simple POCOs, but expose event methods that map directly to the Node semantics. For compatibility with existing engine code during transition, consider a shim that provides `config.emit(eventName, ...args)` semantics.
- Persist tunable values (tick duration, inspector flag) to Redis keys (`MAIN_LOOP_MIN_DURATION`) so they survive restarts, mirroring the legacy driver.
- Custom prototypes/intents should be thread-safe because mods can register them at runtime (use `ConcurrentDictionary`).

### Event Sources
| Event | Triggered by |
| --- | --- |
| `init` | During `connect(processType)` after storage connects. |
| `mainLoopStage` | Main loop (stages: getUsers, addUsersToQueue, waitForUsers, etc.). |
| `runnerLoopStage` | Runner loop (start, runUser, saveResultFinish, finish). |
| `processorLoopStage` | Processor (start, getRoomData, processRoom, saveRoom). |
| `playerSandbox` | When creating a runtime, so mods can inject JS into the sandbox before scripts run. |
| Custom events (preProcessObjectIntents, postProcessObject, processRoom) | Already present in Node engine; we can reroute them through `IDriverConfig` so mods remain compatible. |

### Interop with Sandbox
- When `playerSandbox` fires, pass a wrapped sandbox object exposing `Run(string script)` so mods can execute additional JS within the user context.
- Ensure host callbacks are safe (no deadlocks) and respect CPU/timeout constraints.

### Telemetry
- Provide logging hooks on event dispatch to help diagnose stuck loops (e.g., log `mainLoopStage` transitions with timestamps when `MainLoopResetInterval` triggers).

## Implementation Steps
1. Create `DriverConfig` class implementing `IDriverConfig`, backed by options loaded from configuration (`appsettings` or CLI flags).
2. Add `DriverEvents` helper to expose Node-like `emit`/`on` semantics for compatibility shims.
3. Integrate config persistence with Redis env keys where applicable.
4. Update `AGENT.md` (D7) to “Plan completed (implementation pending)” until wiring is coded.

### Current Status (January 12, 2026)
- `DriverConfig` now persists loop/tick settings (`tickRate`, reset interval, CPU caps, history chunk size, SIGINT toggle, inspector flag) to Redis via `EnvironmentService`, so values survive restarts and match the legacy env keys.
- `EnvironmentService` exposes typed getters/setters for those Redis keys alongside the existing lifecycle helpers (tick started, rooms done, gametime).
- Node-style `config.emit(eventName, ...)` semantics are implemented: consumers can `Subscribe("mainLoopStage", handler)` and receive the same args mods expect from the legacy driver. The typed .NET events continue to fire in parallel.
