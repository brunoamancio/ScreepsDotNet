# JavaScript Sandbox Plan
_Last updated: January 11, 2026_

D3 focuses on selecting and prototyping the runtime environment that will execute user scripts when the driver is rewritten in .NET.

## Chosen Direction: ClearScript + V8
After evaluating options (ClearScript, Node subprocess, managed engines, WASM), we’ll embed V8 via [ClearScript](https://github.com/Microsoft/ClearScript) directly inside the .NET driver.

### Rationale
- **Parity:** V8 is the same engine Screeps players target in the official backend, so language features and performance characteristics align with expectations.
- **In-process control:** ClearScript exposes hooks for execution timeouts, heap limits, host object access, and custom module loading, giving us fine control similar to `isolated-vm`.
- **Deployment:** Fits within our .NET host process; we ship the ClearScript binaries and V8 natives alongside the driver without running an extra Node runtime.

## Sandbox Architecture
```
V8RuntimeHost (singleton per service)
└── V8UserSandbox (per user)
    ├── Engine initialization
    │   ├── Load Screeps runtime bundle
    │   ├── Install CommonJS loader
    │   ├── Inject globals (Game, Memory, console, _halt)
    ├── Tick execution API
    │   ├── Execute user code entry point
    │   ├── Capture intents, console output, errors
    └── Resource controls
        ├── MaxHeapSize (256 MB + terrain overhead)
        ├── MaxRuntimeHeapSize (optional global budget)
        ├── ScriptTimeout (per tick CPU cap)
```

### Key Components
1. **`IV8RuntimeHost`**
   - Manages a shared `V8Runtime` instance and caches compiled runtime bundle scripts.
   - Provides `CreateUserSandboxAsync(UserRuntimeConfig config)`.
2. **`V8UserSandbox`**
   - Wraps a ClearScript `V8ScriptEngine`.
   - API surface:
     - `Task InitializeAsync(UserRuntimeAssets assets)` → loads static terrain data, runtime bundle, module loader.
     - `Task<RuntimeTickResult> ExecuteTickAsync(UserTickContext ctx, CancellationToken token)` → runs the user’s main loop, enforcing timeouts.
     - `void Dispose()` → releases the engine or schedules it for reuse.
3. **Module Loader**
   - Custom JS injected during initialization to wire `require`, `module`, `exports`, and per-user module cache.
   - Resolves host-provided modules (e.g., `driver`, `game`) and user-uploaded files.
4. **Host Bridges**
   - Expose managed services to JS via ClearScript’s host types (e.g., `HostFunctions`, `HostTypeCollection`).
   - Provide functions for pushing intents, logging, fetching CPU usage (`Game.cpu.getUsed`), reading/writing `Memory`.
5. **Resource Guards**
   - `engine.MaxHeapSize = 256 * 1024 * 1024 + terrainBytes`.
   - `engine.ScriptInterruptTimeout = userCpuLimitMs` (converted from tick CPU).
   - Use `engine.RuntimeHeapSizeSampleInterval` and ClearScript’s `Interrupt()` to halt runaway scripts.

## Compatibility Tasks
- Port Screeps’ runtime bootstrap scripts (from `ScreepsNodeJs/driver/lib/runtime/runtime.js` and related files) into the ClearScript host; consider reusing the compiled bundle if licensing permits.
- Reimplement `_halt` semantics so user scripts can opt to stop execution.
- Provide replacements for `Buffer`, `atob`, `btoa`, and other Node globals required by user code or the runtime bundle.

## Testing Strategy
1. **Unit tests:** mock `V8UserSandbox` to verify module loader behavior, intent capturing, and memory/timeout enforcement.
2. **Integration tests:** run official Screeps sample scripts (simple creep loops) through the sandbox and compare outputs to the legacy engine.
3. **Stress tests:** execute CPU-heavy scripts to confirm per-tick timeouts trigger and that the engine recovers cleanly.

## Next Steps
- [ ] Prototype `V8RuntimeHost` + `V8UserSandbox` scaffolding inside `ScreepsDotNet.Driver`.
- [ ] Port the runtime bundle loader and validate module caching with a simple `console.log('hello')` script.
- [ ] Integrate sandbox with the `makeRuntime` workflow once storage adapters are ready.

Once the prototype proves out, we’ll mark D3 complete and proceed to queue + bulk writer implementations (D4/D5).
