namespace ScreepsDotNet.Engine.Data.Bulk;

public interface IRoomMutationWriter
{
    void Upsert(object document);
    void UpsertJson(string documentJson);
    void Patch(string objectId, object updateDocument);
    void PatchJson(string objectId, string updateJson);
    void Remove(string objectId);
    void SetRoomInfoPatch(object updateDocument);
    void SetRoomInfoPatchJson(string updateJson);
    void SetEventLog(string? eventLogJson);
    void SetMapView(string? mapViewJson);
    Task FlushAsync(CancellationToken token = default);
    void Reset();
}
