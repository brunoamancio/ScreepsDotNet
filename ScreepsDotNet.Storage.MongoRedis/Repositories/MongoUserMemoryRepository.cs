using System.Text.Json;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Extensions;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoUserMemoryRepository(IMongoDatabaseProvider databaseProvider) : IUserMemoryRepository
{
    private readonly IMongoCollection<UserMemoryDocument> _collection = databaseProvider.GetCollection<UserMemoryDocument>(databaseProvider.Settings.UserMemoryCollection);

    public async Task<IDictionary<string, object?>> GetMemoryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var document = await FindDocumentAsync(userId, cancellationToken).ConfigureAwait(false);
        return document?.Memory is { Count: > 0 } memory
            ? new Dictionary<string, object?>(memory, StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    public async Task UpdateMemoryAsync(string userId, string? path, JsonElement value, CancellationToken cancellationToken = default)
    {
        var document = await FindDocumentAsync(userId, cancellationToken).ConfigureAwait(false) ?? new UserMemoryDocument { UserId = userId };
        document.Memory ??= new Dictionary<string, object?>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(path)) {
            document.Memory.Clear();
            if (value.ValueKind != JsonValueKind.Undefined) {
                var payload = value.ToObjectValue();
                if (payload is IDictionary<string, object?> dictionary) {
                    foreach (var kvp in dictionary)
                        document.Memory[kvp.Key] = kvp.Value;
                }
                else
                    document.Memory["value"] = payload;
            }
        }
        else {
            var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return;

            if (value.ValueKind == JsonValueKind.Undefined)
                document.Memory.RemoveValueAtPath(segments);
            else
                document.Memory.SetValueAtPath(segments, value.ToObjectValue());
        }

        await _collection.ReplaceOneAsync(doc => doc.UserId == userId,
                                          document,
                                          new ReplaceOptions { IsUpsert = true },
                                          cancellationToken)
                         .ConfigureAwait(false);
    }

    public async Task<string?> GetMemorySegmentAsync(string userId, int segment, CancellationToken cancellationToken = default)
    {
        var document = await FindDocumentAsync(userId, cancellationToken).ConfigureAwait(false);
        if (document?.Segments is null)
            return null;

        var key = segment.ToString();
        return document.Segments.TryGetValue(key, out var data) ? data : null;
    }

    public Task SetMemorySegmentAsync(string userId, int segment, string? data, CancellationToken cancellationToken = default)
    {
        var key = segment.ToString();
        var update = data is null
            ? Builders<UserMemoryDocument>.Update.Unset($"segments.{key}")
            : Builders<UserMemoryDocument>.Update.Set($"segments.{key}", data);

        return _collection.UpdateOneAsync(document => document.UserId == userId,
                                           update,
                                           new UpdateOptions { IsUpsert = true },
                                           cancellationToken);
    }

    private async Task<UserMemoryDocument?> FindDocumentAsync(string userId, CancellationToken cancellationToken)
        => await _collection.Find(document => document.UserId == userId)
                            .FirstOrDefaultAsync(cancellationToken)
                            .ConfigureAwait(false);
}
