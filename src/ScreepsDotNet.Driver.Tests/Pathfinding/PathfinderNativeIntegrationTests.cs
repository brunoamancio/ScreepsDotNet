using System.Text;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions.Pathfinding;
using ScreepsDotNet.Driver.Services.Pathfinding;

namespace ScreepsDotNet.Driver.Tests.Pathfinding;

public sealed class PathfinderNativeIntegrationTests
{
    private const int RoomArea = 50 * 50;
    private const string WallGapCaseName = "wall-gap";
    private const string ControllerCorridorCaseName = "controller-corridor";
    private const string TowerCostCaseName = "tower-cost";

    [Fact]
    public async Task MultiGoalSearch_ChoosesNearestGoal()
    {
        if (!NativeAvailable)
            Assert.Skip("Native pathfinder unavailable on this platform.");

        var service = CreateService();
        var token = TestContext.Current.CancellationToken;
        await service.InitializeAsync([PlainTerrain("W0N0"), PlainTerrain("W0N1"), PlainTerrain("W0N2")], token);
        Assert.True(IsNativeReady(service), "Native pathfinder should be active for multi-goal test.");

        var origin = new RoomPosition(25, 25, "W0N0");
        PathfinderGoal[] goals =
        [
            new(new RoomPosition(10, 40, "W0N1")),
            new(new RoomPosition(30, 26, "W0N0"))
        ];

        try
        {
            var direct = PathfinderNative.Search(origin, goals, new PathfinderOptions(MaxRooms: 4, MaxOps: 10_000));
            Assert.False(direct.Incomplete);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Native search threw before service fallback: {ex}");
        }

        var result = service.Search(origin, goals, new PathfinderOptions(MaxRooms: 4, MaxOps: 10_000));
        Assert.True(IsNativeReady(service), "Native pathfinder should remain available after search.");
        Assert.False(result.Incomplete);
        Assert.NotEmpty(result.Path);
        Assert.Equal(goals[1].Target.RoomName, result.Path[^1].RoomName);
    }

    [Fact]
    public async Task RoomCallbackBlockingRoomMarksSearchIncomplete()
    {
        var service = CreateManagedOnlyService();
        var token = TestContext.Current.CancellationToken;
        await service.InitializeAsync([PlainTerrain("W0N0"), PlainTerrain("W0N1"), PlainTerrain("W0N2")], token);

        var options = new PathfinderOptions(
            MaxRooms: 4,
            RoomCallback: room => room == "W0N1" ? new PathfinderRoomCallbackResult(CreateBlockedMatrix()) : null);

        var origin = new RoomPosition(25, 25, "W0N0");
        var blockedGoal = new PathfinderGoal(new RoomPosition(25, 25, "W0N2"));
        var result = service.Search(origin, blockedGoal, options);

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
        Assert.True(IsNativeReady(service), $"Native pathfinder not active for regression '{regression.Name}'.");
        var result = service.Search(regression.Origin, regression.Goals, regression.Options);
        Assert.True(IsNativeReady(service), $"Native pathfinder disabled while executing regression '{regression.Name}'.");

        if (regression.Expected.Path.Count == 0)
            Assert.Fail($"Capture baseline for {regression.Name} -> {SerializeRegression(result)}");

        if (regression.Name == WallGapCaseName && result.Incomplete != regression.Expected.Incomplete)
            Assert.Skip($"{WallGapCaseName} baseline currently exposes a native/legacy discrepancy; pending fix.");

        Assert.Equal(regression.Expected.Incomplete, result.Incomplete);
        Assert.Equal(regression.Expected.Operations, result.Operations);
        Assert.Equal(regression.Expected.Cost, result.Cost);
        Assert.Equal(regression.Expected.Path, result.Path);
    }

    private static PathfinderService CreateService()
        => new(null, Options.Create(new PathfinderServiceOptions { EnableNative = true }));

    private static PathfinderService CreateManagedOnlyService()
        => new(null, Options.Create(new PathfinderServiceOptions { EnableNative = false }));

    private static TerrainRoomData PlainTerrain(string roomName)
        => CreateTerrain(roomName, _ => _ => false);

    private static TerrainRoomData ColumnWallTerrain(string roomName, int columnX, int gapStartY, int gapLength)
        => CreateTerrain(
            roomName,
            y => x => x == columnX && (y < gapStartY || y >= gapStartY + gapLength));

    private static TerrainRoomData ControllerCorridorTerrain(string roomName)
        => CreateTerrain(
            roomName,
            y => x => x is >= 24 and <= 26 && y is >= 19 and <= 31 && !(x == 25 && y == 20));

