using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Common.Utilities;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;
using ScreepsDotNet.Engine.Tests.Processors.Helpers;

namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

public sealed class SourceRegenerationStepTests
{
    [Fact]
    public async Task SourceWithFullEnergy_DoesNothing()
    {
        // Arrange
        var source = CreateSource(id: "source1", energy: 3000, energyCapacity: 3000);
        var context = CreateContext(source, gameTime: 1000, roomType: RoomType.Normal);  // Normal room, no controller needed
        var step = new SourceRegenerationStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(((RecordingMutationWriter)context.MutationWriter).Patches);
    }

    [Fact]
    public async Task SourceWithDepletedEnergy_SetsNextRegenerationTime()
    {
        // Arrange
        var source = CreateSource(id: "source1", energy: 0, energyCapacity: 3000, nextRegenerationTime: null, gameTime: 1000);
        var context = CreateContext(source, gameTime: 1000);
        var step = new SourceRegenerationStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (objectId, payload) = Assert.Single(((RecordingMutationWriter)context.MutationWriter).Patches);
        Assert.Equal("source1", objectId);
        Assert.Equal(1000 + ScreepsGameConstants.EnergyRegenTime, payload.NextRegenerationTime);
    }

    [Fact]
    public async Task SourceRegenerates_WhenGameTimeReachesNextRegenerationTime()
    {
        // Arrange
        var source = CreateSource(id: "source1", energy: 0, energyCapacity: 3000, nextRegenerationTime: 1300, gameTime: 1299);
        var context = CreateContext(source, gameTime: 1299);
        var step = new SourceRegenerationStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (objectId, payload) = Assert.Single(((RecordingMutationWriter)context.MutationWriter).Patches);
        Assert.Equal("source1", objectId);
        Assert.Null(payload.NextRegenerationTime);
        Assert.Equal(3000, payload.Energy);
    }

    [Fact]
    public async Task SourceRegeneration_ClearsNextRegenerationTime()
    {
        // Arrange
        var source = CreateSource(id: "source1", energy: 0, energyCapacity: 3000, nextRegenerationTime: 1000, gameTime: 999);
        var context = CreateContext(source, gameTime: 999);
        var step = new SourceRegenerationStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (_, payload) = Assert.Single(((RecordingMutationWriter)context.MutationWriter).Patches);
        Assert.Null(payload.NextRegenerationTime);
        Assert.Equal(3000, payload.Energy);
    }

    [Fact]
    public async Task PWRDisruptSource_DelaysRegenerationBy1Tick()
    {
        // Arrange
        var effects = new Dictionary<PowerTypes, PowerEffectSnapshot>
        {
            [PowerTypes.DisruptSource] = new(Power: PowerTypes.DisruptSource, Level: 1, EndTime: 1100)
        };
        var source = CreateSource(id: "source1", energy: 0, energyCapacity: 3000, nextRegenerationTime: 1050, gameTime: 1000, effects: effects);
        var context = CreateContext(source, gameTime: 1000);
        var step = new SourceRegenerationStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (objectId, payload) = Assert.Single(((RecordingMutationWriter)context.MutationWriter).Patches);
        Assert.Equal("source1", objectId);
        Assert.Equal(1051, payload.NextRegenerationTime); // Incremented by 1
    }

    [Fact]
    public async Task PWRRegenSource_AddsEnergyEveryPeriodTicks()
    {
        // Arrange
        var powerInfo = PowerInfo.Abilities[PowerTypes.RegenSource];
        var effectLevel = 1;
        var period = powerInfo.Period!.Value;
        var effect = new PowerEffectSnapshot(Power: PowerTypes.RegenSource, Level: effectLevel, EndTime: 1100);
        var effects = new Dictionary<PowerTypes, PowerEffectSnapshot> { [PowerTypes.RegenSource] = effect };
        var source = CreateSource(id: "source1", energy: 100, energyCapacity: 3000, gameTime: 1000, effects: effects);

        // Calculate game time that should trigger regeneration (endTime - gameTime - 1) % period == 0
        var currentGameTime = 1100 - period; // Should trigger
        var context = CreateContext(source, gameTime: currentGameTime);
        var step = new SourceRegenerationStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (objectId, payload) = Assert.Single(((RecordingMutationWriter)context.MutationWriter).Patches);
        Assert.Equal("source1", objectId);
        var expectedEnergy = 100 + powerInfo.Effect![effectLevel - 1];
        Assert.Equal(expectedEnergy, payload.Energy);
    }

