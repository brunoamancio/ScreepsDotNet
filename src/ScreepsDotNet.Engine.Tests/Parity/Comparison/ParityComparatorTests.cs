namespace ScreepsDotNet.Engine.Tests.Parity.Comparison;

using System.Text.Json;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Tests for parity comparison engine
/// </summary>
public sealed class ParityComparatorTests
{
    [Fact]
    public void Compare_IdenticalOutputs_ReturnsNoDivergences()
    {
        // Arrange - Create identical outputs
        var dotnetOutput = CreateDotNetOutput(energyHarvested: 2, sourceEnergy: 2998, creepEnergy: 2);

        var nodeJson = """
        {
          "mutations": {
            "patches": [
              {
                "objectId": "source1",
                "energy": 2998
              },
              {
                "objectId": "creep1",
                "store": { "energy": 2 }
              }
            ],
            "upserts": [],
            "removals": []
          },
          "stats": {
            "user1.energyHarvested": 2
          },
          "actionLogs": {},
          "finalState": {},
          "metadata": {
            "room": "W1N1",
            "gameTime": 100,
            "timestamp": "2026-01-22T00:00:00Z"
          }
        }
        """;

        using var nodeOutput = JsonDocument.Parse(nodeJson);

        // Act
        var result = ParityComparator.Compare(dotnetOutput, nodeOutput);

        // Assert
        Assert.False(result.HasDivergences);
        Assert.Empty(result.Divergences);
    }

    [Fact]
    public void Compare_DifferentEnergy_ReturnsDivergence()
    {
        // Arrange - .NET harvested 2, Node.js harvested 3
        var dotnetOutput = CreateDotNetOutput(energyHarvested: 2, sourceEnergy: 2998, creepEnergy: 2);

        var nodeJson = """
        {
          "mutations": {
            "patches": [
              {
                "objectId": "source1",
                "energy": 2997
              },
              {
                "objectId": "creep1",
                "store": { "energy": 3 }
              }
            ],
            "upserts": [],
            "removals": []
          },
          "stats": {
            "user1.energyHarvested": 3
          }
        }
        """;

        using var nodeOutput = JsonDocument.Parse(nodeJson);

        // Act
        var result = ParityComparator.Compare(dotnetOutput, nodeOutput);

        // Assert
        Assert.True(result.HasDivergences);
        Assert.Equal(3, result.Divergences.Count); // source energy, creep energy, stats

        var sourceEnergyDivergence = result.Divergences.First(d => d.Path == "mutations.patches[source1].energy");
        Assert.Equal(2997, sourceEnergyDivergence.NodeValue);
        Assert.Equal(2998, sourceEnergyDivergence.DotNetValue);

        var creepEnergyDivergence = result.Divergences.First(d => d.Path == "mutations.patches[creep1].store.energy");
        Assert.Equal(3, creepEnergyDivergence.NodeValue);
        Assert.Equal(2, creepEnergyDivergence.DotNetValue);

        var statDivergence = result.Divergences.First(d => d.Path == "stats.user1.energyHarvested");
        Assert.Equal(3, statDivergence.NodeValue);
        Assert.Equal(2, statDivergence.DotNetValue);
    }

    [Fact]
    public void Compare_MissingPatchInDotNet_ReturnsDivergence()
    {
        // Arrange - Node.js has patch for creep2, .NET doesn't
        var dotnetOutput = CreateDotNetOutput(energyHarvested: 2, sourceEnergy: 2998, creepEnergy: 2);

        var nodeJson = """
        {
          "mutations": {
            "patches": [
              {
                "objectId": "source1",
                "energy": 2998
              },
              {
                "objectId": "creep1",
                "store": { "energy": 2 }
              },
              {
                "objectId": "creep2",
                "store": { "energy": 5 }
              }
            ]
          },
          "stats": {
            "user1.energyHarvested": 2
          }
        }
        """;

        using var nodeOutput = JsonDocument.Parse(nodeJson);

        // Act
        var result = ParityComparator.Compare(dotnetOutput, nodeOutput);

        // Assert
        Assert.True(result.HasDivergences);
        var missingPatch = result.Divergences.First(d => d.Path == "mutations.patches[creep2]");
        Assert.Equal("creep2", missingPatch.NodeValue);
        Assert.Null(missingPatch.DotNetValue);
        Assert.Equal(DivergenceCategory.Mutation, missingPatch.Category);
    }

    [Fact]
    public void DivergenceReporter_FormatReport_ProducesReadableOutput()
    {
        // Arrange
        var dotnetOutput = CreateDotNetOutput(energyHarvested: 2, sourceEnergy: 2998, creepEnergy: 2);

        var nodeJson = """
        {
          "mutations": {
            "patches": [
              {
                "objectId": "source1",
                "energy": 2997
              },
              {
                "objectId": "creep1",
                "store": { "energy": 3 }
              }
            ]
          },
          "stats": {
            "user1.energyHarvested": 3
          }
        }
        """;

        using var nodeOutput = JsonDocument.Parse(nodeJson);
        var result = ParityComparator.Compare(dotnetOutput, nodeOutput);

        // Act
        var report = DivergenceReporter.FormatReport(result, "harvest_basic.json");

        // Assert
        Assert.Contains("❌ Parity Test Failed: harvest_basic.json", report);
        Assert.Contains("Divergences (3)", report);
        Assert.Contains("mutations.patches[source1].energy", report);
        Assert.Contains("Node.js: 2997", report);
        Assert.Contains(".NET:    2998", report);
    }

    [Fact]
    public void DivergenceReporter_FormatSummary_ShowsCategoryCounts()
    {
        // Arrange
        var dotnetOutput = CreateDotNetOutput(energyHarvested: 2, sourceEnergy: 2998, creepEnergy: 2);

        var nodeJson = """
        {
          "mutations": {
            "patches": [
              {
                "objectId": "source1",
                "energy": 2997
              },
              {
                "objectId": "creep1",
                "store": { "energy": 3 }
              }
            ]
          },
          "stats": {
            "user1.energyHarvested": 3
          }
        }
        """;

        using var nodeOutput = JsonDocument.Parse(nodeJson);
        var result = ParityComparator.Compare(dotnetOutput, nodeOutput);

        // Act
        var summary = DivergenceReporter.FormatSummary(result);

        // Assert
        Assert.Contains("❌ 3 divergence(s)", summary);
        Assert.Contains("Mutation: 2", summary);
        Assert.Contains("Stats: 1", summary);
    }

    private static ParityTestOutput CreateDotNetOutput(int energyHarvested, int sourceEnergy, int creepEnergy)
    {
        var writer = new CapturingMutationWriter();
        var sink = new CapturingStatsSink();

        // Simulate mutations
        writer.Patch("source1", new RoomObjectPatchPayload { Energy = sourceEnergy });
        writer.Patch("creep1", new RoomObjectPatchPayload
        {
            Store = new Dictionary<string, int>(StringComparer.Ordinal) { ["energy"] = creepEnergy }
        });

        // Simulate stats
        sink.IncrementEnergyHarvested("user1", energyHarvested);

        return new ParityTestOutput(
            writer,
            sink,
            new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal)
        );
    }
}
