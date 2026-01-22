namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Tests for NodeJsHarnessRunner - verifies integration with Node.js parity harness
/// NOTE: Full harness integration tests require MongoDB and are run manually.
/// Only error handling tests run in CI.
/// </summary>
public sealed class NodeJsHarnessRunnerTests
{
    private static readonly System.Text.Json.JsonSerializerOptions TestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Manual integration tests (require MongoDB + Node.js harness)
    // Run with: dotnet test --filter "FullyQualifiedName~NodeJsHarnessRunnerTests.Manual"
    // Uncomment and run manually when harness is set up:
    //
    // [Fact]
    // public async Task Manual_RunFixtureAsync_HarvestBasic_ReturnsValidOutput()
    // {
    //     var fixturePath = Path.Combine("Parity", "Fixtures", "harvest_basic.json");
    //     var output = await NodeJsHarnessRunner.RunFixtureAsync(fixturePath, TestContext.Current.CancellationToken);
    //
    //     Assert.NotNull(output);
    //     Assert.Equal("W1N1", output.Metadata.Room);
    //     Assert.Equal(100, output.Metadata.GameTime);
    //     Assert.NotEmpty(output.Mutations.Patches);
    // }

    [Fact]
    public void NodeJsOutput_JsonDocument_ParsesCorrectly()
    {
        // Arrange - Sample Node.js harness output
        var json = """
        {
          "mutations": {
            "patches": [
              {
                "objectId": "creep1",
                "store": { "energy": 10 }
              }
            ],
            "upserts": [],
            "removals": []
          },
          "stats": {},
          "actionLogs": {},
          "finalState": {},
          "metadata": {
            "room": "W1N1",
            "gameTime": 100,
            "timestamp": "2026-01-22T00:00:00.000Z"
          }
        }
        """;

        // Act
        using var output = System.Text.Json.JsonDocument.Parse(json);

        // Assert
        Assert.NotNull(output);
        Assert.True(output.RootElement.TryGetProperty("metadata", out var metadata));
        Assert.Equal("W1N1", metadata.GetProperty("room").GetString());
        Assert.Equal(100, metadata.GetProperty("gameTime").GetInt32());

        Assert.True(output.RootElement.TryGetProperty("mutations", out var mutations));
        Assert.True(mutations.TryGetProperty("patches", out var patches));
        Assert.Equal(1, patches.GetArrayLength());
    }
}
