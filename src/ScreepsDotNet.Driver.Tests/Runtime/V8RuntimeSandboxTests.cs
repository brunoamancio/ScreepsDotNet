using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Services.Runtime;

namespace ScreepsDotNet.Driver.Tests.Runtime;

public sealed class V8RuntimeSandboxTests
{
    private readonly V8RuntimeSandbox _sandbox = new(new RuntimeSandboxOptions());

    [Fact]
    public async Task ExecuteAsync_SkipsMemoryWriteWhenUnchanged()
    {
        var context = new RuntimeExecutionContext(
            UserId: "user1",
            CodeHash: "hash",
            CpuLimit: 50,
            CpuBucket: 1000,
            GameTime: 123,
            Memory: new Dictionary<string, object?> { ["counter"] = 1 },
            MemorySegments: new Dictionary<int, string>(),
            InterShardSegment: null,
            RuntimeData: new Dictionary<string, object?>
            {
                ["script"] = "const snapshot = Memory.counter;"
            });

        var result = await _sandbox.ExecuteAsync(context);
        Assert.Null(result.Memory);
    }

    [Fact]
    public async Task ExecuteAsync_RawMemorySetOverridesMemory()
    {
        var context = new RuntimeExecutionContext(
            UserId: "user2",
            CodeHash: "hash",
            CpuLimit: 50,
            CpuBucket: 1000,
            GameTime: 456,
            Memory: new Dictionary<string, object?>(),
            MemorySegments: new Dictionary<int, string>(),
            InterShardSegment: null,
            RuntimeData: new Dictionary<string, object?>
            {
                ["script"] = "RawMemory.set('{\"foo\":1}');"
            });

        var result = await _sandbox.ExecuteAsync(context);
        Assert.Equal("{\"foo\":1}", result.Memory);
    }

    [Fact]
    public async Task ExecuteAsync_TracksSegmentAndInterShardChanges()
    {
        var context = new RuntimeExecutionContext(
            UserId: "user3",
            CodeHash: "hash",
            CpuLimit: 50,
            CpuBucket: 1000,
            GameTime: 789,
            Memory: new Dictionary<string, object?>(),
            MemorySegments: new Dictionary<int, string>
            {
                [3] = "old"
            },
            InterShardSegment: "initial",
            RuntimeData: new Dictionary<string, object?>
            {
                ["script"] = """
RawMemory.segments[3] = "updated";
RawMemory.segments[5] = "new";
RawMemory.interShardSegment = "updated";
"""
            });

        var result = await _sandbox.ExecuteAsync(context);
        Assert.NotNull(result.MemorySegments);
        Assert.Equal("updated", result.MemorySegments![3]);
        Assert.Equal("new", result.MemorySegments[5]);
        Assert.Equal("updated", result.InterShardSegment);
    }
}
