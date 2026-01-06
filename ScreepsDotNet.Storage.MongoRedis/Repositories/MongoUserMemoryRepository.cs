using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoUserMemoryRepository : IUserMemoryRepository
{
    private const string IdField = "_id";
    private const string MemoryField = "memory";
    private const string SegmentsField = "segments";

    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoUserMemoryRepository(IMongoDatabaseProvider databaseProvider)
        => _collection = databaseProvider.GetCollection<BsonDocument>(databaseProvider.Settings.UserMemoryCollection);

    public async Task<IDictionary<string, object?>> GetMemoryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var document = await FindDocumentAsync(userId, cancellationToken).ConfigureAwait(false);
        if (document?.TryGetValue(MemoryField, out var memoryValue) == true && memoryValue is BsonDocument memoryDocument)
            return ConvertBsonDocument(memoryDocument);

        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    public async Task UpdateMemoryAsync(string userId, string? path, JsonElement value, CancellationToken cancellationToken = default)
    {
        var document = await FindDocumentAsync(userId, cancellationToken).ConfigureAwait(false) ?? new BsonDocument { { IdField, userId } };
        var memory = document.TryGetValue(MemoryField, out var memoryValue) && memoryValue.IsBsonDocument
            ? memoryValue.AsBsonDocument
            : new BsonDocument();

        if (string.IsNullOrWhiteSpace(path))
        {
            memory.Clear();
            if (value.ValueKind != JsonValueKind.Undefined)
            {
                var newValue = ConvertJsonElementToBsonValue(value);
                if (newValue is BsonDocument newDoc)
                {
                    foreach (var element in newDoc)
                        memory[element.Name] = element.Value;
                }
            }
        }
        else
        {
            var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (value.ValueKind == JsonValueKind.Undefined)
                RemoveValue(memory, segments, 0);
            else
                SetValue(memory, segments, 0, ConvertJsonElementToBsonValue(value));
        }

        document[MemoryField] = memory;
        await _collection.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq(IdField, userId), document, new ReplaceOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetMemorySegmentAsync(string userId, int segment, CancellationToken cancellationToken = default)
    {
        var document = await FindDocumentAsync(userId, cancellationToken).ConfigureAwait(false);
        if (document?.TryGetValue(SegmentsField, out var segmentsValue) == true && segmentsValue.IsBsonDocument)
        {
            var segments = segmentsValue.AsBsonDocument;
            var key = segment.ToString();
            if (segments.TryGetValue(key, out var segmentValue) && segmentValue.IsString)
                return segmentValue.AsString;
        }

        return null;
    }

    public async Task SetMemorySegmentAsync(string userId, int segment, string? data, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq(IdField, userId);
        var update = data is null
            ? Builders<BsonDocument>.Update.Unset($"{SegmentsField}.{segment}")
            : Builders<BsonDocument>.Update.Set($"{SegmentsField}.{segment}", data);

        await _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
    }

    private Task<BsonDocument> FindDocumentAsync(string userId, CancellationToken cancellationToken)
        => _collection.Find(Builders<BsonDocument>.Filter.Eq(IdField, userId)).FirstOrDefaultAsync(cancellationToken);

    private static IDictionary<string, object?> ConvertBsonDocument(BsonDocument document)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var element in document)
            result[element.Name] = ConvertBsonValue(element.Value);
        return result;
    }

    private static object? ConvertBsonValue(BsonValue value)
        => value switch
        {
            BsonDocument doc => ConvertBsonDocument(doc),
            BsonArray array => ConvertBsonArray(array),
            BsonString s => s.AsString,
            BsonBoolean b => b.AsBoolean,
            BsonInt32 i => i.AsInt32,
            BsonInt64 l => l.AsInt64,
            BsonDouble d => d.AsDouble,
            BsonDecimal128 dec => (double)dec.ToDecimal(),
            BsonNull => null,
            _ => value.ToString()
        };

    private static IList<object?> ConvertBsonArray(BsonArray array)
    {
        var result = new List<object?>(array.Count);
        foreach (var item in array)
            result.Add(ConvertBsonValue(item));
        return result;
    }

    private static BsonValue ConvertJsonElementToBsonValue(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Object => ConvertJsonElementToBsonDocument(element),
            JsonValueKind.Array => ConvertJsonArray(element),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => BsonNull.Value,
            _ => BsonNull.Value
        };

    private static BsonDocument ConvertJsonElementToBsonDocument(JsonElement element)
    {
        var document = new BsonDocument();
        foreach (var property in element.EnumerateObject())
            document[property.Name] = ConvertJsonElementToBsonValue(property.Value);
        return document;
    }

    private static BsonArray ConvertJsonArray(JsonElement element)
    {
        var array = new BsonArray();
        foreach (var item in element.EnumerateArray())
            array.Add(ConvertJsonElementToBsonValue(item));
        return array;
    }

    private static void SetValue(BsonDocument document, IReadOnlyList<string> segments, int index, BsonValue value)
    {
        var key = segments[index];
        if (index == segments.Count - 1)
        {
            document[key] = value;
            return;
        }

        if (!document.TryGetValue(key, out var child) || !child.IsBsonDocument)
        {
            var newChild = new BsonDocument();
            document[key] = newChild;
            SetValue(newChild, segments, index + 1, value);
            return;
        }

        SetValue(child.AsBsonDocument, segments, index + 1, value);
    }

    private static void RemoveValue(BsonDocument document, IReadOnlyList<string> segments, int index)
    {
        var key = segments[index];
        if (!document.TryGetValue(key, out var child))
            return;

        if (index == segments.Count - 1)
        {
            document.Remove(key);
            return;
        }

        if (child.IsBsonDocument)
        {
            RemoveValue(child.AsBsonDocument, segments, index + 1);
            if (!child.AsBsonDocument.Elements.Any())
                document.Remove(key);
        }
    }
}
