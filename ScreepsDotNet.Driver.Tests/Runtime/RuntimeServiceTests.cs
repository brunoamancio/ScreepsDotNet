using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Services.Runtime;

namespace ScreepsDotNet.Driver.Tests.Runtime;

public sealed class RuntimeServiceTests
{
    private readonly RuntimeService _service;

    public RuntimeServiceTests()
    {
        var options = Options.Create(new RuntimeSandboxOptions());
        var factory = new V8RuntimeSandboxFactory(options, NullLoggerFactory.Instance);
        _service = new RuntimeService(factory);
    }

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

        Assert.True(result.Intents.ContainsKey("spawn"), $"Intents: {string.Join(',', result.Intents.Keys)}");
        Assert.NotNull(result.Memory);

        var memoryDoc = JsonDocument.Parse(result.Memory!);
        Assert.Equal(2, memoryDoc.RootElement.GetProperty("counter").GetInt32());
    }
}
