namespace ScreepsDotNet.Engine.Data.Bulk;

using ScreepsDotNet.Driver.Contracts;

public interface IRoomMutationWriter
{
    void Upsert(RoomObjectSnapshot document);
    void Patch(string objectId, RoomObjectPatchPayload patch);
    void Remove(string objectId);
    void SetRoomInfoPatch(RoomInfoPatchPayload patch);
    void SetEventLog(IRoomEventLogPayload? eventLog);
    void SetMapView(IRoomMapViewPayload? mapView);
    int GetMutationCount();
    bool TryGetPendingPatch(string objectId, out RoomObjectPatchPayload patch);
    bool IsMarkedForRemoval(string objectId);
    Task FlushAsync(CancellationToken token = default);
    void Reset();
}
