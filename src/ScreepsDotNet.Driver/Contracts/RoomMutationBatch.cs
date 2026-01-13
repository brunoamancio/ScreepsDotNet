namespace ScreepsDotNet.Driver.Contracts;

public sealed record RoomMutationBatch(
    string RoomName,
    IReadOnlyList<RoomObjectUpsert> ObjectUpserts,
    IReadOnlyList<RoomObjectPatch> ObjectPatches,
    IReadOnlyList<string> ObjectDeletes,
    RoomInfoPatch? RoomInfoPatch,
    string? MapViewJson,
    string? EventLogJson);

public sealed record RoomObjectUpsert(string DocumentJson);

public sealed record RoomObjectPatch(string ObjectId, string UpdateJson);

public sealed record RoomInfoPatch(string UpdateJson);
