using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Validation.Models;
using ScreepsDotNet.Engine.Validation.Validators;

namespace ScreepsDotNet.Engine.Tests.Validation.Validators;

public sealed class PermissionValidatorTests
{
    private readonly PermissionValidator _validator = new();

    // Controller Ownership Tests (8 tests)

    [Fact]
    public void Validate_UpgradeController_OwnedByActor_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.UpgradeController, actorId: "creep1", targetId: "controller1");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1"),
            ("controller1", RoomObjectTypes.Controller, "user1"));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Validate_UpgradeController_NotOwned_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.UpgradeController, actorId: "creep1", targetId: "controller1");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1"),
            ("controller1", RoomObjectTypes.Controller, "user2"));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.ControllerNotOwned, result.ErrorCode);
    }

    [Fact]
    public void Validate_UpgradeController_ReservedByActor_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.UpgradeController, actorId: "creep1", targetId: "controller1");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1", (string?)null),
            ("controller1", RoomObjectTypes.Controller, null, "user1"));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Validate_UpgradeController_ReservedByOther_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.UpgradeController, actorId: "creep1", targetId: "controller1");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1", (string?)null),
            ("controller1", RoomObjectTypes.Controller, null, "user2"));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.ControllerNotReservedByActor, result.ErrorCode);
    }

    [Fact]
    public void Validate_AttackController_EnemyController_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.AttackController, actorId: "creep1", targetId: "controller1");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1"),
            ("controller1", RoomObjectTypes.Controller, "user2"));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Validate_AttackController_OwnController_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.AttackController, actorId: "creep1", targetId: "controller1");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1"),
            ("controller1", RoomObjectTypes.Controller, "user1"));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.ControllerNotOwned, result.ErrorCode);
    }

    [Fact]
    public void Validate_ReserveController_NeutralController_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.ReserveController, actorId: "creep1", targetId: "controller1");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1"),
            ("controller1", RoomObjectTypes.Controller, null));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Validate_ReserveController_OwnedController_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.ReserveController, actorId: "creep1", targetId: "controller1");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1"),
            ("controller1", RoomObjectTypes.Controller, "user2"));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.NotOwnedOrReserved, result.ErrorCode);
    }

    // Safe Mode Tests (5 tests)

    [Fact]
    public void Validate_Attack_SafeModeActive_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.Attack, actorId: "creep1", targetId: "creep2");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1", (int?)null),
            ("creep2", RoomObjectTypes.Creep, "user2", (int?)null),
            ("controller1", RoomObjectTypes.Controller, "user2", 100));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.SafeModeActive, result.ErrorCode);
    }

    [Fact]
    public void Validate_Attack_SafeModeInactive_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.Attack, actorId: "creep1", targetId: "creep2");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1", (int?)null),
            ("creep2", RoomObjectTypes.Creep, "user2", (int?)null),
            ("controller1", RoomObjectTypes.Controller, "user2", 0));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Validate_Attack_OwnerAttacksOwnCreep_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.Attack, actorId: "creep1", targetId: "creep2");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1", (int?)null),
            ("creep2", RoomObjectTypes.Creep, "user1", (int?)null),
            ("controller1", RoomObjectTypes.Controller, "user1", 100));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Validate_RangedAttack_SafeModeActive_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.RangedAttack, actorId: "creep1", targetId: "creep2");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1", (int?)null),
            ("creep2", RoomObjectTypes.Creep, "user2", (int?)null),
            ("controller1", RoomObjectTypes.Controller, "user2", 50));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.SafeModeActive, result.ErrorCode);
    }

    [Fact]
    public void Validate_Dismantle_SafeModeDoesNotBlock_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.Dismantle, actorId: "creep1", targetId: "spawn1");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1", (int?)null),
            ("spawn1", RoomObjectTypes.Spawn, "user2", (int?)null),
            ("controller1", RoomObjectTypes.Controller, "user2", 100));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    // Rampart Access Control Tests (4 tests)

    [Fact]
    public void Validate_Repair_PublicRampart_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.Repair, actorId: "creep1", targetId: "rampart1");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1", (bool?)null),
            ("rampart1", RoomObjectTypes.Rampart, "user2", true));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Validate_Repair_PrivateRampartNotOwned_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.Repair, actorId: "creep1", targetId: "rampart1");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1", (bool?)null),
            ("rampart1", RoomObjectTypes.Rampart, "user2", false));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.RampartBlocking, result.ErrorCode);
    }

    [Fact]
    public void Validate_Transfer_OwnedRampart_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.Transfer, actorId: "creep1", targetId: "container1");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1", (bool?)null, 10, 10),
            ("container1", RoomObjectTypes.Container, "user1", (bool?)null, 10, 20),
            ("rampart1", RoomObjectTypes.Rampart, "user1", false, 10, 20));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Validate_Withdraw_RampartBlocksNonOwner_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.Withdraw, actorId: "creep1", targetId: "container1");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1", (bool?)null, 10, 10),
            ("container1", RoomObjectTypes.Container, "user2", (bool?)null, 15, 25),
            ("rampart1", RoomObjectTypes.Rampart, "user2", false, 15, 25));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.RampartBlocking, result.ErrorCode);
    }

    // Harvest Permission Tests (3 tests)

    [Fact]
    public void Validate_Harvest_OwnedRoom_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.Harvest, actorId: "creep1", targetId: "source1");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1"),
            ("source1", RoomObjectTypes.Source, null),
            ("controller1", RoomObjectTypes.Controller, "user1"));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Validate_Harvest_ReservedRoom_ReturnsValid()
    {
        var intent = CreateIntent(IntentKeys.Harvest, actorId: "creep1", targetId: "source1");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1", (string?)null),
            ("source1", RoomObjectTypes.Source, null, (string?)null),
            ("controller1", RoomObjectTypes.Controller, null, "user1"));

        var result = _validator.Validate(intent, snapshot);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Validate_Harvest_HostileRoom_ReturnsInvalid()
    {
        var intent = CreateIntent(IntentKeys.Harvest, actorId: "creep1", targetId: "source1");
        var snapshot = CreateSnapshot(
            ("creep1", RoomObjectTypes.Creep, "user1"),
            ("source1", RoomObjectTypes.Source, null),
            ("controller1", RoomObjectTypes.Controller, "user2"));

        var result = _validator.Validate(intent, snapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.HostileRoom, result.ErrorCode);
    }

    // Helper Methods

    private static IntentRecord CreateIntent(string intentName, string actorId, string? targetId = null)
    {
        var fields = new Dictionary<string, IntentFieldValue>
        {
            [IntentKeys.ActorId] = new(IntentFieldValueKind.Text, TextValue: actorId)
        };

        if (targetId is not null)
            fields[IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetId);

        var intentRecord = new IntentRecord(intentName, [new IntentArgument(fields)]);
        return intentRecord;
    }

    private static RoomSnapshot CreateSnapshot(params (string Id, string Type, string? UserId)[] objects)
    {
        var objectDict = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal);
        var maxSafeMode = 0;

        foreach (var (id, type, userId) in objects)
        {
            var obj = new RoomObjectSnapshot(
                Id: id,
                Type: type,
                RoomName: "W1N1",
                Shard: null,
                UserId: userId,
                X: 10,
                Y: 10,
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
                Store: new Dictionary<string, int>(StringComparer.Ordinal),
                StoreCapacity: null,
                StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
                Reservation: null,
                Sign: null,
                Structure: null,
                Effects: new Dictionary<ScreepsDotNet.Common.Types.PowerTypes, PowerEffectSnapshot>(0),
                Body: [],
                IsPublic: null,
                SafeMode: null);

            objectDict[id] = obj;
        }

        var snapshot = new RoomSnapshot(
            RoomName: "W1N1",
            GameTime: maxSafeMode,
            Info: null,
            Objects: objectDict,
            Users: new Dictionary<string, UserState>(StringComparer.Ordinal),
            Intents: null,
            Terrain: new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            Flags: []);

        return snapshot;
    }

    private static RoomSnapshot CreateSnapshot(params (string Id, string Type, string? UserId, string? reservationUserId)[] objects)
    {
        var objectDict = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal);

        foreach (var (id, type, userId, reservationUserId) in objects)
        {
            var obj = new RoomObjectSnapshot(
                Id: id,
                Type: type,
                RoomName: "W1N1",
                Shard: null,
                UserId: userId,
                X: 10,
                Y: 10,
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
                Store: new Dictionary<string, int>(StringComparer.Ordinal),
                StoreCapacity: null,
                StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
                Reservation: reservationUserId is not null ? new RoomReservationSnapshot(reservationUserId, 1000) : null,
                Sign: null,
                Structure: null,
                Effects: new Dictionary<ScreepsDotNet.Common.Types.PowerTypes, PowerEffectSnapshot>(0),
                Body: [],
                IsPublic: null,
                SafeMode: null);

            objectDict[id] = obj;
        }

        var snapshot = new RoomSnapshot(
            RoomName: "W1N1",
            GameTime: 0,
            Info: null,
            Objects: objectDict,
            Users: new Dictionary<string, UserState>(StringComparer.Ordinal),
            Intents: null,
            Terrain: new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            Flags: []);

        return snapshot;
    }

    private static RoomSnapshot CreateSnapshot(params (string Id, string Type, string? UserId, int? safeMode)[] objects)
    {
        var objectDict = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal);
        var maxSafeMode = 0;

        foreach (var (id, type, userId, safeMode) in objects)
        {
            var obj = new RoomObjectSnapshot(
                Id: id,
                Type: type,
                RoomName: "W1N1",
                Shard: null,
                UserId: userId,
                X: 10,
                Y: 10,
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
                Store: new Dictionary<string, int>(StringComparer.Ordinal),
                StoreCapacity: null,
                StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
                Reservation: null,
                Sign: null,
                Structure: null,
                Effects: new Dictionary<ScreepsDotNet.Common.Types.PowerTypes, PowerEffectSnapshot>(0),
                Body: [],
                IsPublic: null,
                SafeMode: safeMode);

            objectDict[id] = obj;

            if (safeMode.HasValue && safeMode.Value > maxSafeMode)
                maxSafeMode = safeMode.Value;
        }

        var snapshot = new RoomSnapshot(
            RoomName: "W1N1",
            GameTime: maxSafeMode > 0 ? maxSafeMode - 10 : 0,
            Info: null,
            Objects: objectDict,
            Users: new Dictionary<string, UserState>(StringComparer.Ordinal),
            Intents: null,
            Terrain: new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            Flags: []);

        return snapshot;
    }

    private static RoomSnapshot CreateSnapshot(params (string Id, string Type, string? UserId, bool? isPublic)[] objects)
    {
        var objectDict = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal);

        foreach (var (id, type, userId, isPublic) in objects)
        {
            var obj = new RoomObjectSnapshot(
                Id: id,
                Type: type,
                RoomName: "W1N1",
                Shard: null,
                UserId: userId,
                X: 10,
                Y: 10,
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
                Store: new Dictionary<string, int>(StringComparer.Ordinal),
                StoreCapacity: null,
                StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
                Reservation: null,
                Sign: null,
                Structure: null,
                Effects: new Dictionary<ScreepsDotNet.Common.Types.PowerTypes, PowerEffectSnapshot>(0),
                Body: [],
                IsPublic: isPublic,
                SafeMode: null);

            objectDict[id] = obj;
        }

        var snapshot = new RoomSnapshot(
            RoomName: "W1N1",
            GameTime: 0,
            Info: null,
            Objects: objectDict,
            Users: new Dictionary<string, UserState>(StringComparer.Ordinal),
            Intents: null,
            Terrain: new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            Flags: []);

        return snapshot;
    }

    private static RoomSnapshot CreateSnapshot(params (string Id, string Type, string? UserId, bool? isPublic, int x, int y)[] objects)
    {
        var objectDict = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal);

        foreach (var (id, type, userId, isPublic, x, y) in objects)
        {
            var obj = new RoomObjectSnapshot(
                Id: id,
                Type: type,
                RoomName: "W1N1",
                Shard: null,
                UserId: userId,
                X: x,
                Y: y,
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
                Store: new Dictionary<string, int>(StringComparer.Ordinal),
                StoreCapacity: null,
                StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
                Reservation: null,
                Sign: null,
                Structure: null,
                Effects: new Dictionary<ScreepsDotNet.Common.Types.PowerTypes, PowerEffectSnapshot>(0),
                Body: [],
                IsPublic: isPublic,
                SafeMode: null);

            objectDict[id] = obj;
        }

        var snapshot = new RoomSnapshot(
            RoomName: "W1N1",
            GameTime: 0,
            Info: null,
            Objects: objectDict,
            Users: new Dictionary<string, UserState>(StringComparer.Ordinal),
            Intents: null,
            Terrain: new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            Flags: []);

        return snapshot;
    }
}