    [Fact]
    public async Task PWRRegenSource_RespectsEnergyCapacity()
    {
        // Arrange
        var powerInfo = PowerInfo.Abilities[PowerTypes.RegenSource];
        var effectLevel = 5; // Highest level
        var period = powerInfo.Period!.Value;
        var effect = new PowerEffectSnapshot(Power: PowerTypes.RegenSource, Level: effectLevel, EndTime: 1100);
        var effects = new Dictionary<PowerTypes, PowerEffectSnapshot> { [PowerTypes.RegenSource] = effect };
        var source = CreateSource(id: "source1", energy: 2990, energyCapacity: 3000, gameTime: 1000, effects: effects);

        var currentGameTime = 1100 - period;
        var context = CreateContext(source, gameTime: currentGameTime);
        var step = new SourceRegenerationStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (_, payload) = Assert.Single(((RecordingMutationWriter)context.MutationWriter).Patches);
        Assert.Equal(3000, payload.Energy); // Clamped to capacity
    }

    [Fact]
    public async Task OwnedRoom_SetsCapacityTo3000()
    {
        // Arrange
        var controller = CreateController(id: "controller1", userId: "user1");
        var source = CreateSource(id: "source1", energy: 1500, energyCapacity: 1500, gameTime: 1000);
        var context = CreateContext(source, controller);
        var step = new SourceRegenerationStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (objectId, payload) = Assert.Single(((RecordingMutationWriter)context.MutationWriter).Patches);
        Assert.Equal("source1", objectId);
        Assert.NotNull(payload.StoreCapacityResource);
        Assert.Equal(ScreepsGameConstants.SourceEnergyCapacity, payload.StoreCapacityResource[ResourceTypes.Energy]);
    }

    [Fact]
    public async Task ReservedRoom_SetsCapacityTo3000()
    {
        // Arrange
        var controller = CreateController(id: "controller1", userId: null, reservedByUserId: "user1");
        var source = CreateSource(id: "source1", energy: 1500, energyCapacity: 1500, gameTime: 1000);
        var context = CreateContext(source, controller);
        var step = new SourceRegenerationStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (_, payload) = Assert.Single(((RecordingMutationWriter)context.MutationWriter).Patches);
        Assert.NotNull(payload.StoreCapacityResource);
        Assert.Equal(ScreepsGameConstants.SourceEnergyCapacity, payload.StoreCapacityResource[ResourceTypes.Energy]);
    }

    [Fact]
    public async Task NeutralRoom_SetsCapacityTo1500()
    {
        // Arrange
        var controller = CreateController(id: "controller1", userId: null, reservedByUserId: null);
        var source = CreateSource(id: "source1", energy: 3000, energyCapacity: 3000, gameTime: 1000);
        var context = CreateContext(source, controller);
        var step = new SourceRegenerationStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (_, payload) = Assert.Single(((RecordingMutationWriter)context.MutationWriter).Patches);
        Assert.NotNull(payload.StoreCapacityResource);
        Assert.Equal(ScreepsGameConstants.SourceEnergyNeutralCapacity, payload.StoreCapacityResource[ResourceTypes.Energy]);
        Assert.Equal(1500, payload.Energy); // Clamped
    }

    [Fact]
    public async Task KeeperRoom_SetsCapacityTo4000()
    {
        // Arrange
        var source = CreateSource(id: "source1", energy: 3000, energyCapacity: 3000, gameTime: 1000);
        var context = CreateContext(source, gameTime: 1000, roomType: RoomType.Keeper);  // Explicit keeper room
        var step = new SourceRegenerationStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (_, payload) = Assert.Single(((RecordingMutationWriter)context.MutationWriter).Patches);
        Assert.NotNull(payload.StoreCapacityResource);
        Assert.Equal(ScreepsGameConstants.SourceEnergyKeeperCapacity, payload.StoreCapacityResource[ResourceTypes.Energy]);
    }

    [Fact]
    public async Task CapacityReduction_ClampsEnergy()
    {
        // Arrange
        var controller = CreateController(id: "controller1", userId: null, reservedByUserId: null);
        var source = CreateSource(id: "source1", energy: 3000, energyCapacity: 4000, gameTime: 1000);
        var context = CreateContext(source, controller);
        var step = new SourceRegenerationStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (_, payload) = Assert.Single(((RecordingMutationWriter)context.MutationWriter).Patches);
        Assert.NotNull(payload.StoreCapacityResource);
        Assert.Equal(1500, payload.StoreCapacityResource[ResourceTypes.Energy]); // Neutral capacity
        Assert.Equal(1500, payload.Energy); // Clamped from 3000 to 1500
    }

