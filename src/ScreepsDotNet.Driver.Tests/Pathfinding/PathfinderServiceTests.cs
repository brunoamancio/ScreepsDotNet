using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions.Pathfinding;
using ScreepsDotNet.Driver.Services.Pathfinding;

namespace ScreepsDotNet.Driver.Tests.Pathfinding;

public sealed class PathfinderServiceTests
{
    private const int RoomArea = 50 * 50;

    [Fact]
    public async Task Search_ReturnsSimplePath()
    {
        var service = CreateManagedOnlyService();
        var token = TestContext.Current.CancellationToken;
        await service.InitializeAsync([new TerrainRoomData("W1N1", CreatePlainTerrain())], token);

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
        var service = CreateManagedOnlyService();
        var token = TestContext.Current.CancellationToken;
        await service.InitializeAsync([new TerrainRoomData("W1N1", CreatePlainTerrain())], token);

        var origin = new RoomPosition(10, 10, "W1N1");
        var goal = new PathfinderGoal(new RoomPosition(10, 10, "W2N2"));

        var result = service.Search(origin, goal, new PathfinderOptions());

        Assert.True(result.Incomplete);
    }

    [Fact]
    public async Task ManagedFallback_RespectsRoomCallbackCostMatrix()
    {
        var service = CreateManagedOnlyService();
        var token = TestContext.Current.CancellationToken;
        await service.InitializeAsync([new TerrainRoomData("W1N1", CreatePlainTerrain())], token);

        var columnWithGaps = CreateBlockedColumnWithGaps(25, 3);
        var options = new PathfinderOptions(
            MaxOps: 10_000,
            RoomCallback: _ => new PathfinderRoomCallbackResult(columnWithGaps));

        var origin = new RoomPosition(10, 25, "W1N1");
        var goal = new PathfinderGoal(new RoomPosition(40, 25, "W1N1"));

        var result = service.Search(origin, goal, options);

        Assert.False(result.Incomplete);
        Assert.NotEmpty(result.Path);
        Assert.DoesNotContain(result.Path, pos => pos.X == 25 && pos.Y is >= 3 and <= 46);
        Assert.Contains(result.Path, pos => pos.X == 25 && (pos.Y < 3 || pos.Y >= 47));
    }

    [Fact]
    public async Task ManagedFallback_RoomCallbackBlockReturnsIncomplete()
    {
        var service = CreateManagedOnlyService();
        var token = TestContext.Current.CancellationToken;
        await service.InitializeAsync([new TerrainRoomData("W1N1", CreatePlainTerrain())], token);

        var origin = new RoomPosition(10, 10, "W1N1");
        var goal = new PathfinderGoal(new RoomPosition(40, 40, "W1N1"));
        var options = new PathfinderOptions(RoomCallback: _ => new PathfinderRoomCallbackResult(null, BlockRoom: true));

        var result = service.Search(origin, goal, options);

        Assert.True(result.Incomplete);
    }

    [Fact]
    public async Task ManagedFallback_FleeMovesOutsideRequestedRange()
    {
        var service = CreateManagedOnlyService();
        var token = TestContext.Current.CancellationToken;
        await service.InitializeAsync([new TerrainRoomData("W1N1", CreatePlainTerrain())], token);

        var origin = new RoomPosition(25, 25, "W1N1");
        var goal = new PathfinderGoal(new RoomPosition(25, 25, "W1N1"), Range: 5);
        var options = new PathfinderOptions(Flee: true, MaxOps: 5_000);

        var result = service.Search(origin, goal, options);

        Assert.False(result.Incomplete);
        Assert.NotEmpty(result.Path);
        var last = result.Path[^1];
        var distance = Math.Max(Math.Abs(last.X - goal.Target.X), Math.Abs(last.Y - goal.Target.Y));
        Assert.True(distance >= 5);
    }

    private static byte[] CreatePlainTerrain()
    {
        var data = new byte[RoomArea];
        Array.Fill(data, (byte)'0');
        return data;
    }

    private static byte[] CreateBlockedColumnWithGaps(int column, int gapPadding)
    {
        var matrix = new byte[RoomArea];
        for (var y = 0; y < 50; y++)
        {
            if (y >= gapPadding && y < 50 - gapPadding)
                matrix[y * 50 + column] = byte.MaxValue;
        }

        return matrix;
    }

    private static PathfinderService CreateManagedOnlyService()
        => new(null, Options.Create(new PathfinderServiceOptions { EnableNative = false }));
}
