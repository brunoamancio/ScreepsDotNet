using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Driver.Abstractions.Rooms;

/// <summary>
/// Handles rehoming creeps that reached an exit and carry an <c>interRoom</c> payload.
/// </summary>
public interface IInterRoomTransferProcessor
{
    /// <summary>
    /// Processes pending inter-room transfers, updating Mongo documents and activating the destination rooms.
    /// Returns the number of creeps that were moved.
    /// </summary>
    Task<int> ProcessTransfersAsync(IReadOnlyDictionary<string, RoomInfoSnapshot> accessibleRooms, CancellationToken token = default);
}