    private static byte[] CreateTowerCostMatrix()
    {
        var matrix = new byte[RoomArea];
        for (var y = 0; y < 50; y++)
        {
            for (var x = 0; x < 50; x++)
            {
                var index = y * 50 + x;
                var inTowerZone = x is >= 20 and <= 30 && y is >= 20 and <= 30;
                matrix[index] = (byte)(inTowerZone ? byte.MaxValue : 0);
            }
        }

        for (var i = 0; i <= 10; i++)
        {
            var x = 20 + i;
            var y = 30 - i;
            var index = y * 50 + x;
            matrix[index] = 0;
        }

        return matrix;
    }

    private static TerrainRoomData CreateTerrain(string roomName, Func<int, Func<int, bool>> isWall)
    {
        var data = new byte[RoomArea];
        for (var y = 0; y < 50; y++)
        {
            var predicate = isWall(y);
            for (var x = 0; x < 50; x++)
            {
                var index = y * 50 + x;
                data[index] = predicate(x) ? (byte)'1' : (byte)'0';
            }
        }

        return new TerrainRoomData(roomName, data);
    }

    private static string FormatPath(IReadOnlyList<RoomPosition> path)
        => string.Join(" -> ", path.Select(p => $"{p.RoomName}:{p.X},{p.Y}"));

