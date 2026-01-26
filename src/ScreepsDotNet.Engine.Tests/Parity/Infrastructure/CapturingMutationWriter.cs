namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;

/// <summary>
/// Mutation writer that captures all mutations for parity testing
/// </summary>
public sealed class CapturingMutationWriter : IRoomMutationWriter
{
    public List<(string ObjectId, RoomObjectPatchPayload Payload)> Patches { get; } = [];
    public List<RoomObjectSnapshot> Upserts { get; } = [];
    public List<string> Removals { get; } = [];
    public RoomInfoPatchPayload? RoomInfoPatch { get; private set; }
    public IRoomEventLogPayload? EventLog { get; private set; }
    public IRoomMapViewPayload? MapView { get; private set; }

    public void Upsert(RoomObjectSnapshot document)
        => Upserts.Add(document);

    public void Patch(string objectId, RoomObjectPatchPayload patch)
        => Patches.Add((objectId, patch));

    public void Remove(string objectId)
        => Removals.Add(objectId);

    public void SetRoomInfoPatch(RoomInfoPatchPayload patch)
        => RoomInfoPatch = patch;

    public void SetEventLog(IRoomEventLogPayload? eventLog)
        => EventLog = eventLog;

    public void SetMapView(IRoomMapViewPayload? mapView)
        => MapView = mapView;

    public int GetMutationCount()
        => Patches.Count + Upserts.Count + Removals.Count;

    public bool TryGetPendingPatch(string objectId, out RoomObjectPatchPayload patch)
        => PendingPatchHelper.TryFindLastPatch(Patches, objectId, out patch);

    public bool IsMarkedForRemoval(string objectId)
        => Removals.Contains(objectId);

    public Task FlushAsync(CancellationToken token = default)
        => Task.CompletedTask;

    public void Reset()
    {
        Patches.Clear();
        Upserts.Clear();
        Removals.Clear();
        RoomInfoPatch = null;
        EventLog = null;
        MapView = null;
    }
}
