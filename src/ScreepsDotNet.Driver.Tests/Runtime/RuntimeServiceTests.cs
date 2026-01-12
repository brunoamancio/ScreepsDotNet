using System.Text.Json;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Services.Runtime;

namespace ScreepsDotNet.Driver.Tests.Runtime;

public sealed class RuntimeServiceTests
{
    private readonly RuntimeService _service = new(new StubRuntimeSandboxPool());

    [Fact]
    public async Task ExecuteAsync_UpdatesMemoryAndIntents()
    {
        var context = new RuntimeExecutionContext(
            UserId: "user123",
            CodeHash: "hash",
            CpuLimit: 50,
            CpuBucket: 1000,
            GameTime: 12345,
            Memory: new Dictionary<string, object?> { ["counter"] = 1 },
            MemorySegments: new Dictionary<int, string>(),
            InterShardSegment: null,
            RuntimeData: new Dictionary<string, object?>
            {
                ["script"] = """
Memory.counter = (Memory.counter || 0) + 1;
registerIntent('spawn', { room: 'W1N1' });
console.log('tick', GameTime);
"""
            });

        var result = await _service.ExecuteAsync(context);
        Assert.True(string.IsNullOrEmpty(result.Error), result.Error);

        Assert.True(result.RoomIntents.TryGetValue("W1N1", out var roomIntents)
                    && roomIntents.ContainsKey("spawn"),
            "Expected room-level spawn intent.");
        Assert.Empty(result.GlobalIntents);
        Assert.Empty(result.Notifications);
        Assert.NotNull(result.Memory);

        var memoryDoc = JsonDocument.Parse(result.Memory!);
        Assert.Equal(2, memoryDoc.RootElement.GetProperty("counter").GetInt32());
    }

    [Fact]
    public async Task ExecuteAsync_ForceColdSandbox_InvalidatesPool()
    {
        var pool = new TrackingSandboxPool();
        var service = new RuntimeService(pool);
        var context = new RuntimeExecutionContext(
            UserId: "user456",
            CodeHash: "hash",
            CpuLimit: 50,
            CpuBucket: 1000,
            GameTime: 999,
            Memory: new Dictionary<string, object?>(),
            MemorySegments: new Dictionary<int, string>(),
            InterShardSegment: null,
            RuntimeData: new Dictionary<string, object?>
            {
                ["script"] = "console.log('hi');"
            },
            ForceColdSandbox: true);

        await service.ExecuteAsync(context);

        Assert.True(pool.Invalidated);
        Assert.False(pool.Returned);
    }

    private sealed class StubRuntimeSandboxPool : IRuntimeSandboxPool
    {
        private readonly StubRuntimeSandbox _sandbox = new();

        public IRuntimeSandbox Rent() => _sandbox;

        public void Return(IRuntimeSandbox sandbox)
        {
        }

        public void Invalidate(IRuntimeSandbox sandbox)
        {
        }
    }

    private sealed class TrackingSandboxPool : IRuntimeSandboxPool
    {
        private readonly StubRuntimeSandbox _sandbox = new();
        public bool Returned { get; private set; }
        public bool Invalidated { get; private set; }

        public IRuntimeSandbox Rent() => _sandbox;

        public void Return(IRuntimeSandbox sandbox) => Returned = true;

        public void Invalidate(IRuntimeSandbox sandbox) => Invalidated = true;
    }

    private sealed class StubRuntimeSandbox : IRuntimeSandbox
    {
        public Task<RuntimeExecutionResult> ExecuteAsync(RuntimeExecutionContext context, CancellationToken token = default)
        {
            var counter = context.Memory.TryGetValue("counter", out var raw) && raw is int value
                ? value
                : 0;

            var roomIntents = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["W1N1"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["spawn"] = new Dictionary<string, object?>
                    {
                        ["room"] = "W1N1"
                    }
                }
            };

            var result = new RuntimeExecutionResult(
                ConsoleLog: [],
                ConsoleResults: [],
                Error: null,
                GlobalIntents: new Dictionary<string, object?>(),
                Memory: JsonSerializer.Serialize(new { counter = counter + 1 }),
                MemorySegments: context.MemorySegments,
                InterShardSegment: context.InterShardSegment,
                CpuUsed: 1,
                RoomIntents: roomIntents,
                Notifications: [],
                Metrics: new RuntimeExecutionMetrics(false, false, 0, 0));

            return Task.FromResult(result);
        }
    }
}
