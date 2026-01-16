namespace ScreepsDotNet.Driver.Contracts;

using System.Collections.Generic;

/// <summary>
/// Typed representation of a per-tick room history snapshot that can be serialized by the history service.
/// </summary>
public sealed record RoomHistoryTickPayload(
    string Room,
    IReadOnlyDictionary<string, RoomObjectSnapshot> Objects,
    IReadOnlyDictionary<string, UserState> Users);
