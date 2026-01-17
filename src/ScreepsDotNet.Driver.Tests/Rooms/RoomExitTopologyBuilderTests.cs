namespace ScreepsDotNet.Driver.Tests.Rooms;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Driver.Services.Rooms;
using Xunit;

public sealed class RoomExitTopologyBuilderTests
{
    [Fact]
    public void Build_ComputesDirectionalCountsAndAccessibility()
    {
        var rooms = new[] { "E0S0", "E0S1" };
        var terrain = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["E0S0"] = BuildTerrain((x, y) => y == 0 && x < 10),
            ["E0S1"] = BuildTerrain((_, _) => false)
        };

        var result = RoomExitTopologyBuilder.Build(rooms, terrain);

        Assert.True(result.TryGetValue("E0S0", out var topology));
        Assert.NotNull(topology);
        Assert.Equal(40, topology!.Top?.ExitCount);
        Assert.False(topology.Top!.TargetAccessible);
        Assert.Equal("E0N0", topology.Top.TargetRoomName);

        Assert.Equal(50, topology.Bottom?.ExitCount);
        Assert.True(topology.Bottom!.TargetAccessible);
        Assert.Equal("E0S1", topology.Bottom.TargetRoomName);
    }

    private static string BuildTerrain(Func<int, int, bool> isWall)
    {
        var chars = new char[50 * 50];
        for (var y = 0; y < 50; y++) {
            for (var x = 0; x < 50; x++) {
                var index = (y * 50) + x;
                chars[index] = isWall(x, y) ? '1' : '0';
            }
        }

        return new string(chars);
    }
}
