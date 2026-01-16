namespace ScreepsDotNet.Driver.Contracts;

public sealed record RoomStatsUpdate(
    string Room,
    int GameTime,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Metrics);
