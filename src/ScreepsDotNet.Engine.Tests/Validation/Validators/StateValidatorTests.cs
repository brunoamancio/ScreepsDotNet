using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Validation.Models;
using ScreepsDotNet.Engine.Validation.Validators;

namespace ScreepsDotNet.Engine.Tests.Validation.Validators;

/// <summary>
/// Tests for StateValidator - validates actor/target object states.
/// Total: 15 tests covering spawning, alive, existence, hits, and store requirements.
/// </summary>
public class StateValidatorTests
{
    private readonly StateValidator _validator = new();

    #region Actor State Tests (6 tests)

    [Fact]
    public void Validate_ActorSpawning_AttackIntent_ReturnsActorSpawning()
    {
        // Arrange
        var actor = CreateObject("creep1", spawning: true);
        var target = CreateObject("creep2");
        var roomSnapshot = CreateRoomSnapshot(actor, target);
        var intent = CreateIntent(IntentKeys.Attack, actorId: "creep1", targetId: "creep2");

        // Act
        var result = _validator.Validate(intent, roomSnapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.ActorSpawning, result.ErrorCode);
    }

    [Fact]
    public void Validate_ActorNotSpawning_AttackIntent_ReturnsSuccess()
    {
        // Arrange
        var actor = CreateObject("creep1", spawning: false);
        var target = CreateObject("creep2");
        var roomSnapshot = CreateRoomSnapshot(actor, target);
        var intent = CreateIntent(IntentKeys.Attack, actorId: "creep1", targetId: "creep2");

        // Act
        var result = _validator.Validate(intent, roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ActorNotFound_ReturnsActorNotFound()
    {
        // Arrange
        var target = CreateObject("creep2");
        var roomSnapshot = CreateRoomSnapshot(target);
        var intent = CreateIntent(IntentKeys.Attack, actorId: "nonexistent", targetId: "creep2");

        // Act
        var result = _validator.Validate(intent, roomSnapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.ActorNotFound, result.ErrorCode);
    }

    [Fact]
    public void Validate_ActorDead_ReturnsActorDead()
    {
        // Arrange
        var actor = CreateObject("creep1", hits: 0);
        var target = CreateObject("creep2");
        var roomSnapshot = CreateRoomSnapshot(actor, target);
        var intent = CreateIntent(IntentKeys.Attack, actorId: "creep1", targetId: "creep2");

        // Act
        var result = _validator.Validate(intent, roomSnapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.ActorDead, result.ErrorCode);
    }

    [Fact]
    public void Validate_ActorNoStore_TransferIntent_ReturnsActorNoStore()
    {
        // Arrange
        var actor = CreateObject("creep1", hasStore: false);
        var target = CreateObject("storage1", hasStore: true);
        var roomSnapshot = CreateRoomSnapshot(actor, target);
        var intent = CreateIntent(IntentKeys.Transfer, actorId: "creep1", targetId: "storage1");

        // Act
        var result = _validator.Validate(intent, roomSnapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.ActorNoStore, result.ErrorCode);
    }

    [Fact]
    public void Validate_ActorHasStore_TransferIntent_ReturnsSuccess()
    {
        // Arrange
        var actor = CreateObject("creep1", hasStore: true);
        var target = CreateObject("storage1", hasStore: true);
        var roomSnapshot = CreateRoomSnapshot(actor, target);
        var intent = CreateIntent(IntentKeys.Transfer, actorId: "creep1", targetId: "storage1");

        // Act
        var result = _validator.Validate(intent, roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    #endregion

    #region Target State Tests (9 tests)

    [Fact]
    public void Validate_TargetNotFound_ReturnsTargetNotFound()
    {
        // Arrange
        var actor = CreateObject("creep1");
        var roomSnapshot = CreateRoomSnapshot(actor);
        var intent = CreateIntent(IntentKeys.Attack, actorId: "creep1", targetId: "nonexistent");

        // Act
        var result = _validator.Validate(intent, roomSnapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.TargetNotFound, result.ErrorCode);
    }

    [Fact]
    public void Validate_TargetIsSelf_ReturnsTargetIsSelf()
    {
        // Arrange
        var actor = CreateObject("creep1");
        var roomSnapshot = CreateRoomSnapshot(actor);
        var intent = CreateIntent(IntentKeys.Attack, actorId: "creep1", targetId: "creep1");

        // Act
        var result = _validator.Validate(intent, roomSnapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.TargetSameAsActor, result.ErrorCode);
    }

    [Fact]
    public void Validate_TargetSpawning_AttackIntent_ReturnsTargetSpawning()
    {
        // Arrange
        var actor = CreateObject("creep1");
        var target = CreateObject("creep2", spawning: true);
        var roomSnapshot = CreateRoomSnapshot(actor, target);
        var intent = CreateIntent(IntentKeys.Attack, actorId: "creep1", targetId: "creep2");

        // Act
        var result = _validator.Validate(intent, roomSnapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.TargetSpawning, result.ErrorCode);
    }

    [Fact]
    public void Validate_TargetNotSpawning_AttackIntent_ReturnsSuccess()
    {
        // Arrange
        var actor = CreateObject("creep1");
        var target = CreateObject("creep2", spawning: false);
        var roomSnapshot = CreateRoomSnapshot(actor, target);
        var intent = CreateIntent(IntentKeys.Attack, actorId: "creep1", targetId: "creep2");

        // Act
        var result = _validator.Validate(intent, roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_TargetNoHits_AttackIntent_ReturnsTargetNoHits()
    {
        // Arrange
        var actor = CreateObject("creep1");
        var target = CreateObject("source1", hasHits: false);
        var roomSnapshot = CreateRoomSnapshot(actor, target);
        var intent = CreateIntent(IntentKeys.Attack, actorId: "creep1", targetId: "source1");

        // Act
        var result = _validator.Validate(intent, roomSnapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.TargetNoHits, result.ErrorCode);
    }

    [Fact]
    public void Validate_TargetHasHits_AttackIntent_ReturnsSuccess()
    {
        // Arrange
        var actor = CreateObject("creep1");
        var target = CreateObject("creep2", hits: 100);
        var roomSnapshot = CreateRoomSnapshot(actor, target);
        var intent = CreateIntent(IntentKeys.Attack, actorId: "creep1", targetId: "creep2");

        // Act
        var result = _validator.Validate(intent, roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_TargetNoStore_TransferIntent_ReturnsTargetNoStore()
    {
        // Arrange
        var actor = CreateObject("creep1", hasStore: true);
        var target = CreateObject("creep2", hasStore: false);
        var roomSnapshot = CreateRoomSnapshot(actor, target);
        var intent = CreateIntent(IntentKeys.Transfer, actorId: "creep1", targetId: "creep2");

        // Act
        var result = _validator.Validate(intent, roomSnapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.TargetNoStore, result.ErrorCode);
    }

    [Fact]
    public void Validate_TargetHasStore_TransferIntent_ReturnsSuccess()
    {
        // Arrange
        var actor = CreateObject("creep1", hasStore: true);
        var target = CreateObject("storage1", hasStore: true);
        var roomSnapshot = CreateRoomSnapshot(actor, target);
        var intent = CreateIntent(IntentKeys.Transfer, actorId: "creep1", targetId: "storage1");

        // Act
        var result = _validator.Validate(intent, roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_IntentWithNoStateRequirements_ReturnsSuccess()
    {
        // Arrange - Intent type with no state requirements (alive actor, no specific state checks)
        var actor = CreateObject("creep1", spawning: false, hits: 100);
        var target = CreateObject("controller1");
        var roomSnapshot = CreateRoomSnapshot(actor, target);
        var intent = CreateIntent("unknownIntent", actorId: "creep1", targetId: "controller1");

        // Act
        var result = _validator.Validate(intent, roomSnapshot);

        // Assert - Should pass because unknownIntent has no additional state requirements
        Assert.True(result.IsValid);
    }

    #endregion

    #region Helper Methods

    private static RoomObjectSnapshot CreateObject(
        string id,
        bool spawning = false,
        int? hits = 100,
        bool hasHits = true,
        bool hasStore = false)
    {
        var snapshot = new RoomObjectSnapshot(
            Id: id,
            Type: "creep",
            RoomName: "W1N1",
            Shard: null,
            UserId: "user1",
            X: 25,
            Y: 25,
            Hits: hasHits ? hits : null,
            HitsMax: hasHits ? 100 : null,
            Fatigue: null,
            TicksToLive: null,
            Name: id,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: hasStore ? new Dictionary<string, int> { ["energy"] = 50 } : null!,
            StoreCapacity: hasStore ? 100 : null,
            StoreCapacityResource: new Dictionary<string, int>(),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<Common.Types.PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            IsSpawning: spawning
        );
        return snapshot;
    }

    private static RoomSnapshot CreateRoomSnapshot(params RoomObjectSnapshot[] objects)
    {
        var objectsDict = objects.ToDictionary(o => o.Id);
        var roomSnapshot = new RoomSnapshot(
            RoomName: "W1N1",
            GameTime: 1000,
            Info: null,
            Objects: objectsDict,
            Users: new Dictionary<string, UserState>(),
            Intents: null,
            Terrain: new Dictionary<string, RoomTerrainSnapshot>(),
            Flags: []
        );
        return roomSnapshot;
    }

    private static IntentRecord CreateIntent(string intentType, string actorId, string targetId)
    {
        var fields = new Dictionary<string, IntentFieldValue>
        {
            ["actorId"] = new(IntentFieldValueKind.Text, TextValue: actorId),
            ["targetId"] = new(IntentFieldValueKind.Text, TextValue: targetId)
        };

        var arguments = new List<IntentArgument>
        {
            new(Fields: fields)
        };

        var intentRecord = new IntentRecord(
            Name: intentType,
            Arguments: arguments
        );
        return intentRecord;
    }

    #endregion
}
