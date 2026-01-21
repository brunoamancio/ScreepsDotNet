# Bulk Writer Implementation Plan
_Last updated: January 11, 2026_

D5 covers the managed replacement for `lib/bulk.js`, which batches updates/inserts/removals per Mongo collection before executing them in one operation.

## Requirements
- Keep API parity with the legacy helper: `update(objectIdOrDoc, delta)`, `insert(doc, id?)`, `remove(idOrDoc)`, `inc(id,key,amount)`, `addToSet`, `pull`, `execute()`.
- Merge nested objects when `update` is called with a live document reference (mutating the in-memory copy like the JS version does).
- Strip transient fields (properties starting with `_`, `$loki`) before persisting.
- Ensure operations are applied in the order defined by the JS implementation: staged updates first, then queued `bulk` ops, then inserts.
- Support both typed POCO entities (e.g., `RoomObjectDocument`) and dynamic BSON for collections without strong models.

## Design
### Interfaces
```csharp
public interface IBulkWriter<TDocument>
{
    void Update(TDocument entity, object delta);
    void Update(ObjectId id, object delta);
    string Insert(TDocument document, ObjectId? id = null);
    void Remove(TDocument entity);
    void Remove(ObjectId id);
    void Increment(TDocument entity, string field, long amount);
    void AddToSet(TDocument entity, string field, object value);
    void Pull(TDocument entity, string field, object value);
    Task<BulkWriteResult<TDocument>> ExecuteAsync(CancellationToken token = default);
}

public interface IBulkWriterFactory
{
    IBulkWriter<RoomObjectDocument> CreateRoomObjectsWriter();
    IBulkWriter<UserDocument> CreateUsersWriter();
    // ...repeat for flags, rooms, transactions, etc.
}
```

### Internal Representation
- Maintain three collections in the writer:
  1. `Dictionary<ObjectId, UpdateDefinition<T>> _updates` – merges successive updates for the same doc.
  2. `List<WriteModel<T>> _queuedOps` – stores `DeleteOneModel`, `$inc`, `$addToSet`, `$pull` commands.
  3. `List<T> _inserts` – holds new docs (with generated IDs) until execute.
- Provide helper to sanitize documents (`HiddenFieldStripper.Strip(object)`).

### Execution Flow
1. Convert `_updates` into `UpdateOneModel` instances using `Builders<T>.Update.Combine`.
2. Append `_queuedOps` (already `WriteModel`s) to the batch.
3. Append `InsertOneModel`s for `_inserts`.
4. Call `IMongoCollection<T>.BulkWriteAsync(models, options)`; options should set `IsOrdered = false` for parallelism unless ordering is required.
5. Clear internal state so the writer can be reused.

### Nested Object Merge
When `Update` receives a document reference (common in the engine), mutate the entity before scheduling the write, e.g.:
```csharp
void Update(TDocument entity, object delta)
{
    JsonMergePatch.Apply(entity, delta); // custom helper aligning with lodash merge
    ScheduleUpdate(entity.Id, delta);
}
```
Implement `JsonMergePatch` with `System.Text.Json` or manual reflection to mimic lodash’s shallow merge rules.

### Sanitization
- Recursively remove properties whose names start with `_` from `delta` objects to avoid leaking transient fields.
- On inserts, omit `Id` if Mongo should generate it; otherwise ensure `ObjectId` is valid.

### Testing
- Unit tests verifying:
  - Multiple `Update` calls merge correctly and produce a single `UpdateOneModel`.
  - `Insert` + `Remove` before execute cancels the insertion (parity with JS version).
  - Hidden fields stripped.
  - `Inc/AddToSet/Pull` produce the expected Mongo update definitions.

## Next Steps
- Implement interfaces + `BulkWriterFactory` inside `ScreepsDotNet.Driver.Storage`.
- Wire factory methods into driver services (processor/global logic).
- Mark D5 as “Plan completed (implementation pending)” until code lands.
