using System.Text.Json;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Services.Runtime;

namespace ScreepsDotNet.Driver.Tests.Runtime;

public sealed class RuntimeServiceTests
{
    private readonly RuntimeService _service = new(new StubRuntimeSandboxFactory());

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

    private sealed class StubRuntimeSandboxFactory : IRuntimeSandboxFactory
    {
        public IRuntimeSandbox CreateSandbox()
            => new StubRuntimeSandbox();
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
                Notifications: []);

            return Task.FromResult(result);
        }
    }
}
