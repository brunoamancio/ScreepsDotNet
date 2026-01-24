#pragma warning disable xUnit1051 // Use TestContext.Current.CancellationToken
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

public sealed class MineralRegenerationStepTests
{
    [Fact]
    public async Task MineralWithAmountGreaterThanZero_DoesNothing()
    {
        // Arrange
        var mineral = CreateMineral(
            id: "mineral1",
            x: 25,
            y: 25,
            mineralType: ResourceTypes.Hydrogen,
            mineralAmount: 1000,
            density: ScreepsGameConstants.DensityModerate,
            nextRegenerationTime: null);

        var context = CreateContext(mineral, gameTime: 1000);
        var step = new MineralRegenerationStep();

        // Act
        await step.ExecuteAsync(context);

        // Assert
        Assert.Empty(((RecordingMutationWriter)context.MutationWriter).Patches);
    }

    [Fact]
    public async Task MineralWithZeroAmount_SetsNextRegenerationTime()
    {
        // Arrange
        var gameTime = 1000;
        var mineral = CreateMineral(
            id: "mineral1",
            x: 25,
            y: 25,
            mineralType: ResourceTypes.Hydrogen,
            mineralAmount: 0,
            density: ScreepsGameConstants.DensityModerate,
            nextRegenerationTime: null);

        var context = CreateContext(mineral, gameTime);
        var step = new MineralRegenerationStep();

        // Act
        await step.ExecuteAsync(context);

        // Assert
        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (objectId, payload) = Assert.Single(writer.Patches);
        Assert.Equal(mineral.Id, objectId);
        Assert.Equal(gameTime + ScreepsGameConstants.MineralRegenTime, payload.NextRegenerationTime);
        Assert.Null(payload.MineralAmount);
        Assert.Null(payload.Density);
    }

    [Fact]
    public async Task MineralRegenerates_WhenTimeReached()
    {
        // Arrange
        var gameTime = 51000;
        var mineral = CreateMineral(
            id: "mineral1",
            x: 25,
            y: 25,
            mineralType: ResourceTypes.Hydrogen,
            mineralAmount: 0,
            density: ScreepsGameConstants.DensityModerate,
            nextRegenerationTime: 50999);  // gameTime >= nextRegenerationTime - 1

        var context = CreateContext(mineral, gameTime);
        var step = new MineralRegenerationStep();

        // Act
        await step.ExecuteAsync(context);

        // Assert
        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (objectId, payload) = Assert.Single(writer.Patches);
        Assert.Equal(mineral.Id, objectId);
        Assert.Null(payload.NextRegenerationTime);
        Assert.NotNull(payload.MineralAmount);
        // Density may or may not change (5% probability for MODERATE)
    }

    [Fact]
    public async Task MineralRegenerationClearsNextRegenerationTime()
    {
        // Arrange
        var gameTime = 50999;
        var mineral = CreateMineral(
            id: "mineral1",
            x: 25,
            y: 25,
            mineralType: ResourceTypes.Hydrogen,
            mineralAmount: 0,
            density: ScreepsGameConstants.DensityModerate,
            nextRegenerationTime: 50999);

        var context = CreateContext(mineral, gameTime);
        var step = new MineralRegenerationStep();

        // Act
        await step.ExecuteAsync(context);

        // Assert
        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (objectId, payload) = Assert.Single(writer.Patches);
        Assert.Equal(mineral.Id, objectId);
        Assert.Null(payload.NextRegenerationTime);
    }

    [Fact]
    public async Task DensityLow_AlwaysChangesOnRegeneration()
    {
        // Arrange
        var gameTime = 50999;
        var mineral = CreateMineral(
            id: "mineral1",
            x: 25,
            y: 25,
            mineralType: ResourceTypes.Hydrogen,
            mineralAmount: 0,
            density: ScreepsGameConstants.DensityLow,
            nextRegenerationTime: 50999);

        var context = CreateContext(mineral, gameTime);
        var step = new MineralRegenerationStep();

        // Act
        await step.ExecuteAsync(context);

        // Assert
        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (objectId, payload) = Assert.Single(writer.Patches);
        Assert.Equal(mineral.Id, objectId);
        Assert.NotNull(payload.Density);
        Assert.NotEqual(ScreepsGameConstants.DensityLow, payload.Density);
    }

