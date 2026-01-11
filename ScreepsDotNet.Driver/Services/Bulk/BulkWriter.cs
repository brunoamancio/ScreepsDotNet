using System.Runtime.CompilerServices;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using ScreepsDotNet.Driver.Abstractions.Bulk;

namespace ScreepsDotNet.Driver.Services.Bulk;

internal sealed class BulkWriter<TDocument>(
    IMongoCollection<TDocument> collection,
    BulkWriterIdAccessor<TDocument> idAccessor) : IBulkWriter<TDocument>
    where TDocument : class
{
    private readonly IMongoCollection<TDocument> _collection = collection;
    private readonly BulkWriterIdAccessor<TDocument> _idAccessor = idAccessor;
    private readonly Dictionary<string, BsonDocument> _updates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InsertEntry> _inserts = new(StringComparer.Ordinal);
    private readonly Dictionary<TDocument, string> _insertLookup = new(ReferenceEqualityComparer<TDocument>.Instance);
    private readonly List<WriteModel<TDocument>> _operations = [];

    public bool HasPendingOperations
        => _updates.Count > 0 || _inserts.Count > 0 || _operations.Count > 0;

    public void Update(string id, object delta)
    {
        if (string.IsNullOrWhiteSpace(id) || delta is null)
            return;

        var document = DocumentUtilities.CloneAndClean(delta);
        EnsureNoNestedDiffsWithoutEntity(document);
        StageUpdate(NormalizeId(id), document);
    }

    public void Update(TDocument entity, object delta)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (delta is null)
            return;

        var document = DocumentUtilities.CloneAndClean(delta);
        var entityId = EnsureEntityId(entity);
        ExpandNestedDiffs(entity, document);
        StageUpdate(entityId, document);
    }

    public void Insert(TDocument entity, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var document = entity.ToBsonDocument().DeepClone().AsBsonDocument;
        DocumentUtilities.RemoveHidden(document);
        var resolvedId = EnsureInsertId(entity, document, id);
        _inserts[resolvedId] = new InsertEntry(resolvedId, document, entity);
        _insertLookup[entity] = resolvedId;
    }

    public void Remove(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        var normalized = NormalizeId(id);
        if (_inserts.Remove(normalized, out var pendingInsert))
        {
            _insertLookup.Remove(pendingInsert.Source);
            return;
        }

        _operations.Add(new DeleteOneModel<TDocument>(_idAccessor.CreateFilter(normalized)));
    }

    public void Remove(TDocument entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (_insertLookup.Remove(entity, out var pendingId))
        {
            _inserts.Remove(pendingId);
            return;
        }

        Remove(EnsureEntityId(entity));
    }

    public void Increment(string id, string field, long amount)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(field))
            return;

        var normalized = NormalizeId(id);
        var update = new BsonDocument("$inc", new BsonDocument(field, amount));
        _operations.Add(new UpdateOneModel<TDocument>(_idAccessor.CreateFilter(normalized), new BsonDocumentUpdateDefinition<TDocument>(update)));
    }

    public void AddToSet(string id, string field, object value)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(field))
            return;

        var normalized = NormalizeId(id);
        var update = new BsonDocument("$addToSet", new BsonDocument(field, BsonValue.Create(value)));
        _operations.Add(new UpdateOneModel<TDocument>(_idAccessor.CreateFilter(normalized), new BsonDocumentUpdateDefinition<TDocument>(update)));
    }

    public void Pull(string id, string field, object value)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(field))
            return;

        var normalized = NormalizeId(id);
        var update = new BsonDocument("$pull", new BsonDocument(field, BsonValue.Create(value)));
        _operations.Add(new UpdateOneModel<TDocument>(_idAccessor.CreateFilter(normalized), new BsonDocumentUpdateDefinition<TDocument>(update)));
    }

    public async Task ExecuteAsync(CancellationToken token = default)
    {
        if (!HasPendingOperations)
            return;

        var models = new List<WriteModel<TDocument>>(_operations.Count + _updates.Count + _inserts.Count);
        models.AddRange(_operations);

        foreach (var (id, updateDocument) in _updates)
        {
            var update = new BsonDocument("$set", updateDocument);
            models.Add(new UpdateOneModel<TDocument>(_idAccessor.CreateFilter(id), new BsonDocumentUpdateDefinition<TDocument>(update)));
        }

        foreach (var entry in _inserts.Values)
        {
            var entity = BsonSerializer.Deserialize<TDocument>(entry.Document);
            models.Add(new InsertOneModel<TDocument>(entity));
        }

        await _collection.BulkWriteAsync(models, new BulkWriteOptions { IsOrdered = false }, token).ConfigureAwait(false);
        Clear();
    }

    public void Clear()
    {
        _operations.Clear();
        _updates.Clear();
        _inserts.Clear();
        _insertLookup.Clear();
    }

    private void StageUpdate(string id, BsonDocument document)
    {
        if (_inserts.TryGetValue(id, out var entry))
        {
            DocumentUtilities.Merge(entry.Document, document);
            return;
        }

        if (_updates.TryGetValue(id, out var existing))
        {
            DocumentUtilities.Merge(existing, document);
            return;
        }

        _updates[id] = document;
    }

    private string EnsureInsertId(TDocument entity, BsonDocument document, string? explicitId)
    {
        if (!string.IsNullOrWhiteSpace(explicitId))
        {
            var normalized = NormalizeId(explicitId);
            document["_id"] = _idAccessor.CreateBsonValue(normalized);
            _idAccessor.AssignId?.Invoke(entity, normalized);
            return normalized;
        }

        if (_idAccessor.GetId?.Invoke(entity) is { Length: > 0 } existingId)
        {
            document["_id"] = _idAccessor.CreateBsonValue(existingId);
            return existingId;
        }

        if (document.TryGetValue("_id", out var value))
        {
            var fromDocument = DocumentUtilities.ExtractStringId(value);
            if (!string.IsNullOrWhiteSpace(fromDocument))
                return fromDocument;
        }

        if (_idAccessor.GenerateId is { } generator)
        {
            var generated = generator();
            document["_id"] = _idAccessor.CreateBsonValue(generated);
            _idAccessor.AssignId?.Invoke(entity, generated);
            return generated;
        }

        throw new InvalidOperationException("Insert requires an identifier.");
    }

    private string EnsureEntityId(TDocument entity)
    {
        var id = _idAccessor.GetId?.Invoke(entity);
        if (!string.IsNullOrWhiteSpace(id))
            return NormalizeId(id);

        throw new InvalidOperationException("Entity reference must contain an identifier.");
    }

    private static string NormalizeId(string value)
        => value.Trim();

    private static void EnsureNoNestedDiffsWithoutEntity(BsonDocument document)
    {
        foreach (var element in document) {
            if (element.Value is BsonDocument or BsonArray)
                throw new InvalidOperationException($"Cannot update nested field '{element.Name}' without providing the original document.");
        }
    }

    private static void ExpandNestedDiffs(TDocument entity, BsonDocument delta)
    {
        var entityDocument = entity.ToBsonDocument().DeepClone().AsBsonDocument;
        DocumentUtilities.Merge(entityDocument, delta);
        DocumentUtilities.CopyToEntity(entityDocument, entity);

        foreach (var element in delta.ToList())
        {
            if (element.Value is not BsonDocument)
                continue;

            if (!entityDocument.TryGetValue(element.Name, out var merged) || merged is not BsonDocument mergedDocument)
                continue;

            delta[element.Name] = mergedDocument.DeepClone().AsBsonDocument;
        }
    }

    private sealed record InsertEntry(string Id, BsonDocument Document, TDocument Source);

    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static ReferenceEqualityComparer<T> Instance { get; } = new();

        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private static class DocumentUtilities
    {
        public static BsonDocument CloneAndClean(object value)
        {
            var document = value switch
            {
                BsonDocument existing => existing.DeepClone().AsBsonDocument,
                _ => value.ToBsonDocument()
            };
            RemoveHidden(document);
            return document;
        }

        public static void RemoveHidden(BsonValue value)
        {
            if (value is BsonDocument document)
            {
                var keys = document.Names.ToList();
                foreach (var key in keys)
                {
                    if (key.Length > 0 && key[0] == '_')
                    {
                        document.Remove(key);
                        continue;
                    }

                    RemoveHidden(document[key]);
                }

                if (document.Contains("$loki"))
                    document.Remove("$loki");
                return;
            }

            if (value is BsonArray array)
            {
                foreach (var item in array)
                    RemoveHidden(item);
            }
        }

        public static void Merge(BsonDocument target, BsonDocument source)
        {
            foreach (var element in source)
            {
                if (element.Value is BsonDocument child && target.TryGetValue(element.Name, out var existing) && existing is BsonDocument existingDocument)
                {
                    Merge(existingDocument, child);
                    continue;
                }

                target[element.Name] = element.Value.DeepClone();
            }
        }

        public static string ExtractStringId(BsonValue value)
        {
            return value switch
            {
                BsonObjectId objectId => objectId.Value.ToString(),
                BsonString bsonString => bsonString.Value ?? string.Empty,
                _ => value?.ToString() ?? string.Empty
            };
        }

        public static void CopyToEntity(BsonDocument document, TDocument entity)
        {
            if (entity is BsonDocument bsonDocument)
            {
                bsonDocument.Clear();
                bsonDocument.Merge(document);
                return;
            }

            var snapshot = BsonSerializer.Deserialize<TDocument>(document);
            EntitySynchronizer<TDocument>.Copy(snapshot, entity);
        }
    }

    private sealed class EntitySynchronizer<T>
        where T : class
    {
        private static readonly BsonClassMap ClassMap = BsonClassMap.LookupClassMap(typeof(T));

        public static void Copy(T source, T destination)
        {
            foreach (var memberMap in ClassMap.AllMemberMaps)
            {
                if (memberMap.Setter is null)
                    continue;

                var value = memberMap.Getter(source);
                memberMap.Setter(destination, value);
            }
        }
    }
}
