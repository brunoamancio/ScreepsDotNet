namespace ScreepsDotNet.Driver.Contracts;

public sealed record RoomObjectMutation(
    string Id,
    RoomObjectMutationType Type,
    RoomObjectSnapshot? Snapshot = null,
    GlobalRoomObjectPatch? Patch = null);

public enum RoomObjectMutationType
{
    Upsert,
    Patch,
    Remove
}
