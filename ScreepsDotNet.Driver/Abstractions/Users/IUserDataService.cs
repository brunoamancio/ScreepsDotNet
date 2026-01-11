using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Driver.Abstractions.Users;

public interface IUserDataService
{
    Task<IReadOnlyList<UserDocument>> GetActiveUsersAsync(CancellationToken token = default);
    Task<UserDocument?> GetUserAsync(string userId, CancellationToken token = default);

    Task SaveUserMemoryAsync(string userId, string memoryJson, CancellationToken token = default);
    Task SaveUserMemorySegmentsAsync(string userId, IReadOnlyDictionary<int, string> segments, CancellationToken token = default);
    Task SaveUserInterShardSegmentAsync(string userId, string segmentData, CancellationToken token = default);

    Task SaveUserIntentsAsync(string userId, UserIntentWritePayload payload, CancellationToken token = default);
    Task ClearGlobalIntentsAsync(CancellationToken token = default);

    Task AddRoomToUserAsync(string userId, string roomName, CancellationToken token = default);
    Task RemoveRoomFromUserAsync(string userId, string roomName, CancellationToken token = default);
}

public sealed record UserIntentWritePayload(
    IReadOnlyDictionary<string, object?> Rooms,
    IReadOnlyList<NotifyIntentPayload> Notifications,
    IReadOnlyDictionary<string, object?>? Global = null);

public sealed record NotifyIntentPayload(string Message, int GroupIntervalMinutes);
