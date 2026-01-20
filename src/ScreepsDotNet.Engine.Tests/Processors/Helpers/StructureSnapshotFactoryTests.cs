namespace ScreepsDotNet.Engine.Tests.Processors.Helpers;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Structures;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Engine.Processors.Helpers;

public sealed class StructureSnapshotFactoryTests
{
    private readonly IStructureBlueprintProvider _provider = new StructureBlueprintProvider();
    private readonly StructureSnapshotFactory _factory = new();

    [Fact]
    public void CreateStructureSnapshot_Spawn_UsesBlueprintDefaults()
    {
        var blueprint = _provider.GetRequired(RoomObjectTypes.Spawn);
        var options = new StructureCreationOptions("W1N1", "shard0", "user1", 10, 20, 100, ControllerLevel: ControllerLevel.Level8, OnSwamp: false, OnWall: false);

        var snapshot = _factory.CreateStructureSnapshot(blueprint, options);

        Assert.Equal(RoomObjectTypes.Spawn, snapshot.Type);
        Assert.Equal(ScreepsGameConstants.SpawnHits, snapshot.Hits);
        Assert.Equal(ScreepsGameConstants.SpawnHits, snapshot.HitsMax);
        Assert.Equal(0, snapshot.Store[RoomDocumentFields.RoomObject.Store.Energy]);
        Assert.Equal(
            ScreepsGameConstants.SpawnEnergyCapacity,
            snapshot.StoreCapacityResource[RoomDocumentFields.RoomObject.Store.Energy]);
    }

    [Fact]
    public void CreateStructureSnapshot_Road_AdjustsHitsForTerrain()
    {
        var blueprint = _provider.GetRequired(RoomObjectTypes.Road);
        var options = new StructureCreationOptions("W1N1", null, null, 5, 5, 1234, ControllerLevel: null, OnSwamp: true, OnWall: true);

        var snapshot = _factory.CreateStructureSnapshot(blueprint, options);

        var expected = ScreepsGameConstants.RoadHits * ScreepsGameConstants.RoadSwampMultiplier * ScreepsGameConstants.RoadWallMultiplier;
        Assert.Equal(expected, snapshot.Hits);
        Assert.Equal(expected, snapshot.HitsMax);
    }

    [Fact]
    public void CreateStructureSnapshot_Container_SetsDecayTime()
    {
        var blueprint = _provider.GetRequired(RoomObjectTypes.Container);
        var gameTime = 5000;
        var options = new StructureCreationOptions("W1N1", null, "user1", 3, 7, gameTime, ControllerLevel: null, OnSwamp: false, OnWall: false);

        var snapshot = _factory.CreateStructureSnapshot(blueprint, options);

        Assert.Equal(gameTime + ScreepsGameConstants.ContainerDecayOwnedInterval, snapshot.DecayTime);
        Assert.Equal(ScreepsGameConstants.ContainerCapacity, snapshot.StoreCapacity);
    }
}
