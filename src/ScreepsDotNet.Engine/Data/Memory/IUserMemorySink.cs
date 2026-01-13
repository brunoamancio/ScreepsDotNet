namespace ScreepsDotNet.Engine.Data.Memory;

public interface IUserMemorySink
{
    Task SaveRawMemoryAsync(string userId, string rawMemoryJson, CancellationToken token = default);
    Task SaveMemorySegmentsAsync(string userId, IReadOnlyDictionary<int, string> segments, CancellationToken token = default);
    Task SaveInterShardSegmentAsync(string userId, string segmentData, CancellationToken token = default);
}