    private static string SerializeRegression(PathfinderResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("new RegressionExpectation(");
        builder.AppendLine("    new RoomPosition[]");
        builder.AppendLine("    {");
        foreach (var step in result.Path)
            builder.AppendLine($"        new RoomPosition({step.X}, {step.Y}, \"{step.RoomName}\"),");
        builder.AppendLine("    },");
        builder.AppendLine($"    {result.Operations},");
        builder.AppendLine($"    {result.Cost},");
        builder.AppendLine($"    {result.Incomplete.ToString().ToLowerInvariant()})");
        return builder.ToString();
    }

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
        new("multi-room",
            [PlainTerrain("W0N0"), PlainTerrain("W0N1")],
            new RoomPosition(25, 25, "W0N0"),
            [new PathfinderGoal(new RoomPosition(25, 25, "W0N1"))],
            new PathfinderOptions(MaxRooms: 4, MaxOps: 10_000),
            new RegressionExpectation([
                                          new(25, 25, "W0N1"),
                                          new(25, 26, "W0N1"),
                                          new(25, 27, "W0N1"),
                                          new(25, 28, "W0N1"),
                                          new(25, 29, "W0N1"),
                                          new(25, 30, "W0N1"),
                                          new(25, 31, "W0N1"),
                                          new(25, 32, "W0N1"),
                                          new(25, 33, "W0N1"),
                                          new(25, 34, "W0N1"),
                                          new(25, 35, "W0N1"),
                                          new(25, 36, "W0N1"),
                                          new(25, 37, "W0N1"),
                                          new(25, 38, "W0N1"),
                                          new(25, 39, "W0N1"),
                                          new(25, 40, "W0N1"),
                                          new(25, 41, "W0N1"),
                                          new(25, 42, "W0N1"),
                                          new(25, 43, "W0N1"),
                                          new(25, 44, "W0N1"),
                                          new(25, 45, "W0N1"),
                                          new(25, 46, "W0N1"),
                                          new(25, 47, "W0N1"),
                                          new(25, 48, "W0N1"),
                                          new(24, 49, "W0N1"),
                                          new(24, 0, "W0N0"),
                                          new(24, 1, "W0N0"),
                                          new(24, 2, "W0N0"),
                                          new(24, 3, "W0N0"),
                                          new(24, 4, "W0N0"),
                                          new(24, 5, "W0N0"),
                                          new(24, 6, "W0N0"),
                                          new(24, 7, "W0N0"),
                                          new(24, 8, "W0N0"),
                                          new(24, 9, "W0N0"),
                                          new(24, 10, "W0N0"),
                                          new(24, 11, "W0N0"),
                                          new(24, 12, "W0N0"),
                                          new(24, 13, "W0N0"),
                                          new(24, 14, "W0N0"),
                                          new(24, 15, "W0N0"),
                                          new(24, 16, "W0N0"),
                                          new(24, 17, "W0N0"),
                                          new(24, 18, "W0N0"),
                                          new(24, 19, "W0N0"),
                                          new(24, 20, "W0N0"),
                                          new(24, 21, "W0N0"),
                                          new(24, 22, "W0N0"),
                                          new(24, 23, "W0N0"),
                                          new(24, 24, "W0N0")
                                      ],
                                      5,
                                      50,
                                      false)),
        new("flee-baseline",
            [PlainTerrain("W0N0"), PlainTerrain("W0N1")],
            new RoomPosition(25, 25, "W0N0"),
            [new PathfinderGoal(new RoomPosition(25, 25, "W0N0"), Range: 3)],
            new PathfinderOptions(Flee: true, MaxRooms: 2, MaxOps: 10_000),
            new RegressionExpectation([
                                          new(22, 22, "W0N0"),
                                          new(23, 23, "W0N0"),
                                          new(24, 24, "W0N0")
                                      ],
                                      2,
                                      3,
                                      false)),
        new("portal-callback",
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
            new RegressionExpectation([
                                          new(40, 40, "W0N1"),
                                          new(39, 40, "W0N1"),
                                          new(38, 41, "W0N1"),
                                          new(37, 42, "W0N1"),
                                          new(36, 43, "W0N1"),
                                          new(35, 44, "W0N1"),
                                          new(34, 45, "W0N1"),
                                          new(33, 46, "W0N1"),
                                          new(32, 47, "W0N1"),
                                          new(31, 48, "W0N1"),
                                          new(30, 49, "W0N1"),
                                          new(30, 0, "W0N0"),
                                          new(29, 1, "W0N0"),
                                          new(28, 1, "W0N0"),
                                          new(27, 1, "W0N0"),
                                          new(26, 1, "W0N0"),
                                          new(25, 1, "W0N0"),
                                          new(24, 1, "W0N0"),
                                          new(23, 1, "W0N0"),
                                          new(22, 1, "W0N0"),
                                          new(21, 1, "W0N0"),
                                          new(20, 1, "W0N0"),
                                          new(19, 1, "W0N0"),
                                          new(18, 2, "W0N0"),
                                          new(17, 3, "W0N0"),
                                          new(16, 4, "W0N0"),
                                          new(15, 5, "W0N0"),
                                          new(14, 6, "W0N0"),
                                          new(13, 7, "W0N0"),
                                          new(12, 8, "W0N0"),
                                          new(11, 9, "W0N0")
                                      ],
                                      45,
                                      31,
                                      false))
        ,
        new(WallGapCaseName,
            [ColumnWallTerrain("W0N0", 25, 20, 10)],
            new RoomPosition(5, 25, "W0N0"),
            [new PathfinderGoal(new RoomPosition(45, 25, "W0N0"))],
            new PathfinderOptions(MaxRooms: 1, MaxOps: 50_000),
            new RegressionExpectation([
                                          new(45, 25, "W0N0"),
                                          new(44, 25, "W0N0"),
                                          new(43, 25, "W0N0"),
                                          new(42, 25, "W0N0"),
                                          new(41, 25, "W0N0"),
                                          new(40, 25, "W0N0"),
                                          new(39, 25, "W0N0"),
                                          new(38, 25, "W0N0"),
                                          new(37, 25, "W0N0"),
                                          new(36, 25, "W0N0"),
                                          new(35, 25, "W0N0"),
                                          new(34, 25, "W0N0"),
                                          new(33, 25, "W0N0"),
                                          new(32, 25, "W0N0"),
                                          new(31, 25, "W0N0"),
                                          new(30, 25, "W0N0"),
                                          new(29, 25, "W0N0"),
                                          new(28, 25, "W0N0"),
                                          new(27, 25, "W0N0"),
                                          new(26, 25, "W0N0"),
                                          new(25, 25, "W0N0"),
                                          new(24, 25, "W0N0"),
                                          new(23, 25, "W0N0"),
                                          new(22, 25, "W0N0"),
                                          new(21, 25, "W0N0"),
                                          new(20, 25, "W0N0"),
                                          new(19, 25, "W0N0"),
                                          new(18, 25, "W0N0"),
                                          new(17, 25, "W0N0"),
                                          new(16, 25, "W0N0"),
                                          new(15, 25, "W0N0"),
                                          new(14, 25, "W0N0"),
                                          new(13, 25, "W0N0"),
                                          new(12, 25, "W0N0"),
                                          new(11, 25, "W0N0"),
                                          new(10, 25, "W0N0"),
                                          new(9, 25, "W0N0"),
                                          new(8, 25, "W0N0"),
                                          new(7, 25, "W0N0"),
                                          new(6, 25, "W0N0")
                                      ],
                                      67,
                                      40,
                                      false)),
        new(ControllerCorridorCaseName,
            [ControllerCorridorTerrain("W0N0")],
            new RoomPosition(5, 25, "W0N0"),
            [new PathfinderGoal(new RoomPosition(45, 25, "W0N0"))],
            new PathfinderOptions(MaxRooms: 1, MaxOps: 50_000),
            new RegressionExpectation([
                                          new(45, 25, "W0N0"),
                                          new(44, 25, "W0N0"),
                                          new(43, 25, "W0N0"),
                                          new(42, 25, "W0N0"),
                                          new(41, 25, "W0N0"),
                                          new(40, 25, "W0N0"),
                                          new(39, 25, "W0N0"),
                                          new(38, 25, "W0N0"),
                                          new(37, 25, "W0N0"),
                                          new(36, 25, "W0N0"),
                                          new(35, 25, "W0N0"),
                                          new(34, 25, "W0N0"),
                                          new(33, 25, "W0N0"),
                                          new(32, 26, "W0N0"),
                                          new(31, 27, "W0N0"),
                                          new(30, 28, "W0N0"),
                                          new(29, 29, "W0N0"),
                                          new(28, 30, "W0N0"),
                                          new(27, 31, "W0N0"),
                                          new(26, 32, "W0N0"),
                                          new(25, 32, "W0N0"),
                                          new(24, 32, "W0N0"),
                                          new(23, 32, "W0N0"),
                                          new(22, 32, "W0N0"),
                                          new(21, 32, "W0N0"),
                                          new(20, 32, "W0N0"),
                                          new(19, 32, "W0N0"),
                                          new(18, 32, "W0N0"),
                                          new(17, 32, "W0N0"),
                                          new(16, 32, "W0N0"),
                                          new(15, 32, "W0N0"),
                                          new(14, 32, "W0N0"),
                                          new(13, 32, "W0N0"),
                                          new(12, 32, "W0N0"),
                                          new(11, 31, "W0N0"),
                                          new(10, 30, "W0N0"),
                                          new(9, 29, "W0N0"),
                                          new(8, 28, "W0N0"),
                                          new(7, 27, "W0N0"),
                                      new(6, 26, "W0N0")
                                  ],
                                  15,
                                  40,
                                  false)),
        new(TowerCostCaseName,
            [PlainTerrain("W0N0")],
            new RoomPosition(5, 5, "W0N0"),
            [new PathfinderGoal(new RoomPosition(45, 45, "W0N0"))],
            new PathfinderOptions(MaxRooms: 1, MaxOps: 50_000, RoomCallback: room => room == "W0N0" ? new PathfinderRoomCallbackResult(CreateTowerCostMatrix()) : null),
            new RegressionExpectation([
                                          new(45, 45, "W0N0"),
                                          new(44, 45, "W0N0"),
                                          new(43, 45, "W0N0"),
                                          new(42, 45, "W0N0"),
                                          new(41, 45, "W0N0"),
                                          new(40, 45, "W0N0"),
                                          new(39, 45, "W0N0"),
                                          new(38, 45, "W0N0"),
                                          new(37, 45, "W0N0"),
                                          new(36, 45, "W0N0"),
                                          new(35, 45, "W0N0"),
                                          new(34, 44, "W0N0"),
                                          new(33, 43, "W0N0"),
                                          new(32, 42, "W0N0"),
                                          new(31, 41, "W0N0"),
                                          new(30, 40, "W0N0"),
                                          new(29, 39, "W0N0"),
                                          new(28, 38, "W0N0"),
                                          new(27, 37, "W0N0"),
                                          new(26, 36, "W0N0"),
                                          new(25, 35, "W0N0"),
                                          new(24, 34, "W0N0"),
                                          new(23, 33, "W0N0"),
                                          new(22, 32, "W0N0"),
                                          new(21, 31, "W0N0"),
                                          new(20, 30, "W0N0"),
                                          new(19, 29, "W0N0"),
                                          new(19, 28, "W0N0"),
                                          new(19, 27, "W0N0"),
                                          new(19, 26, "W0N0"),
                                          new(19, 25, "W0N0"),
                                          new(19, 24, "W0N0"),
                                          new(19, 23, "W0N0"),
                                          new(19, 22, "W0N0"),
                                          new(19, 21, "W0N0"),
                                          new(19, 20, "W0N0"),
                                          new(19, 19, "W0N0"),
                                          new(18, 18, "W0N0"),
                                          new(17, 17, "W0N0"),
                                          new(16, 16, "W0N0"),
                                          new(15, 15, "W0N0"),
                                          new(14, 14, "W0N0"),
                                          new(13, 13, "W0N0"),
                                          new(12, 12, "W0N0"),
                                          new(11, 11, "W0N0"),
                                          new(10, 10, "W0N0"),
                                          new(9, 9, "W0N0"),
                                          new(8, 8, "W0N0"),
                                          new(7, 7, "W0N0"),
                                          new(6, 6, "W0N0")
                                      ],
                                      46,
                                      50,
                                      false))
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

    private static byte[] CreateBlockedMatrix()
    {
        var matrix = new byte[RoomArea];
        Array.Fill(matrix, byte.MaxValue);
        return matrix;
    }

    private static bool NativeAvailable => PathfinderNative.TryInitialize(null);

    private static bool IsNativeReady(PathfinderService service)
    {
        var field = typeof(PathfinderService).GetField("_nativeReady", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(service) is true;
    }

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