    [Fact]
    public async Task DensityUltra_AlwaysChangesOnRegeneration()
    {
        // Arrange
        var gameTime = 50999;
        var mineral = CreateMineral(
            id: "mineral1",
            x: 25,
            y: 25,
            mineralType: ResourceTypes.Hydrogen,
            mineralAmount: 0,
            density: ScreepsGameConstants.DensityUltra,
            nextRegenerationTime: 50999);

        var context = CreateContext(mineral, gameTime);
        var step = new MineralRegenerationStep();

        // Act
        await step.ExecuteAsync(context);

        // Assert
        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (objectId, payload) = Assert.Single(writer.Patches);
        Assert.Equal(mineral.Id, objectId);
        Assert.NotNull(payload.Density);
        Assert.NotEqual(ScreepsGameConstants.DensityUltra, payload.Density);
    }

    [Fact]
    public async Task DensityModerateOrHigh_Changes5PercentOfTime()
    {
        // Arrange - Run simulation 1000 times to test probability
        var changeCount = 0;
        const int iterations = 1000;

        for (var i = 0; i < iterations; i++) {
            var gameTime = 50999 + i;  // Vary game time for different random seeds
            var mineral = CreateMineral(
                id: $"mineral{i}",  // Vary ID for different random seeds
                x: 25,
                y: 25,
                mineralType: ResourceTypes.Hydrogen,
                mineralAmount: 0,
                density: ScreepsGameConstants.DensityModerate,
                nextRegenerationTime: 50999);

            var context = CreateContext(mineral, gameTime);
            var step = new MineralRegenerationStep();

            // Act
            await step.ExecuteAsync(context);

            // Count density changes
            var writer = (RecordingMutationWriter)context.MutationWriter;
            if (writer.Patches.TryGetValue(mineral.Id, out var payload)) {
                if (payload.Density.HasValue && payload.Density != ScreepsGameConstants.DensityModerate)
                    changeCount++;
            }
        }

        // Assert - Should be approximately 5% (50 out of 1000), allow ±3% tolerance (20-80 changes)
        Assert.InRange(changeCount, 20, 80);
    }

    [Fact]
    public async Task DensityChange_NeverPicksSameDensity()
    {
        // Arrange - Test all density values
        var densities = new[] {
            ScreepsGameConstants.DensityLow,
            ScreepsGameConstants.DensityModerate,
            ScreepsGameConstants.DensityHigh,
            ScreepsGameConstants.DensityUltra
        };

        foreach (var density in densities) {
            // Run multiple times to ensure consistency
            for (var i = 0; i < 10; i++) {
                var gameTime = 50999 + i;
                var mineral = CreateMineral(
                    id: $"mineral{density}_{i}",
                    x: 25,
                    y: 25,
                    mineralType: ResourceTypes.Hydrogen,
                    mineralAmount: 0,
                    density: density,
                    nextRegenerationTime: 50999);

                var context = CreateContext(mineral, gameTime);
                var step = new MineralRegenerationStep();

                // Act
                await step.ExecuteAsync(context);

                // Assert
                var writer = (RecordingMutationWriter)context.MutationWriter;
                if (writer.Patches.TryGetValue(mineral.Id, out var payload)) {
                    if (payload.Density.HasValue)
                        Assert.NotEqual(density, payload.Density);
                }
            }
        }
    }

