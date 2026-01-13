namespace ScreepsDotNet.Driver.Contracts;

using System.Collections.Generic;

public sealed record RoomIntentSnapshot(
    string RoomName,
    string? Shard,
    IReadOnlyDictionary<string, IntentEnvelope> Users,
    string RawJson);

public sealed record IntentEnvelope(
    string UserId,
    IReadOnlyDictionary<string, string> ObjectsManualJson,
    string RawJson);
