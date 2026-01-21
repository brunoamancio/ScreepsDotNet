using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Validation.Models;
using ScreepsDotNet.Engine.Validation.Validators;

namespace ScreepsDotNet.Engine.Tests.Validation.Validators;

/// <summary>
/// Tests for ResourceValidator - validates resource availability, capacity, and costs.
/// </summary>
public sealed class ResourceValidatorTests
{
    private readonly ResourceValidator _validator = new();

    #region Energy Validation (Build/Repair/Upgrade)

    [Fact]
    public void Validate_Build_SufficientEnergy_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.Build, actorId: "creep1", targetId: "constructionSite1");
        var snapshot = CreateSnapshot(
            new ObjectData("creep1", RoomObjectTypes.Creep, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 }, BodyParts: [new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)]),
            new ObjectData("constructionSite1", RoomObjectTypes.ConstructionSite, UserId: "user1"));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Validate_Build_InsufficientEnergy_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.Build, actorId: "creep1", targetId: "constructionSite1");
        var snapshot = CreateSnapshot(
            new ObjectData("creep1", RoomObjectTypes.Creep, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 0 }, BodyParts: [new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)]),
            new ObjectData("constructionSite1", RoomObjectTypes.ConstructionSite, UserId: "user1"));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.InsufficientEnergy, result.ErrorCode);
    }

    [Fact]
    public void Validate_Repair_SufficientEnergy_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.Repair, actorId: "creep1", targetId: "road1");
        var snapshot = CreateSnapshot(
            new ObjectData("creep1", RoomObjectTypes.Creep, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 50 }, BodyParts: [new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)]),
            new ObjectData("road1", RoomObjectTypes.Road, UserId: null, Hits: 100, HitsMax: 200));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Repair_InsufficientEnergy_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.Repair, actorId: "creep1", targetId: "road1");
        var snapshot = CreateSnapshot(
            new ObjectData("creep1", RoomObjectTypes.Creep, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 0 }, BodyParts: [new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)]),
            new ObjectData("road1", RoomObjectTypes.Road, UserId: null, Hits: 100, HitsMax: 200));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.InsufficientEnergy, result.ErrorCode);
    }

    [Fact]
    public void Validate_UpgradeController_SufficientEnergy_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.UpgradeController, actorId: "creep1", targetId: "controller1");
        var snapshot = CreateSnapshot(
            new ObjectData("creep1", RoomObjectTypes.Creep, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 10 }, BodyParts: [new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)]),
            new ObjectData("controller1", RoomObjectTypes.Controller, UserId: "user1"));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_UpgradeController_InsufficientEnergy_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.UpgradeController, actorId: "creep1", targetId: "controller1");
        var snapshot = CreateSnapshot(
            new ObjectData("creep1", RoomObjectTypes.Creep, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 0 }, BodyParts: [new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)]),
            new ObjectData("controller1", RoomObjectTypes.Controller, UserId: "user1"));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.InsufficientEnergy, result.ErrorCode);
    }

    [Fact]
    public void Validate_UpgradeController_BoostedCreep_SufficientEnergy_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.UpgradeController, actorId: "creep1", targetId: "controller1");
        var snapshot = CreateSnapshot(
            new ObjectData("creep1", RoomObjectTypes.Creep, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 2 }, BodyParts: [new CreepBodyPartSnapshot(BodyPartType.Work, 100, ResourceTypes.GhodiumHydride)]),
            new ObjectData("controller1", RoomObjectTypes.Controller, UserId: "user1"));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
    }

    #endregion

    #region Mineral/Compound Validation (Lab Boost)

    [Fact]
    public void Validate_BoostCreep_SufficientEnergyAndMineral_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.BoostCreep, actorId: "lab1", targetId: "creep1", resourceType: ResourceTypes.UtriumHydride, bodyPartType: BodyPartType.Attack);
        var snapshot = CreateSnapshot(
            new ObjectData("lab1", RoomObjectTypes.Lab, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 60, [ResourceTypes.UtriumHydride] = 90 }),
            new ObjectData("creep1", RoomObjectTypes.Creep, UserId: "user1", BodyParts: [new CreepBodyPartSnapshot(BodyPartType.Attack, 100, null), new CreepBodyPartSnapshot(BodyPartType.Attack, 100, null), new CreepBodyPartSnapshot(BodyPartType.Attack, 100, null)]));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_BoostCreep_InsufficientEnergy_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.BoostCreep, actorId: "lab1", targetId: "creep1", resourceType: ResourceTypes.UtriumHydride, bodyPartType: BodyPartType.Attack);
        var snapshot = CreateSnapshot(
            new ObjectData("lab1", RoomObjectTypes.Lab, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 10, [ResourceTypes.UtriumHydride] = 90 }),
            new ObjectData("creep1", RoomObjectTypes.Creep, UserId: "user1", BodyParts: [new CreepBodyPartSnapshot(BodyPartType.Attack, 100, null), new CreepBodyPartSnapshot(BodyPartType.Attack, 100, null), new CreepBodyPartSnapshot(BodyPartType.Attack, 100, null)]));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.InsufficientEnergy, result.ErrorCode);
    }

    [Fact]
    public void Validate_BoostCreep_InsufficientMineral_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.BoostCreep, actorId: "lab1", targetId: "creep1", resourceType: ResourceTypes.UtriumHydride, bodyPartType: BodyPartType.Attack);
        var snapshot = CreateSnapshot(
            new ObjectData("lab1", RoomObjectTypes.Lab, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 60, [ResourceTypes.UtriumHydride] = 10 }),
            new ObjectData("creep1", RoomObjectTypes.Creep, UserId: "user1", BodyParts: [new CreepBodyPartSnapshot(BodyPartType.Attack, 100, null), new CreepBodyPartSnapshot(BodyPartType.Attack, 100, null), new CreepBodyPartSnapshot(BodyPartType.Attack, 100, null)]));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.InsufficientResource, result.ErrorCode);
    }

    [Fact]
    public void Validate_UnboostCreep_LabHasCapacity_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.UnboostCreep, actorId: "lab1", targetId: "creep1");
        var snapshot = CreateSnapshot(
            new ObjectData("lab1", RoomObjectTypes.Lab, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.UtriumHydride] = 100 }, StoreCapacity: 3000),
            new ObjectData("creep1", RoomObjectTypes.Creep, UserId: "user1", BodyParts: [new CreepBodyPartSnapshot(BodyPartType.Attack, 100, ResourceTypes.UtriumHydride)]));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_UnboostCreep_LabCapacityFull_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.UnboostCreep, actorId: "lab1", targetId: "creep1");
        var snapshot = CreateSnapshot(
            new ObjectData("lab1", RoomObjectTypes.Lab, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.UtriumHydride] = 3000 }, StoreCapacity: 3000),
            new ObjectData("creep1", RoomObjectTypes.Creep, UserId: "user1", BodyParts: [new CreepBodyPartSnapshot(BodyPartType.Attack, 100, ResourceTypes.UtriumHydride)]));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.TargetCapacityFull, result.ErrorCode);
    }

    #endregion

    #region Transfer/Capacity Validation

    [Fact]
    public void Validate_Transfer_SufficientResource_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.Transfer, actorId: "creep1", targetId: "spawn1", resourceType: ResourceTypes.Energy, amount: 50);
        var snapshot = CreateSnapshot(
            new ObjectData("creep1", RoomObjectTypes.Creep, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 }),
            new ObjectData("spawn1", RoomObjectTypes.Spawn, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 }, StoreCapacity: 300));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Transfer_InsufficientResource_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.Transfer, actorId: "creep1", targetId: "spawn1", resourceType: ResourceTypes.Energy, amount: 150);
        var snapshot = CreateSnapshot(
            new ObjectData("creep1", RoomObjectTypes.Creep, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 }),
            new ObjectData("spawn1", RoomObjectTypes.Spawn, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 }, StoreCapacity: 300));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.InsufficientResource, result.ErrorCode);
    }

    [Fact]
    public void Validate_Transfer_TargetCapacityFull_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.Transfer, actorId: "creep1", targetId: "spawn1", resourceType: ResourceTypes.Energy, amount: 50);
        var snapshot = CreateSnapshot(
            new ObjectData("creep1", RoomObjectTypes.Creep, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 }),
            new ObjectData("spawn1", RoomObjectTypes.Spawn, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 300 }, StoreCapacity: 300));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.TargetCapacityFull, result.ErrorCode);
    }

    [Fact]
    public void Validate_Withdraw_SufficientResource_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.Withdraw, actorId: "creep1", targetId: "storage1", resourceType: ResourceTypes.Energy, amount: 50);
        var snapshot = CreateSnapshot(
            new ObjectData("creep1", RoomObjectTypes.Creep, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 0 }, StoreCapacity: 100),
            new ObjectData("storage1", RoomObjectTypes.Storage, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 200 }));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Withdraw_InsufficientResourceInTarget_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.Withdraw, actorId: "creep1", targetId: "storage1", resourceType: ResourceTypes.Energy, amount: 250);
        var snapshot = CreateSnapshot(
            new ObjectData("creep1", RoomObjectTypes.Creep, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 0 }, StoreCapacity: 300),
            new ObjectData("storage1", RoomObjectTypes.Storage, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 200 }));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.InsufficientResource, result.ErrorCode);
    }

    [Fact]
    public void Validate_Withdraw_ActorCapacityFull_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.Withdraw, actorId: "creep1", targetId: "storage1", resourceType: ResourceTypes.Energy, amount: 50);
        var snapshot = CreateSnapshot(
            new ObjectData("creep1", RoomObjectTypes.Creep, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 100 }, StoreCapacity: 100),
            new ObjectData("storage1", RoomObjectTypes.Storage, UserId: "user1", Store: new Dictionary<string, int> { [ResourceTypes.Energy] = 200 }));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.ActorCapacityFull, result.ErrorCode);
    }

    #endregion

    #region Helper Methods

    private static IntentRecord CreateIntent(string intentName, string actorId, string? targetId = null, string? resourceType = null, int? amount = null, BodyPartType? bodyPartType = null)
    {
        var fields = new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.ActorId] = new(IntentFieldValueKind.Text, TextValue: actorId)
        };

        if (targetId is not null)
            fields[IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetId);

        if (resourceType is not null)
            fields[IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: resourceType);

        if (amount.HasValue)
            fields[IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: amount.Value);

        if (bodyPartType.HasValue)
            fields["bodyPartType"] = new(IntentFieldValueKind.Number, NumberValue: (int)bodyPartType.Value);

        var intent = new IntentRecord(
            Name: intentName,
            Arguments: [new IntentArgument(fields)]);

        return intent;
    }

    private sealed record ObjectData(
        string Id,
        string Type,
        string? UserId = null,
        Dictionary<string, int>? Store = null,
        IReadOnlyList<CreepBodyPartSnapshot>? BodyParts = null,
        int? StoreCapacity = null,
        int? Hits = null,
        int? HitsMax = null);

    private static RoomSnapshot CreateSnapshot(params ObjectData[] objects)
    {
        var objectDict = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal);

        foreach (var obj in objects) {
            var snapshot = new RoomObjectSnapshot(
                Id: obj.Id,
                Type: obj.Type,
                RoomName: "W1N1",
                Shard: "shard0",
                UserId: obj.UserId,
                X: 25,
                Y: 25,
                Hits: obj.Hits,
                HitsMax: obj.HitsMax,
                Fatigue: null,
                TicksToLive: null,
                Name: null,
                Level: null,
                Density: null,
                MineralType: null,
                DepositType: null,
                StructureType: obj.Type,
                Store: obj.Store ?? [],
                StoreCapacity: obj.StoreCapacity,
                StoreCapacityResource: new Dictionary<string, int>(),
                Reservation: null,
                Sign: null,
                Structure: null,
                Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
                Body: obj.BodyParts ?? []);

            objectDict[obj.Id] = snapshot;
        }

        var roomSnapshot = new RoomSnapshot(
            RoomName: "W1N1",
            GameTime: 100,
            Info: null,
            Objects: objectDict,
            Users: new Dictionary<string, UserState>(),
            Intents: null,
            Terrain: new Dictionary<string, RoomTerrainSnapshot>(),
            Flags: []);

        return roomSnapshot;
    }

    #endregion
}