    [Fact]
    public async Task RegeneratedAmount_MatchesNewDensity()
    {
        // Arrange
        var densities = new[] {
            ScreepsGameConstants.DensityLow,
            ScreepsGameConstants.DensityModerate,
            ScreepsGameConstants.DensityHigh,
            ScreepsGameConstants.DensityUltra
        };

        foreach (var density in densities) {
            var gameTime = 50999;
            var mineral = CreateMineral(
                id: $"mineral{density}",
                x: 25,
                y: 25,
                mineralType: ResourceTypes.Hydrogen,
                mineralAmount: 0,
                density: density,
                nextRegenerationTime: 50999);

            var context = CreateContext(mineral, gameTime);
            var step = new MineralRegenerationStep();

            // Act
            await step.ExecuteAsync(context);

            // Assert
            var writer = (RecordingMutationWriter)context.MutationWriter;
            var (objectId, payload) = Assert.Single(writer.Patches);
            Assert.Equal(mineral.Id, objectId);

            // Amount should be set
            Assert.NotNull(payload.MineralAmount);

            // If density changed, the amount should match the NEW density
            // If density didn't change, the amount should match the OLD density
            var effectiveDensity = payload.Density ?? density;
            var expectedAmount = ScreepsGameConstants.MineralDensityAmounts[effectiveDensity];
            Assert.Equal(expectedAmount, payload.MineralAmount);
        }
    }

    [Fact]
    public async Task PwrRegenMineral_AddsMineralsEveryPeriodTicks()
    {
        // Arrange
        var powerInfo = PowerInfo.Abilities[PowerTypes.RegenMineral];
        var period = powerInfo.Period!.Value;
        var level = 2;
        var effectAmount = powerInfo.Effect![level - 1];
        var duration = powerInfo.Duration!.Value;
        var gameTime = 1000;
        var endTime = gameTime + duration;

        var mineral = CreateMineral(
            id: "mineral1",
            x: 25,
            y: 25,
            mineralType: ResourceTypes.Hydrogen,
            mineralAmount: 50,
            density: ScreepsGameConstants.DensityModerate,
            nextRegenerationTime: null,
            effects: new Dictionary<PowerTypes, PowerEffectSnapshot>
            {
                [PowerTypes.RegenMineral] = new(Power: PowerTypes.RegenMineral, Level: level, EndTime: endTime)
            });

        var context = CreateContext(mineral, gameTime);
        var step = new MineralRegenerationStep();

        // Act
        await step.ExecuteAsync(context);

        // Assert
        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (objectId, payload) = Assert.Single(writer.Patches);
        Assert.Equal(mineral.Id, objectId);
        Assert.Equal(50 + effectAmount, payload.MineralAmount);
    }