    [Fact]
    public async Task MultipleEffects_DisruptAndRegen_WorkTogether()
    {
        // Arrange - gameTime is BEFORE regeneration time, so DISRUPT delay is observable
        var powerInfo = PowerInfo.Abilities[PowerTypes.RegenSource];
        var period = powerInfo.Period!.Value;
        var effects = new Dictionary<PowerTypes, PowerEffectSnapshot>
        {
            [PowerTypes.DisruptSource] = new(Power: PowerTypes.DisruptSource, Level: 1, EndTime: 1100),
            [PowerTypes.RegenSource] = new(Power: PowerTypes.RegenSource, Level: 1, EndTime: 1100)
        };
        var source = CreateSource(id: "source1", energy: 100, energyCapacity: 3000, nextRegenerationTime: 1100, gameTime: 1000, effects: effects);

        var currentGameTime = 1100 - period;  // 1085 - before regeneration time (1100)
        var context = CreateContext(source, gameTime: currentGameTime);
        var step = new SourceRegenerationStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Both effects applied in single combined patch (more efficient, identical client result)
        var (id, payload) = Assert.Single(((RecordingMutationWriter)context.MutationWriter).Patches);
        Assert.Equal("source1", id);

        // DISRUPT delayed regeneration timer (from 1100 to 1101)
        Assert.Equal(1101, payload.NextRegenerationTime);

        // REGEN added energy
        var expectedEnergy = 100 + powerInfo.Effect![0];
        Assert.Equal(expectedEnergy, payload.Energy);
    }

    [Fact]
    public async Task RegenerationAtExactBoundary_TriggersRegen()
    {
        // Arrange
        var source = CreateSource(id: "source1", energy: 0, energyCapacity: 3000, nextRegenerationTime: 1000, gameTime: 999);
        var context = CreateContext(source, gameTime: 999);
        var step = new SourceRegenerationStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (_, payload) = Assert.Single(((RecordingMutationWriter)context.MutationWriter).Patches);
        Assert.Null(payload.NextRegenerationTime);
        Assert.Equal(3000, payload.Energy);
    }

    [Fact]
    public async Task PartialEnergy_DoesNotTriggerRegenUntilTimeReached()
    {
        // Arrange
        var source = CreateSource(id: "source1", energy: 500, energyCapacity: 3000, nextRegenerationTime: 1300, gameTime: 1000);
        var context = CreateContext(source, gameTime: 1000, roomType: RoomType.Normal);  // Normal room
        var step = new SourceRegenerationStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(((RecordingMutationWriter)context.MutationWriter).Patches); // No regeneration yet
    }

    // Helper methods
    private static RoomProcessorContext CreateContext(RoomObjectSnapshot primary, params RoomObjectSnapshot[] additional)
        => CreateContext(primary, gameTime: 1000, roomType: null, additional);

    private static RoomProcessorContext CreateContext(RoomObjectSnapshot primary, int gameTime, params RoomObjectSnapshot[] additional)
        => CreateContext(primary, gameTime, roomType: null, additional);

    private static RoomProcessorContext CreateContext(RoomObjectSnapshot primary, int gameTime, RoomType? roomType, params RoomObjectSnapshot[] additional)
    {
        var objects = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal)
        {
            [primary.Id] = primary
        };

        foreach (var extra in additional)
            objects[extra.Id] = extra;

        // Determine room type: explicit parameter > derive from room name > unknown
        var actualRoomType = roomType ?? RoomCoordinateHelper.DetermineRoomType(primary.RoomName);

        var roomInfo = new RoomInfoSnapshot(
            RoomName: primary.RoomName,
            Shard: null,
            Status: null,
            IsNoviceArea: null,
            IsRespawnArea: null,
            OpenTime: null,
            OwnerUserId: null,
            ControllerLevel: null,
            EnergyAvailable: null,
            NextNpcMarketOrder: null,
            PowerBankTime: null,
            InvaderGoal: null,
            Type: actualRoomType);

        var state = new RoomState(
            primary.RoomName,
            gameTime,
            roomInfo,
            objects,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            null,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            []);

        var context = new RoomProcessorContext(
            state,
            new RecordingMutationWriter(),
            new NullCreepStatsSink(),
            new NullGlobalMutationWriter());
        return context;
    }

    private static RoomObjectSnapshot CreateSource(string id, int energy, int energyCapacity, int? nextRegenerationTime = null, int gameTime = 1000, Dictionary<PowerTypes, PowerEffectSnapshot>? effects = null)
    {
        var storeCapacityResource = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ResourceTypes.Energy] = energyCapacity
        };

        var source = new RoomObjectSnapshot(
            Id: id,
            Type: RoomObjectTypes.Source,
            RoomName: "W1N1",
            Shard: null,
            UserId: null,
            X: 25,
            Y: 25,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: storeCapacityResource,
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: effects ?? [],
            Body: [],
            Energy: energy,
            NextRegenerationTime: nextRegenerationTime);
        return source;
    }

    private static RoomObjectSnapshot CreateController(string id, string? userId = null, string? reservedByUserId = null)
    {
        var reservation = reservedByUserId is not null ? new RoomReservationSnapshot(reservedByUserId, null) : null;

        var controller = new RoomObjectSnapshot(
            Id: id,
            Type: RoomObjectTypes.Controller,
            RoomName: "W1N1",
            Shard: null,
            UserId: userId,
            X: 25,
            Y: 25,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: reservation,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: []);
        return controller;
    }

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

        public void Reset()
            => Patches.Clear();
    }
}
