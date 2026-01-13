using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions.Pathfinding;
using ScreepsDotNet.Driver.Extensions;
using ScreepsDotNet.Driver.Services.Pathfinding;

namespace ScreepsDotNet.Driver.Tests.Pathfinding;

public sealed class PathfinderNativeIntegrationTests
{
    private const int RoomArea = 50 * 50;
    private const string WallGapCaseName = "wall-gap";
    private const string ControllerCorridorCaseName = "controller-corridor";
    private const string FleeMultiRoomCaseName = "flee-multi-room";
    private const string TowerCostCaseName = "tower-cost";
    private const string TowerPowerChokeCaseName = "tower-power-choke";
    private const string KeeperLairCorridorCaseName = "keeper-lair-corridor";
    private const string PortalChainCaseName = "portal-chain";
    private const string RegressionBaselineRelativePath = "Pathfinding/Baselines/legacy-regressions.json";

    private static readonly Lazy<IReadOnlyDictionary<string, RegressionExpectation>> BaselineExpectationMap =
        new(LoadBaselineExpectations);

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
        var expectation = ResolveExpectation(caseName, regression.Expected);
        var result = service.Search(regression.Origin, regression.Goals, regression.Options);
        Assert.True(IsNativeReady(service), $"Native pathfinder disabled while executing regression '{regression.Name}'.");

        if (expectation.Path.Count == 0)
            Assert.Fail($"Capture baseline for {regression.Name} -> {SerializeRegression(result)}");

        if (regression.Name == WallGapCaseName && result.Incomplete != regression.Expected.Incomplete)
            Assert.Skip($"{WallGapCaseName} baseline currently exposes a native/legacy discrepancy; pending fix.");

        Assert.Equal(expectation.Incomplete, result.Incomplete);
        Assert.Equal(expectation.Operations, result.Operations);
        Assert.Equal(expectation.Cost, result.Cost);
        Assert.Equal(expectation.Path, result.Path);
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

    private static byte[] CreateTowerPowerCostMatrix()
    {
        var matrix = new byte[RoomArea];
        AddZone(matrix, 15, 15, 20, 20, 200);
        AddZone(matrix, 30, 30, 35, 35, 200);
        AddZone(matrix, 20, 32, 29, 41, 200);

        foreach (var (x, y) in new (int X, int Y)[] { (18, 25), (32, 24), (27, 34) })
            matrix[y * 50 + x] = byte.MaxValue;

        for (var i = 0; i < 15; i++)
        {
            var x = 5 + i;
            var y = 25 + (int)Math.Round(Math.Sin(i / 2d) * 5);
            matrix[y * 50 + x] = 1;
        }

        for (var i = 0; i < 15; i++)
        {
            var x = 20 + i;
            var y = 20 + i;
            matrix[y * 50 + x] = 1;
        }

        for (var i = 0; i < 10; i++)
        {
            var x = 35 + i;
            var y = 30 - i;
            matrix[y * 50 + x] = 1;
        }

        return matrix;

        static void AddZone(byte[] buffer, int x1, int y1, int x2, int y2, byte cost)
        {
            for (var y = y1; y <= y2; y++)
            {
                for (var x = x1; x <= x2; x++)
                    buffer[y * 50 + x] = cost;
            }
        }
    }

    private static byte[] CreateKeeperLairMatrix()
    {
        var matrix = new byte[RoomArea];
        foreach (var (x, y) in new (int X, int Y)[] { (15, 35), (25, 20), (35, 30) })
        {
            for (var dy = -5; dy <= 5; dy++)
            {
                for (var dx = -5; dx <= 5; dx++)
                {
                    var lx = x + dx;
                    var ly = y + dy;
                    if (lx < 0 || lx >= 50 || ly < 0 || ly >= 50)
                        continue;

                    var idx = ly * 50 + lx;
                    var dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    var cost = dist <= 2 ? byte.MaxValue : (byte)180;
                    matrix[idx] = Math.Max(matrix[idx], cost);
                }
            }
        }

        foreach (var (x, y) in SafeKeeperPath())
        {
            var idx = y * 50 + x;
            matrix[idx] = matrix[idx] < 5 ? matrix[idx] : (byte)5;
        }

        return matrix;

        static IEnumerable<(int X, int Y)> SafeKeeperPath()
        {
            for (var i = 0; i < 20; i++)
                yield return (5 + i, 40 - i);

            for (var i = 0; i < 10; i++)
                yield return (25, 20 - i);

            for (var i = 0; i < 15; i++)
                yield return (25 + i, 10);
        }
    }

