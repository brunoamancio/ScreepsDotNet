namespace ScreepsDotNet.Engine.Data.Memory;

using ScreepsDotNet.Driver.Abstractions.Users;

internal sealed class UserMemorySink(IUserDataService userDataService) : IUserMemorySink
{
    public Task SaveRawMemoryAsync(string userId, string rawMemoryJson, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawMemoryJson);
        return userDataService.SaveUserMemoryAsync(userId, rawMemoryJson, token);
    }

    public Task SaveMemorySegmentsAsync(string userId, IReadOnlyDictionary<int, string> segments, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(segments);
        var result = segments.Count == 0
            ? Task.CompletedTask
            : userDataService.SaveUserMemorySegmentsAsync(userId, segments, token);
        return result;
    }

    public Task SaveInterShardSegmentAsync(string userId, string segmentData, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(segmentData);
        return userDataService.SaveUserInterShardSegmentAsync(userId, segmentData, token);
    }
}
