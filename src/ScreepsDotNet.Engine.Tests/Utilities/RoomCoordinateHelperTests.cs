using ScreepsDotNet.Common.Utilities;

namespace ScreepsDotNet.Engine.Tests.Utilities;

public sealed class RoomCoordinateHelperTests
{
    [Fact]
    public void ToCoordinates_ParsesQuadrants()
    {
        var (x1, y1) = RoomCoordinateHelper.ToCoordinates("E0S0");
        Assert.Equal((0, 0), (x1, y1));

        var (x2, y2) = RoomCoordinateHelper.ToCoordinates("W10N5");
        Assert.Equal((-11, -6), (x2, y2));

        var (x3, y3) = RoomCoordinateHelper.ToCoordinates("E12N3");
        Assert.Equal((12, -4), (x3, y3));
    }

    [Fact]
    public void FromCoordinates_FormatsNames()
    {
        Assert.Equal("E0S0", RoomCoordinateHelper.FromCoordinates(0, 0));
        Assert.Equal("W1S2", RoomCoordinateHelper.FromCoordinates(-2, 2));
        Assert.Equal("E3N0", RoomCoordinateHelper.FromCoordinates(3, -1));
    }

    [Fact]
    public void CalculateDistance_HandlesWrap()
    {
        var nonWrap = RoomCoordinateHelper.CalculateDistance("E0S0", "E5S5");
        Assert.Equal(5, nonWrap);

        var wrap = RoomCoordinateHelper.CalculateDistance("E0S0", "E59S0", wrapWorld: true, worldSize: 60);
        Assert.Equal(1, wrap);
    }
}
