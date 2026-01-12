namespace ScreepsDotNet.Driver.Abstractions.Bulk;

/// <summary>
/// Matches the semantics of the legacy bulk helper (lib/bulk.js), allowing
/// callers to stage multiple collection mutations and execute them in one go.
/// </summary>
public interface IBulkWriter<TDocument>
{
    bool HasPendingOperations { get; }

    void Update(string id, object delta);
    void Update(TDocument entity, object delta);

    void Insert(TDocument entity, string? id = null);

    void Remove(string id);
    void Remove(TDocument entity);

    void Increment(string id, string field, long amount);
    void AddToSet(string id, string field, object value);
    void Pull(string id, string field, object value);

    Task ExecuteAsync(CancellationToken token = default);
    void Clear();
}
