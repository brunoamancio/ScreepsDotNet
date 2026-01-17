namespace ScreepsDotNet.Engine.Tests.Processors.Helpers;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors.Helpers;

public sealed class SpawnEnergyAllocatorTests
{
    private readonly ISpawnEnergyAllocator _allocator = new SpawnEnergyAllocator();

    [Fact]
    public void AllocateEnergy_UsesPreferredStructuresFirst()
    {
        var spawn = CreateStructure("spawn1", RoomObjectTypes.Spawn, energy: 100);
        var extension = CreateStructure("ext1", RoomObjectTypes.Extension, energy: 150);
        var objects = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal)
        {
            [spawn.Id] = spawn,
            [extension.Id] = extension
        };

        var result = _allocator.AllocateEnergy(objects, spawn, 120, [extension.Id]);

        Assert.True(result.Success);
        var draw = Assert.Single(result.Draws, d => d.Source.Id == extension.Id);
        Assert.Equal(120, draw.Amount);
    }

    [Fact]
    public void AllocateEnergy_FallsBackToSpawnAndExtensions()
    {
        var spawn = CreateStructure("spawn1", RoomObjectTypes.Spawn, energy: 50);
        var extension = CreateStructure("ext1", RoomObjectTypes.Extension, energy: 80);
        var objects = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal)
        {
            [spawn.Id] = spawn,
            [extension.Id] = extension
        };

        var result = _allocator.AllocateEnergy(objects, spawn, 100, null);

        Assert.True(result.Success);
        Assert.Equal(2, result.Draws.Count);
        Assert.Equal(50, result.Draws[0].Amount);
        Assert.Equal(50, result.Draws[1].Amount);
    }

    [Fact]
    public void AllocateEnergy_Fails_WhenEnergyInsufficient()
    {
        var spawn = CreateStructure("spawn1", RoomObjectTypes.Spawn, energy: 20);
        var objects = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal)
        {
            [spawn.Id] = spawn
        };

        var result = _allocator.AllocateEnergy(objects, spawn, 100, null);

        Assert.False(result.Success);
        Assert.Contains("Not enough energy", result.Error);
    }

    [Fact]
    public void AllocateEnergy_RespectsOverrides()
    {
        var spawn = CreateStructure("spawn1", RoomObjectTypes.Spawn, energy: 50);
        var extension = CreateStructure("ext1", RoomObjectTypes.Extension, energy: 100);
        var objects = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal)
        {
            [spawn.Id] = spawn,
            [extension.Id] = extension
        };

        var overrides = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [extension.Id] = 30
        };

        var result = _allocator.AllocateEnergy(objects, spawn, 60, [extension.Id], overrides);

        Assert.True(result.Success);
        Assert.Equal(2, result.Draws.Count);
        Assert.Equal(extension.Id, result.Draws[0].Source.Id);
        Assert.Equal(30, result.Draws[0].Amount);
        Assert.Equal(spawn.Id, result.Draws[1].Source.Id);
        Assert.Equal(30, result.Draws[1].Amount);
    }

    private static RoomObjectSnapshot CreateStructure(string id, string type, int energy)
        => new(
            id,
            type,
            "W1N1",
            "shard0",
            "user1",
            10,
            10,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: type,
            Store: new Dictionary<string, int>(StringComparer.Ordinal) { [RoomDocumentFields.RoomObject.Store.Energy] = energy },
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<string, object?>(),
            Spawning: null,
            Body: []);
}
