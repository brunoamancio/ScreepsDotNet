using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions.Pathfinding;
using ScreepsDotNet.Driver.Services.Pathfinding;

namespace ScreepsDotNet.Driver.Tests.Pathfinding;

public sealed class PathfinderNativeIntegrationTests
{
    private const int RoomArea = 50 * 50;

    [Fact]
    public async Task MultiGoalSearch_ChoosesNearestGoal()
    {
        if (!NativeAvailable)
            Assert.Skip("Native pathfinder unavailable on this platform.");

        var service = CreateService();
        var token = TestContext.Current.CancellationToken;
        await service.InitializeAsync([PlainTerrain("W0N0"), PlainTerrain("W0N1")], token);

        var origin = new RoomPosition(25, 25, "W0N0");
        PathfinderGoal[] goals =
        [
            new(new RoomPosition(10, 40, "W0N1")),
            new(new RoomPosition(30, 26, "W0N0"))
        ];

        var result = service.Search(origin, goals, new PathfinderOptions(MaxRooms: 4, MaxOps: 10_000));

        Assert.False(result.Incomplete);
        Assert.NotEmpty(result.Path);
        Assert.Equal(goals[1].Target.RoomName, result.Path[^1].RoomName);
    }

    [Fact]
    public async Task RoomCallbackBlockingRoomMarksSearchIncomplete()
    {
        if (!NativeAvailable)
            Assert.Skip("Native pathfinder unavailable on this platform.");

        var service = CreateService();
        var token = TestContext.Current.CancellationToken;
        await service.InitializeAsync([PlainTerrain("W0N0"), PlainTerrain("W0N1")], token);

        var options = new PathfinderOptions(
            MaxRooms: 4,
            RoomCallback: room => room == "W0N1" ? new PathfinderRoomCallbackResult(null, BlockRoom: true) : null);

        var result = service.Search(
            new RoomPosition(25, 25, "W0N0"),
            new PathfinderGoal(new RoomPosition(25, 25, "W0N1")),
            options);

        Assert.True(result.Incomplete);
    }

    [Theory]
    [MemberData(nameof(RegressionCaseData))]
    public async Task NativePathfinderMatchesRecordedBaseline(string caseName)
    {
        if (!NativeAvailable)
            Assert.Skip("Native pathfinder unavailable on this platform.");

        var regression = RegressionCaseMap[caseName];
        var service = CreateService();
        var token = TestContext.Current.CancellationToken;
        await service.InitializeAsync(regression.Rooms, token);
        var result = service.Search(regression.Origin, regression.Goals, regression.Options);

        if (regression.Expected.Path.Count == 0)
            Assert.Fail($"Capture baseline for {regression.Name} -> Path: {FormatPath(result.Path)} Ops={result.Operations} Cost={result.Cost} Incomplete={result.Incomplete}");

        Assert.Equal(regression.Expected.Incomplete, result.Incomplete);
        Assert.Equal(regression.Expected.Operations, result.Operations);
        Assert.Equal(regression.Expected.Cost, result.Cost);
        Assert.Equal(regression.Expected.Path, result.Path);
    }

    private static PathfinderService CreateService()
        => new(null, Options.Create(new PathfinderServiceOptions { EnableNative = true }));

    private static TerrainRoomData PlainTerrain(string roomName)
    {
        var data = new byte[RoomArea];
        Array.Fill(data, (byte)'0');
        return new TerrainRoomData(roomName, data);
    }

    private static string FormatPath(IReadOnlyList<RoomPosition> path)
        => string.Join(" -> ", path.Select(p => $"{p.RoomName}:{p.X},{p.Y}"));

    public static TheoryData<string> RegressionCaseData
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var regression in RegressionCases)
                data.Add(regression.Name);
            return data;
        }
    }

    private static readonly PathfinderRegressionCase[] RegressionCases =
    [
        new(
            "multi-room",
            [PlainTerrain("W0N0"), PlainTerrain("W0N1")],
            new RoomPosition(25, 25, "W0N0"),
            [new PathfinderGoal(new RoomPosition(25, 25, "W0N1"))],
            new PathfinderOptions(MaxRooms: 4, MaxOps: 10_000),
            new RegressionExpectation([], 0, 0, false)),
        new(
            "flee-baseline",
            [PlainTerrain("W0N0"), PlainTerrain("W0N1")],
            new RoomPosition(25, 25, "W0N0"),
            [new PathfinderGoal(new RoomPosition(25, 25, "W0N0"), Range: 3)],
            new PathfinderOptions(Flee: true, MaxRooms: 2, MaxOps: 10_000),
            new RegressionExpectation([], 0, 0, false)),
        new(
            "portal-callback",
            [PlainTerrain("W0N0"), PlainTerrain("W0N1")],
            new RoomPosition(10, 10, "W0N0"),
            [new PathfinderGoal(new RoomPosition(40, 40, "W0N1"))],
            new PathfinderOptions(
                                  MaxRooms: 4,
                                  MaxOps: 20_000,
                                  RoomCallback: room => room switch
                                  {
                                      "W0N0" => new PathfinderRoomCallbackResult(CreatePortalMatrix(0)),
                                      "W0N1" => new PathfinderRoomCallbackResult(CreatePortalMatrix(49)),
                                      _ => null
                                  }),
            new RegressionExpectation([], 0, 0, false))
    ];

    private static readonly IReadOnlyDictionary<string, PathfinderRegressionCase> RegressionCaseMap =
        RegressionCases.ToDictionary(caseDef => caseDef.Name);

    private static byte[] CreatePortalMatrix(int edgeY)
    {
        var matrix = new byte[RoomArea];
        for (var y = 0; y < 50; y++)
        {
            for (var x = 0; x < 50; x++)
            {
                var index = y * 50 + x;
                matrix[index] = y == edgeY ? (x == 25 ? (byte)0 : (byte)255) : (byte)0;
            }
        }

        return matrix;
    }

    private static bool NativeAvailable => PathfinderNative.TryInitialize(null);

    public sealed record PathfinderRegressionCase(
        string Name,
        TerrainRoomData[] Rooms,
        RoomPosition Origin,
        PathfinderGoal[] Goals,
        PathfinderOptions Options,
        RegressionExpectation Expected);

    public sealed record RegressionExpectation(
        IReadOnlyList<RoomPosition> Path,
        int Operations,
        int Cost,
        bool Incomplete);
}
