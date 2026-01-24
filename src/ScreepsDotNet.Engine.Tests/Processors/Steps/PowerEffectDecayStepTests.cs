using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;
using ScreepsDotNet.Engine.Tests.Processors.Helpers;

namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

public sealed class PowerEffectDecayStepTests
{
    [Fact]
    public async Task ExecuteAsync_ExpiredEffect_RemovesFromStructure()
    {
        var spawn = CreateSpawn(effects: new Dictionary<PowerTypes, PowerEffectSnapshot>
        {
            [PowerTypes.OperateSpawn] = new(PowerTypes.OperateSpawn, Level: 3, EndTime: 10)
        });
        var context = CreateContext(spawn, gameTime: 11);
        var step = new PowerEffectDecayStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Single(((RecordingMutationWriter)context.MutationWriter).Patches);
        var (objectId, payload) = ((RecordingMutationWriter)context.MutationWriter).Patches[0];
        Assert.Equal("spawn1", objectId);
        Assert.NotNull(payload.Effects);
        Assert.Empty(payload.Effects);
    }

    [Fact]
    public async Task ExecuteAsync_NotExpiredEffect_RemainsOnStructure()
    {
        var spawn = CreateSpawn(effects: new Dictionary<PowerTypes, PowerEffectSnapshot>
        {
            [PowerTypes.OperateSpawn] = new(PowerTypes.OperateSpawn, Level: 3, EndTime: 20)
        });
        var context = CreateContext(spawn, gameTime: 10);
        var step = new PowerEffectDecayStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(((RecordingMutationWriter)context.MutationWriter).Patches);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleEffects_OnlyExpiredOnesRemoved()
    {
        var spawn = CreateSpawn(effects: new Dictionary<PowerTypes, PowerEffectSnapshot>
        {
            [PowerTypes.OperateSpawn] = new(PowerTypes.OperateSpawn, Level: 3, EndTime: 10),
            [PowerTypes.DisruptSpawn] = new(PowerTypes.DisruptSpawn, Level: 2, EndTime: 20)
        });
        var context = CreateContext(spawn, gameTime: 11);
        var step = new PowerEffectDecayStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Single(((RecordingMutationWriter)context.MutationWriter).Patches);
        var (objectId, payload) = ((RecordingMutationWriter)context.MutationWriter).Patches[0];
        Assert.Equal("spawn1", objectId);
        Assert.NotNull(payload.Effects);
        Assert.Single(payload.Effects);
        Assert.True(payload.Effects.ContainsKey(PowerTypes.DisruptSpawn));
        Assert.False(payload.Effects.ContainsKey(PowerTypes.OperateSpawn));
    }

    [Fact]
    public async Task ExecuteAsync_NoEffects_NoPatchEmitted()
    {
        var spawn = CreateSpawn(effects: new Dictionary<PowerTypes, PowerEffectSnapshot>());
        var context = CreateContext(spawn, gameTime: 10);
        var step = new PowerEffectDecayStep();

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(((RecordingMutationWriter)context.MutationWriter).Patches);
    }

    private static RoomProcessorContext CreateContext(RoomObjectSnapshot primary, int gameTime, params RoomObjectSnapshot[] additional)
    {
        var objects = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal)
        {
            [primary.Id] = primary
        };

        foreach (var extra in additional)
            objects[extra.Id] = extra;

        var state = new RoomState(
            primary.RoomName,
            gameTime,
            null,
            objects,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            null,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            []);

        return new RoomProcessorContext(
            state,
            new RecordingMutationWriter(),
            new NullCreepStatsSink(),
            new NullGlobalMutationWriter());
    }

    private static RoomObjectSnapshot CreateSpawn(IReadOnlyDictionary<PowerTypes, PowerEffectSnapshot> effects)
        => new(
            "spawn1",
            RoomObjectTypes.Spawn,
            "W1N1",
            null,
            "user1",
            10,
            10,
            Hits: 5000,
            HitsMax: 5000,
            Fatigue: null,
            TicksToLive: null,
            Name: "Spawn1",
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = 300
            },
            StoreCapacity: 300,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: effects,
            Spawning: null,
            Body: []);

    private sealed class RecordingMutationWriter : IRoomMutationWriter
    {
        public List<(string ObjectId, RoomObjectPatchPayload Payload)> Patches { get; } = [];

        public void Upsert(RoomObjectSnapshot document) { }

        public void Patch(string objectId, RoomObjectPatchPayload patch)
            => Patches.Add((objectId, patch));

        public void Remove(string objectId) { }

        public void SetRoomInfoPatch(RoomInfoPatchPayload patch) { }

        public void SetEventLog(IRoomEventLogPayload? eventLog) { }

        public void SetMapView(IRoomMapViewPayload? mapView) { }

#pragma warning disable CA1822 // Mark members as static
        public int GetMutationCount() => 0;
#pragma warning restore CA1822

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;

#pragma warning disable CA1822 // Method cannot be static as it implements interface member
        public bool TryGetPendingPatch(string objectId, out RoomObjectPatchPayload patch) { patch = new RoomObjectPatchPayload(); return false; }

        public void Reset()
            => Patches.Clear();
    }
}
