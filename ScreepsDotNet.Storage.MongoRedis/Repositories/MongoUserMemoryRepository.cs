using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Extensions;
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
        if (document.TryGetValue(MemoryField, out var memoryValue) && memoryValue.IsBsonDocument)
            return memoryValue.AsBsonDocument.ToPlainDictionary();

        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    public async Task UpdateMemoryAsync(string userId, string? path, JsonElement value, CancellationToken cancellationToken = default)
    {
        var document = await FindDocumentAsync(userId, cancellationToken).ConfigureAwait(false);
        var memory = document.TryGetValue(MemoryField, out var memoryValue) && memoryValue.IsBsonDocument
            ? memoryValue.AsBsonDocument : [];

        ApplyMemoryMutation(memory, path, value);

        document[MemoryField] = memory;
        await _collection.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq(IdField, userId), document, new ReplaceOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetMemorySegmentAsync(string userId, int segment, CancellationToken cancellationToken = default)
    {
        var document = await FindDocumentAsync(userId, cancellationToken).ConfigureAwait(false);
        if (document.TryGetValue(SegmentsField, out var segmentsValue) && segmentsValue.IsBsonDocument)
        {
            var segments = segmentsValue.AsBsonDocument;
            var key = segment.ToString();
            return segments.GetStringOrNull(key);
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

    private static void ApplyMemoryMutation(BsonDocument memory, string? path, JsonElement value)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            memory.Clear();
            if (value.ValueKind != JsonValueKind.Undefined)
            {
                var newValue = value.ToBsonValue();
                if (newValue is BsonDocument newDoc)
                {
                    foreach (var element in newDoc)
                        memory[element.Name] = element.Value;
                }
            }

            return;
        }

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (value.ValueKind == JsonValueKind.Undefined)
        {
            memory.RemoveValueAtPath(segments);
        }
        else
        {
            var bsonValue = value.ToBsonValue();
            memory.SetValueAtPath(segments, bsonValue);
        }
    }
}
