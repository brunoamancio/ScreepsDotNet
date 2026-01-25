namespace ScreepsDotNet.Engine.Processors;

using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.GlobalMutations;
using ScreepsDotNet.Engine.Data.Memory;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors.Helpers;

public sealed class RoomProcessorContext(
    RoomState state,
    IRoomMutationWriter mutationWriter,
    ICreepStatsSink statsSink,
    IGlobalMutationWriter globalMutationWriter,
    INotificationSink notificationSink,
    RoomExitTopology? exitTopology = null)
{
    private readonly Dictionary<string, string> _rawMemory = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyDictionary<int, string>> _memorySegments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _interShardSegments = new(StringComparer.Ordinal);

    public RoomState State { get; private set; } = state;
    public IRoomMutationWriter MutationWriter { get; } = mutationWriter;
    public ICreepStatsSink Stats { get; } = statsSink;
    public IGlobalMutationWriter GlobalMutationWriter { get; } = globalMutationWriter;
    public INotificationSink Notifications { get; } = notificationSink;
    public RoomExitTopology? ExitTopology { get; } = exitTopology;

    /// <summary>
    /// Replace the room state (used by IntentValidationStep to apply filtered intents).
    /// </summary>
    internal void ReplaceState(RoomState newState)
    {
        ArgumentNullException.ThrowIfNull(newState);
        State = newState;
    }

    public void SetRawMemory(string userId, string memoryJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryJson);
        _rawMemory[userId] = memoryJson;
    }

    public void SetMemorySegments(string userId, IReadOnlyDictionary<int, string> segments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(segments);
        _memorySegments[userId] = segments;
    }

    public void SetInterShardSegment(string userId, string segment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(segment);
        _interShardSegments[userId] = segment;
    }

    internal async Task FlushMemoryAsync(IUserMemorySink sink, CancellationToken token)
    {
        foreach (var (userId, rawMemory) in _rawMemory)
            await sink.SaveRawMemoryAsync(userId, rawMemory, token).ConfigureAwait(false);

        foreach (var (userId, segments) in _memorySegments)
            await sink.SaveMemorySegmentsAsync(userId, segments, token).ConfigureAwait(false);

        foreach (var (userId, segment) in _interShardSegments)
            await sink.SaveInterShardSegmentAsync(userId, segment, token).ConfigureAwait(false);
    }

    internal void ClearPendingMemory()
    {
        _rawMemory.Clear();
        _memorySegments.Clear();
        _interShardSegments.Clear();
    }
}
