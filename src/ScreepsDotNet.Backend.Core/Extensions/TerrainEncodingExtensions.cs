namespace ScreepsDotNet.Backend.Core.Extensions;

using ScreepsDotNet.Backend.Core.Constants;

public static class TerrainEncodingExtensions
{
    private const int WallBit = 1;
    private const int SwampBit = 2;

    public static TerrainType ToTerrainType(this int encodedValue)
    {
        if ((encodedValue & WallBit) != 0)
            return TerrainType.Wall;

        if ((encodedValue & SwampBit) != 0)
            return TerrainType.Swamp;

        return TerrainType.Plain;
    }
}
