namespace ScreepsDotNet.Backend.Core.Constants;

public static class GameConstants
{
    public const int MaxConstructionSites = 100;
    public const int TerrainMaskWall = 1;
    public const int TerrainMaskSwamp = 2;
    public const int ConstructionCostRoadSwampRatio = 5;
    public const int ConstructionCostRoadWallRatio = 150;

    public static readonly IReadOnlyDictionary<StructureType, int> ConstructionCost = new Dictionary<StructureType, int>
    {
        [StructureType.Spawn] = 15000,
        [StructureType.Extension] = 3000,
        [StructureType.Road] = 300,
        [StructureType.Wall] = 1,
        [StructureType.Rampart] = 1,
        [StructureType.Link] = 5000,
        [StructureType.Storage] = 30000,
        [StructureType.Tower] = 5000,
        [StructureType.Observer] = 8000,
        [StructureType.PowerSpawn] = 100000,
        [StructureType.Extractor] = 5000,
        [StructureType.Lab] = 50000,
        [StructureType.Terminal] = 100000,
        [StructureType.Container] = 5000,
        [StructureType.Nuker] = 100000,
        [StructureType.Factory] = 100000
    };

    public static readonly IReadOnlyDictionary<StructureType, int[]> ControllerStructures = new Dictionary<StructureType, int[]>
    {
        [StructureType.Spawn] = [0, 1, 1, 1, 1, 1, 1, 2, 3],
        [StructureType.Extension] = [0, 0, 5, 10, 20, 30, 40, 50, 60],
        [StructureType.Road] = [0, 2500, 2500, 2500, 2500, 2500, 2500, 2500, 2500],
        [StructureType.Wall] = [0, 2500, 2500, 2500, 2500, 2500, 2500, 2500, 2500],
        [StructureType.Rampart] = [0, 2500, 2500, 2500, 2500, 2500, 2500, 2500, 2500],
        [StructureType.Link] = [0, 0, 0, 0, 0, 2, 3, 4, 6],
        [StructureType.Storage] = [0, 0, 0, 0, 1, 1, 1, 1, 1],
        [StructureType.Tower] = [0, 0, 0, 1, 1, 2, 2, 3, 6],
        [StructureType.Observer] = [0, 0, 0, 0, 0, 0, 0, 0, 1],
        [StructureType.PowerSpawn] = [0, 0, 0, 0, 0, 0, 0, 0, 1],
        [StructureType.Extractor] = [0, 0, 0, 0, 0, 0, 1, 1, 1],
        [StructureType.Lab] = [0, 0, 0, 0, 0, 0, 3, 6, 10],
        [StructureType.Terminal] = [0, 0, 0, 0, 0, 0, 1, 1, 1],
        [StructureType.Container] = [5, 5, 5, 5, 5, 5, 5, 5, 5],
        [StructureType.Nuker] = [0, 0, 0, 0, 0, 0, 0, 0, 1],
        [StructureType.Factory] = [0, 0, 0, 0, 0, 0, 0, 1, 1]
    };

    public static readonly StructureType[] BlockerStructureTypes =
    [
        StructureType.Wall, StructureType.ConstructedWall, StructureType.Spawn, StructureType.Extension, StructureType.Link, StructureType.Storage, StructureType.Tower, StructureType.Observer, StructureType.PowerSpawn, StructureType.Lab, StructureType.Terminal, StructureType.Nuker, StructureType.Factory, StructureType.Controller
    ];
}
