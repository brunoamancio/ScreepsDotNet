namespace ScreepsDotNet.Engine.Data.Bulk;

using System.Text.Json;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Contracts;

internal sealed class RoomMutationWriter(
    string roomName,
    IRoomMutationDispatcher dispatcher,
    JsonSerializerOptions? serializerOptions = null) : IRoomMutationWriter
{
    private readonly List<RoomObjectUpsert> _upserts = [];
    private readonly List<RoomObjectPatch> _patches = [];
    private readonly List<string> _removals = [];
    private readonly JsonSerializerOptions _serializerOptions = serializerOptions ?? new(JsonSerializerDefaults.Web);

    private RoomInfoPatch? _roomInfoPatch;
    private string? _eventLogJson;
    private string? _mapViewJson;

    public void Upsert(object document)
        => UpsertJson(Serialize(document));

    public void UpsertJson(string documentJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentJson);
        _upserts.Add(new RoomObjectUpsert(documentJson));
    }

    public void Patch(string objectId, object updateDocument)
        => PatchJson(objectId, Serialize(updateDocument));

    public void PatchJson(string objectId, string updateJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(updateJson);
        _patches.Add(new RoomObjectPatch(objectId, updateJson));
    }

    public void Remove(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
            return;

        _removals.Add(objectId);
    }

    public void SetRoomInfoPatch(object updateDocument)
        => SetRoomInfoPatchJson(Serialize(updateDocument));

    public void SetRoomInfoPatchJson(string updateJson)
    {
        if (string.IsNullOrWhiteSpace(updateJson))
            return;

        _roomInfoPatch = new RoomInfoPatch(updateJson);
    }

    public void SetEventLog(string? eventLogJson)
        => _eventLogJson = string.IsNullOrWhiteSpace(eventLogJson) ? null : eventLogJson;

    public void SetMapView(string? mapViewJson)
        => _mapViewJson = string.IsNullOrWhiteSpace(mapViewJson) ? null : mapViewJson;

    public async Task FlushAsync(CancellationToken token = default)
    {
        if (_upserts.Count == 0 &&
            _patches.Count == 0 &&
            _removals.Count == 0 &&
            _roomInfoPatch is null &&
            string.IsNullOrWhiteSpace(_eventLogJson) &&
            string.IsNullOrWhiteSpace(_mapViewJson))
            return;

        var batch = new RoomMutationBatch(
            roomName,
            _upserts.ToArray(),
            _patches.ToArray(),
            _removals.ToArray(),
            _roomInfoPatch,
            _mapViewJson,
            _eventLogJson);

        await dispatcher.ApplyAsync(batch, token).ConfigureAwait(false);
        Reset();
    }

    public void Reset()
    {
        _upserts.Clear();
        _patches.Clear();
        _removals.Clear();
        _roomInfoPatch = null;
        _eventLogJson = null;
        _mapViewJson = null;
    }

    private string Serialize(object document)
        => document switch
        {
            null => string.Empty,
            string json => json,
            _ => JsonSerializer.Serialize(document, _serializerOptions)
        };
}
