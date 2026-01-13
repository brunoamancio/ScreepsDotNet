# Legacy Pathfinder Regression Report

_Last run: January 13, 2026 @ 10:10 UTC_

Command:
```
source ~/.nvm/nvm.sh
nvm use 12
node src/native/pathfinder/scripts/run-legacy-regressions.js
```

Artifacts:
- `legacy-regressions.json` – raw output from the Node driver comparison (origin-first + canonical target-first paths).

## Summary

| Case | Ops | Cost | Incomplete | Result |
| --- | --- | --- | --- | --- |
| multi-room | 5 | 50 | false | ✅ Matches legacy driver |
| flee-baseline | 2 | 3 | false | ✅ Matches legacy driver |
| portal-callback | 45 | 31 | false | ✅ Matches legacy driver |

Notes:
- The legacy Node module reports paths in origin→target order. The .NET wrapper keeps the native ordering (target→origin) so the script normalizes both orientations before comparing.
- Native binaries were compiled with Node 12.22.12 (`native/build/Release/native.node`). Rerun `npx node-gyp rebuild -C ScreepsNodeJs/driver/native` if the binary is missing.
- If a future regression fails, inspect the JSON diff and update `PathfinderNativeIntegrationTests` (managed expectations) once the root cause is resolved.
