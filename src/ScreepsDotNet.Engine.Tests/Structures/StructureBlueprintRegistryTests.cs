namespace ScreepsDotNet.Engine.Tests.Structures;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Structures;

public sealed class StructureBlueprintRegistryTests
{
    [Fact]
    public void ExtensionBlueprint_HasControllerCapacities()
    {
        var found = StructureBlueprintRegistry.TryGetBlueprint(RoomObjectTypes.Extension, out var blueprint);
        Assert.True(found);
        Assert.NotNull(blueprint);
        Assert.Equal(0, blueprint!.Store.InitialStore[RoomDocumentFields.RoomObject.Store.Energy]);
        Assert.Equal(ScreepsGameConstants.ExtensionEnergyCapacityByControllerLevel[8], blueprint.Store.ControllerLevelCapacity![8]);
    }

    [Fact]
    public void SpawnBlueprint_HasExpectedDefaults()
    {
        var found = StructureBlueprintRegistry.TryGetBlueprint(RoomObjectTypes.Spawn, out var blueprint);
        Assert.True(found);
        Assert.NotNull(blueprint);
        Assert.Equal(ScreepsGameConstants.SpawnHits, blueprint!.Hits.Hits);
        Assert.Equal(ScreepsGameConstants.SpawnHits, blueprint.Hits.HitsMax);
        Assert.Equal(0, blueprint.Store.InitialStore[RoomDocumentFields.RoomObject.Store.Energy]);
        Assert.Equal(ScreepsGameConstants.SpawnEnergyCapacity, blueprint.Store.StoreCapacityResource[RoomDocumentFields.RoomObject.Store.Energy]);
        Assert.True(blueprint.Ownership.RequiresOwner);
        Assert.True(blueprint.Ownership.NotifyWhenAttacked);
    }

    [Fact]
    public void ContainerBlueprint_HasDecayMetadata()
    {
        var found = StructureBlueprintRegistry.TryGetBlueprint(RoomObjectTypes.Container, out var blueprint);
        Assert.True(found);
        Assert.NotNull(blueprint);
        Assert.NotNull(blueprint!.Decay);
        Assert.Equal(ScreepsGameConstants.ContainerDecayAmount, blueprint.Decay!.Amount);
        Assert.Equal(ScreepsGameConstants.ContainerDecayInterval, blueprint.Decay.IntervalTicks);
        Assert.Equal(ScreepsGameConstants.ContainerDecayOwnedInterval, blueprint.Decay.OwnedIntervalTicks);
        Assert.Equal(ScreepsGameConstants.ContainerCapacity, blueprint.Store.StoreCapacity);
    }

    [Fact]
    public void RampartBlueprint_ContainsHitsLookupAndDecay()
    {
        var found = StructureBlueprintRegistry.TryGetBlueprint(RoomObjectTypes.Rampart, out var blueprint);
        Assert.True(found);
        Assert.NotNull(blueprint);
        Assert.NotNull(blueprint!.Rampart);
        Assert.Equal(ScreepsGameConstants.RampartHitsMaxByControllerLevel[8], blueprint.Rampart!.HitsMaxByControllerLevel[8]);
        Assert.NotNull(blueprint.Decay);
        Assert.Equal(ScreepsGameConstants.RampartDecayAmount, blueprint.Decay!.Amount);
        Assert.Equal(ScreepsGameConstants.RampartDecayInterval, blueprint.Decay.IntervalTicks);
    }

    [Fact]
    public void RoadBlueprint_HasTerrainMultipliers()
    {
        var found = StructureBlueprintRegistry.TryGetBlueprint(RoomObjectTypes.Road, out var blueprint);
        Assert.True(found);
        Assert.NotNull(blueprint);
        Assert.NotNull(blueprint!.Road);
        Assert.Equal(ScreepsGameConstants.RoadHits, blueprint.Road!.BaseHits);
        Assert.Equal(ScreepsGameConstants.RoadSwampMultiplier, blueprint.Road.SwampMultiplier);
        Assert.Equal(ScreepsGameConstants.RoadWallMultiplier, blueprint.Road.WallMultiplier);
        Assert.Equal(ScreepsGameConstants.RoadDecayAmount, blueprint.Road.DecayAmount);
        Assert.Equal(ScreepsGameConstants.RoadDecayInterval, blueprint.Road.DecayTime);
    }
}
