# History & Notification Plan
_Last updated: January 11, 2026_

D9 addresses the remaining driver helpers: history snapshot uploads, map view persistence, room activation, accessible room updates, and notification helpers (`sendNotification`, `notifyRoomsDone`, etc.).

## Responsibilities
1. **History Service**
   - Save per-room tick data (`saveRoomHistory`) and trigger uploads when `gameTime % historyChunkSize == 0`.
   - Mirror legacy behavior: `driver.history.saveTick(roomId, gameTime, data)` and `driver.history.upload(roomId, baseTime)`.
   - Use Mongo/GridFS or filesystem storage depending on config.

2. **Room Event Log / Map View**
   - `saveRoomEventLog(roomId, events)` → store JSON in Redis hash `ROOM_EVENT_LOG`.
   - `mapViewSave(roomId, mapView)` → store JSON serialized snapshots keyed by room.

3. **Accessible Rooms + Status Data**
   - `updateAccessibleRoomsList()` → gather all rooms with `status == 'normal'` and `openTime < now`, flush to `env.ACCESSIBLE_ROOMS` (JSON array).
   - `updateRoomStatusData()` → compile `novice`, `respawn`, `closed` dictionaries and cache under `env.ROOM_STATUS_DATA`.

4. **Notifications**
   - `sendNotification(userId, message)` → update `users.notifications` with `type = 'msg'` entries, same throttling as legacy.
   - `notifyRoomsDone(gameTime)` → publish to `ROOMS_DONE` channel.
   - `sendConsoleMessages`, `sendConsoleError` already covered in D8 but referenced here for completeness.

5. **Room Activation Helpers**
   - `activateRoom(roomId)` / `activateRoom(IEnumerable<string>)` → add to `env.ACTIVE_ROOMS` set.
   - `notifyTickStarted()` ties into lifecycle (already handled in D1/D7 but tracked here to ensure consistency).

## Design Outline
- Introduce `IHistoryService`, `IRoomLogService`, `INotificationService`, `IRoomStatusService` interfaces so these tasks can be tested independently.
- Implement services using storage adapters from D2; reuse existing repositories where applicable.
- Provide wrappers that expose the same method names as legacy driver exports so the engine compatibility layer remains thin.

## Testing Considerations
- Unit tests verifying serialization format (event logs, map views) matches legacy conventions (JSON arrays of coordinates, etc.).
- Integration tests ensuring `updateAccessibleRoomsList` writes the proper JSON payload and that `getAllRoomsNames` drains the set, similar to the Node flow.
- Notification throttling tests (group interval, error notification interval) to ensure we don’t spam `users.notifications`.

## Current Implementation
- `HistoryService` stores per-room tick diffs in Redis; once a chunk is ready it persists the chunk to Mongo (`rooms.history`) and emits `roomHistorySaved`.
- `RoomHistoryPipeline` listens for that event and refreshes both the map-view snapshot and room-event log payload in Redis so UI consumers stay in sync—no extra filesystem copies are produced.
- `NotificationService` handles console/watchdog/intent notifications through the shared throttler, while `RoomsDoneBroadcaster` throttles `roomsDone` emits so downstream listeners aren’t flooded when ticks complete rapidly.

## Next Steps
- Swap the filesystem uploader for the final storage target (S3, GridFS, etc.) once the deployment story is ready.
- Hook the throttled `roomsDone` emitter into the upcoming compatibility shim so legacy engine consumers receive the same cadence they expect today.

## Current Progress
- `HistoryService` now batches per-room ticks in Redis, persists the assembled chunk to Mongo (`rooms.history`) with upsert semantics, and only then emits `config.emit('roomHistorySaved', ...)` so downstream uploaders can rely on durable storage.
- Notification helpers (`SendNotificationAsync`, `PublishConsole*`, `NotifyRoomsDoneAsync`) share the throttling logic in `NotificationThrottler`, while runtime watchdog alerts reuse the same path with the `"watchdog"` notification type.
- Remaining work: wire the persisted chunks into whatever long-term blob/S3 upload pipeline we adopt and add throttled listeners for high-volume history consumers once processor loops cover more intent handlers.