    [Fact]
    public async Task PwrRegenMineral_OnlyWorksIfMineralAmountGreaterThanZero()
    {
        // Arrange
        var gameTime = 1000;
        var mineral = CreateMineral(
            id: "mineral1",
            x: 25,
            y: 25,
            mineralType: ResourceTypes.Hydrogen,
            mineralAmount: 0,  // Zero amount
            density: ScreepsGameConstants.DensityModerate,
            nextRegenerationTime: 60000,
            effects: new Dictionary<PowerTypes, PowerEffectSnapshot>
            {
                [PowerTypes.RegenMineral] = new(Power: PowerTypes.RegenMineral, Level: 2, EndTime: 1100)
            });

        var context = CreateContext(mineral, gameTime);
        var step = new MineralRegenerationStep();

        // Act
        await step.ExecuteAsync(context);

        // Assert - Should not apply PWR_REGEN_MINERAL effect
        var writer = (RecordingMutationWriter)context.MutationWriter;
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task PwrRegenMineral_HasNoUpperLimit()
    {
        // Arrange
        var powerInfo = PowerInfo.Abilities[PowerTypes.RegenMineral];
        var period = powerInfo.Period!.Value;
        var level = 5;
        var effectAmount = powerInfo.Effect![level - 1];
        var gameTime = 1000;
        var duration = powerInfo.Duration!.Value;
        var endTime = gameTime + duration;

        var mineral = CreateMineral(
            id: "mineral1",
            x: 25,
            y: 25,
            mineralType: ResourceTypes.Hydrogen,
            mineralAmount: 200000,  // Already very high
            density: ScreepsGameConstants.DensityUltra,
            nextRegenerationTime: null,
            effects: new Dictionary<PowerTypes, PowerEffectSnapshot>
            {
                [PowerTypes.RegenMineral] = new(Power: PowerTypes.RegenMineral, Level: level, EndTime: endTime)
            });

        var context = CreateContext(mineral, gameTime);
        var step = new MineralRegenerationStep();

        // Act
        await step.ExecuteAsync(context);

        // Assert - Should still add minerals even though amount is high
        var writer = (RecordingMutationWriter)context.MutationWriter;
        var (objectId, payload) = Assert.Single(writer.Patches);
        Assert.Equal(mineral.Id, objectId);
        Assert.Equal(200000 + effectAmount, payload.MineralAmount);
    }

    private static RoomProcessorContext CreateContext(RoomObjectSnapshot mineral, int gameTime)
    {
        var objects = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal)
        {
            [mineral.Id] = mineral
        };

        var state = new RoomState(
            mineral.RoomName,
            gameTime,
            null,
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

    private static RoomObjectSnapshot CreateMineral(
        string id,
        int x,
        int y,
        string mineralType,
        int mineralAmount,
        int density,
        int? nextRegenerationTime,
        IReadOnlyDictionary<PowerTypes, PowerEffectSnapshot>? effects = null)
    {
        var result = new RoomObjectSnapshot(
            Id: id,
            Type: RoomObjectTypes.Mineral,
            RoomName: "W1N1",
            Shard: null,
            UserId: null,
            X: x,
            Y: y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: density,
            MineralType: mineralType,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: effects ?? new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            Spawning: null,
            IsSpawning: null,
            UserSummoned: null,
            IsPublic: null,
            StrongholdId: null,
            DeathTime: null,
            DecayTime: null,
            CreepId: null,
            CreepName: null,
            CreepTicksToLive: null,
            CreepSaying: null,
            ResourceType: null,
            ResourceAmount: null,
            Progress: null,
            ProgressTotal: null,
            ActionLog: null,
            Energy: null,
            MineralAmount: mineralAmount,
            InvaderHarvested: null,
            Harvested: null,
            Cooldown: null,
            CooldownTime: null,
            NextRegenerationTime: nextRegenerationTime,
            SafeMode: null,
            SafeModeAvailable: null,
            PortalDestination: null,
            Send: null,
            Powers: null);
        return result;
    }

    private sealed class RecordingMutationWriter : IRoomMutationWriter
    {
        public Dictionary<string, RoomObjectPatchPayload> Patches { get; } = new(StringComparer.Ordinal);

        public void Upsert(RoomObjectSnapshot document) { }

        public void Patch(string objectId, RoomObjectPatchPayload patch)
            => Patches[objectId] = patch;

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

    private sealed class NullCreepStatsSink : ICreepStatsSink
    {
        public void IncrementEnergyCreeps(string userId, int amount) { }
        public void IncrementCreepsLost(string userId, int bodyParts) { }
        public void IncrementCreepsProduced(string userId, int bodyParts) { }
        public void IncrementSpawnRenewals(string userId) { }
        public void IncrementSpawnRecycles(string userId) { }
        public void IncrementSpawnCreates(string userId) { }
        public void IncrementTombstonesCreated(string userId) { }
        public void IncrementEnergyConstruction(string userId, int amount) { }
        public void IncrementEnergyHarvested(string userId, int amount) { }
        public void IncrementEnergyControl(string userId, int amount) { }
#pragma warning disable CA1822 // Mark members as static
        public int GetMutationCount() => 0;
#pragma warning restore CA1822

        public Task FlushAsync(int gameTime, CancellationToken token = default) => Task.CompletedTask;
    }
}
