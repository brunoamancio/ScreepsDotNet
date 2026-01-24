namespace ScreepsDotNet.Engine.Data.Bulk;

using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Contracts;

internal sealed class RoomMutationWriter(
    string roomName,
    IRoomMutationDispatcher dispatcher) : IRoomMutationWriter
{
    private readonly List<RoomObjectUpsert> _upserts = [];
    private readonly List<RoomObjectPatch> _patches = [];
    private readonly List<string> _removals = [];

    private RoomInfoPatchPayload? _roomInfoPatch;
    private IRoomEventLogPayload? _eventLog;
    private IRoomMapViewPayload? _mapView;

    public void Upsert(RoomObjectSnapshot document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _upserts.Add(new RoomObjectUpsert(document));
    }

    public void Patch(string objectId, RoomObjectPatchPayload patch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);
        ArgumentNullException.ThrowIfNull(patch);
        if (!patch.HasChanges)
            return;

        _patches.Add(new RoomObjectPatch(objectId, patch));
    }

    public void Remove(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
            return;

        _removals.Add(objectId);
    }

    public void SetRoomInfoPatch(RoomInfoPatchPayload patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        if (!patch.HasChanges)
            return;

        _roomInfoPatch = patch;
    }

    public void SetEventLog(IRoomEventLogPayload? eventLog)
        => _eventLog = eventLog;

    public void SetMapView(IRoomMapViewPayload? mapView)
        => _mapView = mapView;

    public int GetMutationCount()
    {
        var count = _upserts.Count + _patches.Count + _removals.Count;
        if (_roomInfoPatch is not null) count++;
        if (_eventLog is not null) count++;
        if (_mapView is not null) count++;
        return count;
    }

    public bool TryGetPendingPatch(string objectId, out RoomObjectPatchPayload patch)
        => PendingPatchHelper.TryFindLastPatch(_patches, objectId, out patch);

    public async Task FlushAsync(CancellationToken token = default)
    {
        if (_upserts.Count == 0 &&
            _patches.Count == 0 &&
            _removals.Count == 0 &&
            _roomInfoPatch is null &&
            _eventLog is null &&
            _mapView is null) {
            return;
        }

        var batch = new RoomMutationBatch(
            roomName,
            [.. _upserts],
            [.. _patches],
            [.. _removals],
            _roomInfoPatch,
            _mapView,
            _eventLog);

        await dispatcher.ApplyAsync(batch, token).ConfigureAwait(false);
        Reset();
    }

    public void Reset()
    {
        _upserts.Clear();
        _patches.Clear();
        _removals.Clear();
        _roomInfoPatch = null;
        _eventLog = null;
        _mapView = null;
    }
}
