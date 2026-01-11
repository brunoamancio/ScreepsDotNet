namespace ScreepsDotNet.Driver.Abstractions.Runtime;

public interface IRuntimeService
{
    Task<RuntimeExecutionResult> ExecuteAsync(RuntimeExecutionContext context, CancellationToken token = default);
}

public sealed record RuntimeExecutionContext(
    string UserId,
    string CodeHash,
    int CpuLimit,
    int CpuBucket,
    int GameTime,
    IReadOnlyDictionary<string, object?> Memory,
    IReadOnlyDictionary<int, string> MemorySegments,
    string? InterShardSegment,
    IReadOnlyDictionary<string, object?> RuntimeData);

public sealed record RuntimeExecutionResult(
    IReadOnlyList<string> ConsoleLog,
    IReadOnlyList<string> ConsoleResults,
    string? Error,
    IReadOnlyDictionary<string, object?> Intents,
    string? Memory,
    IReadOnlyDictionary<int, string>? MemorySegments,
    string? InterShardSegment,
    int CpuUsed);
