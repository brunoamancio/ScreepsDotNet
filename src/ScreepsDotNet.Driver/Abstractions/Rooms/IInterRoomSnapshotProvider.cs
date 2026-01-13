namespace ScreepsDotNet.Driver.Abstractions.Rooms;

using ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Supplies cached inter-room/global snapshots per tick for the global processor and engine.
/// </summary>
public interface IInterRoomSnapshotProvider
{
    Task<GlobalSnapshot> GetSnapshotAsync(CancellationToken token = default);
    void Invalidate();
}
