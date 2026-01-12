using ScreepsDotNet.Driver.Abstractions.Pathfinding;
using ScreepsDotNet.Driver.Services.Pathfinding;

namespace ScreepsDotNet.Driver.Tests.Pathfinding;

public sealed class PathfinderServiceTests
{
    [Fact]
    public async Task Search_ReturnsSimplePath()
    {
        var service = new PathfinderService();
        await service.InitializeAsync([new TerrainRoomData("W1N1", CreatePlainTerrain())]);

        var origin = new RoomPosition(25, 25, "W1N1");
        var goal = new PathfinderGoal(new RoomPosition(30, 25, "W1N1"));

        var result = service.Search(origin, goal, new PathfinderOptions());

        Assert.False(result.Incomplete);
        Assert.True(result.Path.Count > 0);
        var last = result.Path[^1];
        Assert.Equal("W1N1", last.RoomName);
        Assert.Equal(30, last.X);
        Assert.Equal(25, last.Y);
    }

    [Fact]
    public async Task Search_DifferentRooms_ReturnsIncomplete()
    {
        var service = new PathfinderService();
        await service.InitializeAsync([new TerrainRoomData("W1N1", CreatePlainTerrain())]);

        var origin = new RoomPosition(10, 10, "W1N1");
        var goal = new PathfinderGoal(new RoomPosition(10, 10, "W2N2"));

        var result = service.Search(origin, goal, new PathfinderOptions());

        Assert.True(result.Incomplete);
    }

    private static byte[] CreatePlainTerrain()
    {
        var data = new byte[50 * 50];
        Array.Fill(data, (byte)'0');
        return data;
    }
}