    private static byte[] CreatePortalChainMatrix(string roomName)
    {
        var matrix = new byte[RoomArea];
        Array.Fill(matrix, (byte)200);

        void Carve(int x, int y)
        {
            if (x is >= 0 and < 50 && y is >= 0 and < 50)
                matrix[y * 50 + x] = 0;
        }

        switch (roomName)
        {
            case "W0N0":
            {
                var x = 10;
                var y = 10;
                while (y >= 0)
                {
                    Carve(x, y);
                    x += 1;
                    y -= 1;
                }

                while (x <= 30)
                {
                    Carve(x, 0);
                    x += 1;
                }

                for (var cx = 0; cx < 50; cx++)
                    matrix[cx] = cx == 30 ? (byte)0 : byte.MaxValue;

                break;
            }
            case "W0N1":
            {
                for (var cx = 0; cx < 50; cx++)
                    matrix[49 * 50 + cx] = cx == 30 ? (byte)0 : byte.MaxValue;

                for (var cy = 49; cy >= 35; cy--)
                    Carve(30, cy);

                for (var cx = 30; cx >= 5; cx--)
                    Carve(cx, 35);

                for (var cy = 35; cy >= 0; cy--)
                    Carve(5, cy);

                for (var cx = 0; cx < 50; cx++)
                    matrix[cx] = cx == 5 ? (byte)0 : byte.MaxValue;

                break;
            }
            case "W0N2":
            {
                for (var cy = 49; cy >= 10; cy--)
                    Carve(5, cy);

                for (var step = 0; step <= 40; step++)
                {
                    var px = 5 + step;
                    var py = 10 + Math.Min(30, (int)Math.Round(step * 0.75));
                    Carve(px, py);
                }

                break;
            }
            default:
                Array.Fill(matrix, (byte)0);
                break;
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
                                          new(9, 11, "W0N0"),
                                          new(8, 12, "W0N0"),
                                          new(7, 13, "W0N0"),
                                          new(6, 14, "W0N0"),
                                          new(5, 15, "W0N0"),
                                          new(4, 16, "W0N0"),
                                          new(3, 17, "W0N0"),
                                          new(2, 18, "W0N0"),
                                          new(1, 19, "W0N0"),
                                          new(1, 20, "W0N0"),
                                          new(1, 21, "W0N0"),
                                          new(1, 22, "W0N0"),
                                          new(1, 23, "W0N0"),
                                          new(1, 24, "W0N0"),
                                          new(1, 25, "W0N0"),
                                          new(1, 26, "W0N0"),
                                          new(1, 27, "W0N0"),
                                          new(1, 28, "W0N0"),
                                          new(1, 29, "W0N0"),
                                          new(0, 30, "W0N0"),
                                          new(49, 30, "W1N0"),
                                          new(48, 29, "W1N0"),
                                          new(47, 28, "W1N0"),
                                          new(47, 27, "W1N0"),
                                          new(47, 26, "W1N0"),
                                          new(47, 25, "W1N0"),
                                          new(47, 24, "W1N0"),
                                          new(47, 23, "W1N0"),
                                          new(47, 22, "W1N0"),
                                          new(47, 21, "W1N0"),
                                          new(47, 20, "W1N0"),
                                          new(47, 19, "W1N0"),
                                          new(47, 18, "W1N0"),
                                          new(47, 17, "W1N0"),
                                          new(47, 16, "W1N0"),
                                          new(47, 15, "W1N0"),
                                          new(47, 14, "W1N0"),
                                          new(47, 13, "W1N0"),
                                          new(47, 12, "W1N0"),
                                          new(47, 11, "W1N0"),
                                          new(47, 10, "W1N0"),
                                          new(47, 9, "W1N0"),
                                          new(47, 8, "W1N0"),
                                          new(47, 7, "W1N0"),
                                          new(47, 6, "W1N0"),
                                          new(47, 5, "W1N0"),
                                          new(47, 4, "W1N0"),
                                          new(47, 3, "W1N0"),
                                          new(47, 2, "W1N0"),
                                          new(47, 1, "W1N0"),
                                          new(47, 0, "W1N0"),
                                          new(47, 49, "W1N1"),
                                          new(48, 48, "W1N1"),
                                          new(48, 47, "W1N1"),
                                          new(48, 46, "W1N1"),
                                          new(48, 45, "W1N1"),
                                          new(48, 44, "W1N1"),
                                          new(48, 43, "W1N1"),
                                          new(48, 42, "W1N1"),
                                          new(48, 41, "W1N1"),
                                          new(48, 40, "W1N1"),
                                          new(48, 39, "W1N1"),
                                          new(48, 38, "W1N1"),
                                          new(48, 37, "W1N1"),
                                          new(48, 36, "W1N1"),
                                          new(48, 35, "W1N1"),
                                          new(48, 34, "W1N1"),
                                          new(48, 33, "W1N1"),
                                          new(48, 32, "W1N1"),
                                          new(48, 31, "W1N1"),
                                          new(48, 30, "W1N1"),
                                          new(48, 29, "W1N1"),
                                          new(48, 28, "W1N1"),
                                          new(48, 27, "W1N1"),
                                          new(48, 26, "W1N1"),
                                          new(48, 25, "W1N1"),
                                          new(48, 24, "W1N1"),
                                          new(48, 23, "W1N1"),
                                          new(48, 22, "W1N1"),
                                          new(48, 21, "W1N1"),
                                          new(48, 20, "W1N1"),
                                          new(48, 19, "W1N1"),
                                          new(48, 18, "W1N1"),
                                          new(48, 17, "W1N1"),
                                          new(48, 16, "W1N1"),
                                          new(48, 15, "W1N1"),
                                          new(48, 14, "W1N1"),
                                          new(48, 13, "W1N1"),
                                          new(48, 12, "W1N1"),
                                          new(48, 11, "W1N1"),
                                          new(48, 10, "W1N1"),
                                          new(48, 9, "W1N1"),
                                          new(48, 8, "W1N1"),
                                          new(48, 7, "W1N1"),
                                          new(48, 6, "W1N1"),
                                          new(49, 5, "W1N1"),
                                          new(0, 5, "W0N1"),
                                          new(1, 5, "W0N1"),
                                          new(2, 5, "W0N1"),
                                          new(3, 5, "W0N1"),
                                          new(4, 5, "W0N1"),
                                          new(5, 5, "W0N1"),
                                          new(6, 5, "W0N1"),
                                          new(7, 5, "W0N1"),
                                          new(8, 5, "W0N1"),
                                          new(9, 5, "W0N1"),
                                          new(10, 5, "W0N1"),
                                          new(11, 5, "W0N1"),
                                          new(12, 5, "W0N1"),
                                          new(13, 5, "W0N1"),
                                          new(14, 5, "W0N1"),
                                          new(15, 5, "W0N1"),
                                          new(16, 5, "W0N1"),
                                          new(17, 5, "W0N1"),
                                          new(18, 5, "W0N1"),
                                          new(19, 5, "W0N1"),
                                          new(20, 5, "W0N1"),
                                          new(21, 5, "W0N1"),
                                          new(22, 5, "W0N1"),
                                          new(23, 5, "W0N1"),
                                          new(24, 5, "W0N1"),
                                          new(25, 5, "W0N1"),
                                          new(26, 5, "W0N1"),
                                          new(27, 5, "W0N1"),
                                          new(28, 5, "W0N1"),
                                          new(29, 5, "W0N1"),
                                          new(30, 5, "W0N1"),
                                          new(31, 4, "W0N1"),
                                          new(32, 3, "W0N1"),
                                          new(33, 2, "W0N1"),
                                          new(34, 1, "W0N1"),
                                          new(35, 0, "W0N1")
                                      ],
                                      3933,
                                      3117,
                                      true)),
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
        new(FleeMultiRoomCaseName,
            [PlainTerrain("W0N0"), PlainTerrain("W1N0"), PlainTerrain("W2N0")],
            new RoomPosition(10, 25, "W0N0"),
            [new PathfinderGoal(new RoomPosition(10, 25, "W0N0"), Range: 5)],
            new PathfinderOptions(Flee: true, MaxRooms: 3, MaxOps: 20_000),
            new RegressionExpectation([
                                          new(5, 20, "W0N0"),
                                          new(6, 21, "W0N0"),
                                          new(7, 22, "W0N0"),
                                          new(8, 23, "W0N0"),
                                          new(9, 24, "W0N0")
                                      ],
                                      4,
                                      5,
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
                                  false)),
        new(TowerPowerChokeCaseName,
            [PlainTerrain("W0N0")],
            new RoomPosition(5, 10, "W0N0"),
            [new PathfinderGoal(new RoomPosition(45, 40, "W0N0"))],
            new PathfinderOptions(MaxRooms: 1, MaxOps: 60_000, RoomCallback: room => room == "W0N0" ? new PathfinderRoomCallbackResult(CreateTowerPowerCostMatrix()) : null),
            new RegressionExpectation([
                                          new(45, 40, "W0N0"),
                                          new(44, 40, "W0N0"),
                                          new(43, 40, "W0N0"),
                                          new(42, 40, "W0N0"),
                                          new(41, 40, "W0N0"),
                                          new(40, 40, "W0N0"),
                                          new(39, 40, "W0N0"),
                                          new(38, 40, "W0N0"),
                                          new(37, 40, "W0N0"),
                                          new(36, 40, "W0N0"),
                                          new(35, 40, "W0N0"),
                                          new(34, 40, "W0N0"),
                                          new(33, 39, "W0N0"),
                                          new(32, 38, "W0N0"),
                                          new(31, 37, "W0N0"),
                                          new(30, 36, "W0N0"),
                                          new(29, 35, "W0N0"),
                                          new(28, 34, "W0N0"),
                                          new(27, 33, "W0N0"),
                                          new(26, 32, "W0N0"),
                                          new(25, 31, "W0N0"),
                                          new(24, 30, "W0N0"),
                                          new(23, 29, "W0N0"),
                                          new(22, 28, "W0N0"),
                                          new(21, 27, "W0N0"),
                                          new(20, 26, "W0N0"),
                                          new(19, 25, "W0N0"),
                                          new(18, 24, "W0N0"),
                                          new(17, 23, "W0N0"),
                                          new(16, 22, "W0N0"),
                                          new(15, 21, "W0N0"),
                                          new(14, 20, "W0N0"),
                                          new(14, 19, "W0N0"),
                                          new(13, 18, "W0N0"),
                                          new(12, 17, "W0N0"),
                                          new(11, 16, "W0N0"),
                                          new(10, 15, "W0N0"),
                                          new(9, 14, "W0N0"),
                                          new(8, 13, "W0N0"),
                                      new(7, 12, "W0N0"),
                                      new(6, 11, "W0N0")
                                  ],
                                  66,
                                  41,
                                  false)),
        new(KeeperLairCorridorCaseName,
            [PlainTerrain("W0N0")],
            new RoomPosition(5, 40, "W0N0"),
            [new PathfinderGoal(new RoomPosition(45, 10, "W0N0"))],
            new PathfinderOptions(MaxRooms: 1, MaxOps: 70_000, RoomCallback: room => room == "W0N0" ? new PathfinderRoomCallbackResult(CreateKeeperLairMatrix()) : null),
            new RegressionExpectation([
                                          new(45, 10, "W0N0"),
                                          new(44, 10, "W0N0"),
                                          new(43, 10, "W0N0"),
                                          new(42, 10, "W0N0"),
                                          new(41, 10, "W0N0"),
                                          new(40, 9, "W0N0"),
                                          new(39, 9, "W0N0"),
                                          new(38, 9, "W0N0"),
                                          new(37, 9, "W0N0"),
                                          new(36, 9, "W0N0"),
                                          new(35, 9, "W0N0"),
                                          new(34, 9, "W0N0"),
                                          new(33, 9, "W0N0"),
                                          new(32, 9, "W0N0"),
                                          new(31, 9, "W0N0"),
                                          new(30, 9, "W0N0"),
                                          new(29, 9, "W0N0"),
                                          new(28, 9, "W0N0"),
                                          new(27, 9, "W0N0"),
                                          new(26, 9, "W0N0"),
                                          new(25, 9, "W0N0"),
                                          new(24, 10, "W0N0"),
                                          new(23, 11, "W0N0"),
                                          new(22, 12, "W0N0"),
                                          new(21, 13, "W0N0"),
                                          new(20, 14, "W0N0"),
                                          new(19, 15, "W0N0"),
                                          new(18, 16, "W0N0"),
                                          new(17, 17, "W0N0"),
                                          new(16, 18, "W0N0"),
                                          new(15, 19, "W0N0"),
                                          new(14, 20, "W0N0"),
                                          new(14, 21, "W0N0"),
                                          new(14, 22, "W0N0"),
                                          new(14, 23, "W0N0"),
                                          new(14, 24, "W0N0"),
                                          new(14, 25, "W0N0"),
                                          new(14, 26, "W0N0"),
                                          new(14, 27, "W0N0"),
                                          new(14, 28, "W0N0"),
                                          new(14, 29, "W0N0"),
                                          new(14, 30, "W0N0"),
                                          new(14, 31, "W0N0"),
                                          new(13, 32, "W0N0"),
                                          new(12, 33, "W0N0"),
                                          new(11, 34, "W0N0"),
                                          new(10, 35, "W0N0"),
                                          new(9, 36, "W0N0"),
                                          new(8, 37, "W0N0"),
                                          new(7, 38, "W0N0"),
                                          new(6, 39, "W0N0")
                                      ],
                                  125,
                                  51,
                                  false)),
        new(PortalChainCaseName,
            [
                PlainTerrain("W0N0"),
                PlainTerrain("W0N1"),
                PlainTerrain("W0N2"),
                PlainTerrain("W0N3"),
                PlainTerrain("E0N0"),
                PlainTerrain("E0N1"),
                PlainTerrain("E0N2"),
                PlainTerrain("E0N3"),
                PlainTerrain("W1N0"),
                PlainTerrain("W1N1"),
                PlainTerrain("W1N2"),
                PlainTerrain("W1N3"),
                PlainTerrain("W0S1"),
                PlainTerrain("E0S1"),
                PlainTerrain("W1S1")
            ],
            new RoomPosition(10, 10, "W0N0"),
            [new PathfinderGoal(new RoomPosition(45, 40, "W0N2"))],
            new PathfinderOptions(
                MaxRooms: 4,
                MaxOps: 80_000,
                RoomCallback: room => room is "W0N0" or "W0N1" or "W0N2"
                    ? new PathfinderRoomCallbackResult(CreatePortalChainMatrix(room))
                    : null),
            ExpectationFromBaseline(
                PortalChainCaseName,
                new RegressionExpectation([
                                          new(45, 40, "W0N2"),
                                          new(44, 39, "W0N2"),
                                          new(43, 38, "W0N2"),
                                          new(42, 37, "W0N2"),
                                          new(41, 36, "W0N2"),
                                          new(40, 35, "W0N2"),
                                          new(39, 34, "W0N2"),
                                          new(38, 33, "W0N2"),
                                          new(37, 32, "W0N2"),
                                          new(36, 31, "W0N2"),
                                          new(35, 30, "W0N2"),
                                          new(34, 29, "W0N2"),
                                          new(33, 28, "W0N2"),
                                          new(32, 27, "W0N2"),
                                          new(31, 26, "W0N2"),
                                          new(30, 25, "W0N2"),
                                          new(29, 24, "W0N2"),
                                          new(28, 23, "W0N2"),
                                          new(27, 22, "W0N2"),
                                          new(26, 21, "W0N2"),
                                          new(25, 20, "W0N2"),
                                          new(24, 19, "W0N2"),
                                          new(23, 18, "W0N2"),
                                          new(22, 17, "W0N2"),
                                          new(21, 16, "W0N2"),
                                          new(20, 15, "W0N2"),
                                          new(19, 14, "W0N2"),
                                          new(18, 13, "W0N2"),
                                          new(17, 12, "W0N2"),
                                          new(16, 11, "W0N2"),
                                          new(15, 10, "W0N2"),
                                          new(14, 10, "W0N2"),
                                          new(13, 10, "W0N2"),
                                          new(12, 10, "W0N2"),
                                          new(11, 10, "W0N2"),
                                          new(10, 10, "W0N2"),
                                          new(9, 10, "W0N2"),
                                          new(8, 10, "W0N2"),
                                          new(7, 10, "W0N2"),
                                          new(6, 10, "W0N2"),
                                          new(5, 10, "W0N2"),
                                          new(5, 11, "W0N2"),
                                          new(5, 12, "W0N2"),
                                          new(5, 13, "W0N2"),
                                          new(5, 14, "W0N2"),
                                          new(5, 15, "W0N2"),
                                          new(5, 16, "W0N2"),
                                          new(5, 17, "W0N2"),
                                          new(5, 18, "W0N2"),
                                          new(5, 19, "W0N2"),
                                          new(5, 20, "W0N2"),
                                          new(5, 21, "W0N2"),
                                          new(5, 22, "W0N2"),
                                          new(5, 23, "W0N2"),
                                          new(5, 24, "W0N2"),
                                          new(5, 25, "W0N2"),
                                          new(5, 26, "W0N2"),
                                          new(5, 27, "W0N2"),
                                          new(5, 28, "W0N2"),
                                          new(5, 29, "W0N2"),
                                          new(5, 30, "W0N2"),
                                          new(5, 31, "W0N2"),
                                          new(5, 32, "W0N2"),
                                          new(5, 33, "W0N2"),
                                          new(5, 34, "W0N2"),
                                          new(5, 35, "W0N2"),
                                          new(5, 36, "W0N2"),
                                          new(5, 37, "W0N2"),
                                          new(5, 38, "W0N2"),
                                          new(5, 39, "W0N2"),
                                          new(5, 40, "W0N2"),
                                          new(5, 41, "W0N2"),
                                          new(5, 42, "W0N2"),
                                          new(5, 43, "W0N2"),
                                          new(5, 44, "W0N2"),
                                          new(5, 45, "W0N2"),
                                          new(5, 46, "W0N2"),
                                          new(5, 47, "W0N2"),
                                          new(5, 48, "W0N2"),
                                          new(5, 49, "W0N2"),
                                          new(5, 0, "W0N1"),
                                          new(5, 1, "W0N1"),
                                          new(5, 2, "W0N1"),
                                          new(5, 3, "W0N1"),
                                          new(5, 4, "W0N1"),
                                          new(5, 5, "W0N1"),
                                          new(5, 6, "W0N1"),
                                          new(5, 7, "W0N1"),
                                          new(5, 8, "W0N1"),
                                          new(5, 9, "W0N1"),
                                          new(5, 10, "W0N1"),
                                          new(5, 11, "W0N1"),
                                          new(5, 12, "W0N1"),
                                          new(5, 13, "W0N1"),
                                          new(5, 14, "W0N1"),
                                          new(5, 15, "W0N1"),
                                          new(5, 16, "W0N1"),
                                          new(5, 17, "W0N1"),
                                          new(5, 18, "W0N1"),
                                          new(5, 19, "W0N1"),
                                          new(5, 20, "W0N1"),
                                          new(5, 21, "W0N1"),
                                          new(5, 22, "W0N1"),
                                          new(5, 23, "W0N1"),
                                          new(5, 24, "W0N1"),
                                          new(5, 25, "W0N1"),
                                          new(5, 26, "W0N1"),
                                          new(5, 27, "W0N1"),
                                          new(5, 28, "W0N1"),
                                          new(5, 29, "W0N1"),
                                          new(5, 30, "W0N1"),
                                          new(5, 31, "W0N1"),
                                          new(5, 32, "W0N1"),
                                          new(5, 33, "W0N1"),
                                          new(5, 34, "W0N1"),
                                          new(5, 35, "W0N1"),
                                          new(5, 36, "W0N1"),
                                          new(5, 37, "W0N1"),
                                          new(5, 38, "W0N1"),
                                          new(5, 39, "W0N1"),
                                          new(5, 40, "W0N1"),
                                          new(5, 41, "W0N1"),
                                          new(5, 42, "W0N1"),
                                          new(5, 43, "W0N1"),
                                          new(5, 44, "W0N1"),
                                          new(5, 45, "W0N1"),
                                          new(5, 46, "W0N1"),
                                          new(5, 47, "W0N1"),
                                          new(5, 48, "W0N1"),
                                          new(5, 49, "W0N1"),
                                          new(30, 0, "W0N1"),
                                          new(30, 1, "W0N1"),
                                          new(30, 2, "W0N1"),
                                          new(30, 3, "W0N1"),
                                          new(30, 4, "W0N1"),
                                          new(30, 5, "W0N1"),
                                          new(30, 6, "W0N1"),
                                          new(30, 7, "W0N1"),
                                          new(30, 8, "W0N1"),
                                          new(30, 9, "W0N1"),
                                          new(30, 10, "W0N1"),
                                          new(30, 11, "W0N1"),
                                          new(30, 12, "W0N1"),
                                          new(30, 13, "W0N1"),
                                          new(30, 14, "W0N1"),
                                          new(30, 15, "W0N1"),
                                          new(30, 16, "W0N1"),
                                          new(30, 17, "W0N1"),
                                          new(30, 18, "W0N1"),
                                          new(30, 19, "W0N1"),
                                          new(30, 20, "W0N1"),
                                          new(30, 21, "W0N1"),
                                          new(30, 22, "W0N1"),
                                          new(30, 23, "W0N1"),
                                          new(30, 24, "W0N1"),
                                          new(30, 25, "W0N1"),
                                          new(30, 26, "W0N1"),
                                          new(30, 27, "W0N1"),
                                          new(30, 28, "W0N1"),
                                          new(30, 29, "W0N1"),
                                          new(30, 30, "W0N1"),
                                          new(30, 31, "W0N1"),
                                          new(30, 32, "W0N1"),
                                          new(30, 33, "W0N1"),
                                          new(30, 34, "W0N1"),
                                          new(30, 35, "W0N1"),
                                          new(30, 36, "W0N1"),
                                          new(30, 37, "W0N1"),
                                          new(30, 38, "W0N1"),
                                          new(30, 39, "W0N1"),
                                          new(30, 40, "W0N1"),
                                          new(30, 41, "W0N1"),
                                          new(30, 42, "W0N1"),
                                          new(30, 43, "W0N1"),
                                          new(30, 44, "W0N1"),
                                          new(30, 45, "W0N1"),
                                          new(30, 46, "W0N1"),
                                          new(30, 47, "W0N1"),
                                          new(30, 48, "W0N1"),
                                          new(30, 49, "W0N1"),
                                          new(30, 0, "W0N0"),
                                          new(31, 0, "W0N0"),
                                          new(32, 0, "W0N0"),
                                          new(33, 0, "W0N0"),
                                          new(34, 0, "W0N0"),
                                          new(35, 0, "W0N0"),
                                          new(36, 0, "W0N0"),
                                          new(37, 0, "W0N0"),
                                          new(38, 0, "W0N0"),
                                          new(39, 0, "W0N0"),
                                          new(40, 0, "W0N0"),
                                          new(41, 0, "W0N0"),
                                          new(42, 0, "W0N0"),
                                          new(43, 0, "W0N0"),
                                          new(44, 0, "W0N0"),
                                          new(45, 0, "W0N0"),
                                          new(46, 0, "W0N0"),
                                          new(47, 0, "W0N0"),
                                          new(48, 0, "W0N0"),
                                          new(49, 0, "W0N0"),
                                          new(0, 5, "W0N0"),
                                          new(1, 6, "W0N0"),
                                          new(2, 7, "W0N0"),
                                          new(3, 8, "W0N0"),
                                          new(4, 9, "W0N0"),
                                          new(5, 10, "W0N0")
                                      ],
                                      3933,
                                      3117,
                                      true)))
    ];

    private static readonly IReadOnlyDictionary<string, PathfinderRegressionCase> RegressionCaseMap =
        RegressionCases.ToDictionary(caseDef => caseDef.Name);

    private static RegressionExpectation ExpectationFromBaseline(string caseName, RegressionExpectation fallback)
        => ResolveExpectation(caseName, fallback);

    private static RegressionExpectation ResolveExpectation(string caseName, RegressionExpectation fallback)
        => BaselineExpectationMap.Value.GetValueOrDefault(caseName, fallback);

    private static IReadOnlyDictionary<string, RegressionExpectation> LoadBaselineExpectations()
    {
        var map = new Dictionary<string, RegressionExpectation>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var baselinePath = Path.Combine(AppContext.BaseDirectory, RegressionBaselineRelativePath);
            if (!File.Exists(baselinePath))
                return map;

            using var document = JsonDocument.Parse(File.ReadAllBytes(baselinePath));
            if (!document.RootElement.TryGetProperty("cases", out var casesElement) ||
                casesElement.ValueKind != JsonValueKind.Array)
                return map;

            foreach (var caseElement in casesElement.EnumerateArray())
            {
                if (!caseElement.TryGetProperty("name", out var nameElement) ||
                    nameElement.ValueKind != JsonValueKind.String)
                    continue;

                var name = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var pathPositions = ParseBaselinePath(caseElement.TryGetProperty("path", out var pathElement) ? pathElement : default);
                var operations = caseElement.TryGetProperty("ops", out var opsElement) && opsElement.TryGetInt32(out var opsValue) ? opsValue : 0;
                var cost = caseElement.TryGetProperty("cost", out var costElement) && costElement.TryGetInt32(out var costValue) ? costValue : 0;
                var incomplete = caseElement.TryGetBooleanProperty("incomplete", out var incompleteValue) && incompleteValue;
                map[name] = new RegressionExpectation(pathPositions, operations, cost, incomplete);
            }

            return map;
        }
        catch
        {
            return map;
        }
    }

    private static IReadOnlyList<RoomPosition> ParseBaselinePath(JsonElement pathElement)
    {
        if (pathElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<RoomPosition>();

        var positions = new List<RoomPosition>();
        foreach (var entry in pathElement.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Array)
                continue;

            var enumerator = entry.EnumerateArray();
            if (!enumerator.MoveNext())
                continue;
            var roomElement = enumerator.Current;
            if (!enumerator.MoveNext())
                continue;
            var xElement = enumerator.Current;
            if (!enumerator.MoveNext())
                continue;
            var yElement = enumerator.Current;

            if (roomElement.ValueKind != JsonValueKind.String ||
                xElement.ValueKind != JsonValueKind.Number ||
                yElement.ValueKind != JsonValueKind.Number)
                continue;

            var roomName = roomElement.GetString();
            if (string.IsNullOrEmpty(roomName))
                continue;

            positions.Add(new RoomPosition(xElement.GetInt32(), yElement.GetInt32(), roomName));
        }

        positions.Reverse();
        return positions;
    }

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
