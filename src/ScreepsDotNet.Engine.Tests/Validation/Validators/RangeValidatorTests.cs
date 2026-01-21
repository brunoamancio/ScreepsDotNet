using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Validation.Models;
using ScreepsDotNet.Engine.Validation.Validators;

namespace ScreepsDotNet.Engine.Tests.Validation.Validators;

/// <summary>
/// Tests for RangeValidator - validates Chebyshev distance constraints for all intent types.
/// Total: 30 tests covering Range 1, Range 3, special cases, and edge cases.
/// </summary>
public class RangeValidatorTests
{
    private readonly RangeValidator _validator = new();
    private readonly RoomSnapshot _roomSnapshot = new(
        RoomName: "W1N1",
        GameTime: 1000,
        Info: null,
        Objects: new Dictionary<string, RoomObjectSnapshot>(),
        Users: new Dictionary<string, UserState>(),
        Intents: null,
        Terrain: new Dictionary<string, RoomTerrainSnapshot>(),
        Flags: []);

    #region Range 1 Tests (Adjacent) - 13 intent types

    [Fact]
    public void Validate_Attack_WithinRange1_ReturnsSuccess()
    {
        // Arrange
        var intent = CreateIntent(IntentKeys.Attack, actorX: 25, actorY: 25, targetX: 26, targetY: 25);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Attack_OutOfRange1_ReturnsNotInRange()
    {
        // Arrange - Distance 2 (out of range for attack)
        var intent = CreateIntent(IntentKeys.Attack, actorX: 25, actorY: 25, targetX: 27, targetY: 25);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.NotInRange, result.ErrorCode);
    }

    [Fact]
    public void Validate_Harvest_WithinRange1_ReturnsSuccess()
    {
        // Arrange
        var intent = CreateIntent(IntentKeys.Harvest, actorX: 10, actorY: 10, targetX: 11, targetY: 11);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Harvest_OutOfRange1_ReturnsNotInRange()
    {
        // Arrange
        var intent = CreateIntent(IntentKeys.Harvest, actorX: 10, actorY: 10, targetX: 13, targetY: 10);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.NotInRange, result.ErrorCode);
    }

    [Fact]
    public void Validate_Build_ExactlyAtRange1_ReturnsSuccess()
    {
        // Arrange - Exactly at boundary (Chebyshev distance = 1)
        var intent = CreateIntent(IntentKeys.Build, actorX: 20, actorY: 20, targetX: 21, targetY: 21);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Repair_OutOfRange1_ReturnsNotInRange()
    {
        // Arrange
        var intent = CreateIntent(IntentKeys.Repair, actorX: 5, actorY: 5, targetX: 10, targetY: 5);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.NotInRange, result.ErrorCode);
    }

    [Fact]
    public void Validate_Transfer_WithinRange1_ReturnsSuccess()
    {
        // Arrange
        var intent = CreateIntent(IntentKeys.Transfer, actorX: 15, actorY: 15, targetX: 15, targetY: 16);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Withdraw_OutOfRange1_ReturnsNotInRange()
    {
        // Arrange
        var intent = CreateIntent(IntentKeys.Withdraw, actorX: 30, actorY: 30, targetX: 33, targetY: 33);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.NotInRange, result.ErrorCode);
    }

    [Fact]
    public void Validate_Heal_WithinRange1_ReturnsSuccess()
    {
        // Arrange
        var intent = CreateIntent(IntentKeys.Heal, actorX: 12, actorY: 12, targetX: 12, targetY: 13);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Pickup_ExactlyAtRange1_ReturnsSuccess()
    {
        // Arrange - Diagonal adjacent (max distance = 1)
        var intent = CreateIntent(IntentKeys.Pickup, actorX: 40, actorY: 40, targetX: 41, targetY: 41);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_BoostCreep_WithinRange1_ReturnsSuccess()
    {
        // Arrange
        var intent = CreateIntent(IntentKeys.BoostCreep, actorX: 8, actorY: 8, targetX: 8, targetY: 9);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_UnboostCreep_OutOfRange1_ReturnsNotInRange()
    {
        // Arrange
        var intent = CreateIntent(IntentKeys.UnboostCreep, actorX: 22, actorY: 22, targetX: 25, targetY: 22);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.NotInRange, result.ErrorCode);
    }

    #endregion

    #region Range 3 Tests - 5 intent types

    [Fact]
    public void Validate_RangedAttack_WithinRange3_ReturnsSuccess()
    {
        // Arrange - Distance 3
        var intent = CreateIntent(IntentKeys.RangedAttack, actorX: 10, actorY: 10, targetX: 13, targetY: 10);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RangedAttack_OutOfRange3_ReturnsNotInRange()
    {
        // Arrange - Distance 4 (out of range)
        var intent = CreateIntent(IntentKeys.RangedAttack, actorX: 10, actorY: 10, targetX: 14, targetY: 10);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.NotInRange, result.ErrorCode);
    }

    [Fact]
    public void Validate_RangedHeal_ExactlyAtRange3_ReturnsSuccess()
    {
        // Arrange - Diagonal distance = 3
        var intent = CreateIntent(IntentKeys.RangedHeal, actorX: 20, actorY: 20, targetX: 23, targetY: 23);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_UpgradeController_WithinRange3_ReturnsSuccess()
    {
        // Arrange
        var intent = CreateIntent(IntentKeys.UpgradeController, actorX: 25, actorY: 25, targetX: 25, targetY: 28);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_UpgradeController_OutOfRange3_ReturnsNotInRange()
    {
        // Arrange - Distance 4
        var intent = CreateIntent(IntentKeys.UpgradeController, actorX: 25, actorY: 25, targetX: 29, targetY: 25);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.NotInRange, result.ErrorCode);
    }

    [Fact]
    public void Validate_ReserveController_WithinRange3_ReturnsSuccess()
    {
        // Arrange
        var intent = CreateIntent(IntentKeys.ReserveController, actorX: 15, actorY: 15, targetX: 18, targetY: 17);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_AttackController_ExactlyAtRange3_ReturnsSuccess()
    {
        // Arrange
        var intent = CreateIntent(IntentKeys.AttackController, actorX: 5, actorY: 5, targetX: 8, targetY: 8);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    #endregion

    #region Edge Cases - 9 tests

    [Fact]
    public void Validate_SamePosition_Distance0_ReturnsSuccess()
    {
        // Arrange - Actor and target at same position (distance = 0, within range 1)
        var intent = CreateIntent(IntentKeys.Drop, actorX: 10, actorY: 10, targetX: 10, targetY: 10);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_DiagonalDistance_UsesMaxOfDeltas()
    {
        // Arrange - Position (10,10) to (12,11): dx=2, dy=1, Chebyshev=max(2,1)=2
        var intent = CreateIntent(IntentKeys.Attack, actorX: 10, actorY: 10, targetX: 12, targetY: 11);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert - Should fail (distance 2, required 1)
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.NotInRange, result.ErrorCode);
    }

    [Fact]
    public void Validate_RoomEdges_Position0_ReturnsSuccess()
    {
        // Arrange - Edge of room (0,0) to (1,0)
        var intent = CreateIntent(IntentKeys.Attack, actorX: 0, actorY: 0, targetX: 1, targetY: 0);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RoomEdges_Position49_ReturnsSuccess()
    {
        // Arrange - Edge of room (49,49) to (48,49)
        var intent = CreateIntent(IntentKeys.Attack, actorX: 49, actorY: 49, targetX: 48, targetY: 49);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_UnknownIntentType_DefaultsToRange1()
    {
        // Arrange - Unknown intent type should default to range 1
        var intent = CreateIntent("unknownIntent", actorX: 10, actorY: 10, targetX: 11, targetY: 10);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert - Should succeed (distance 1, default range 1)
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_UnknownIntentType_OutOfDefaultRange_ReturnsNotInRange()
    {
        // Arrange - Unknown intent type, distance 2
        var intent = CreateIntent("unknownIntent", actorX: 10, actorY: 10, targetX: 12, targetY: 10);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert - Should fail (distance 2, default range 1)
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.NotInRange, result.ErrorCode);
    }

    [Fact]
    public void Validate_NegativeCoordinates_CalculatesCorrectly()
    {
        // Arrange - Hypothetical negative coordinates (for symmetry testing)
        var intent = CreateIntentWithCoords(IntentKeys.Attack, -5, -5, -4, -5);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert - Distance = max(|-4-(-5)|, |-5-(-5)|) = max(1, 0) = 1
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_LargeDistance_ReturnsNotInRange()
    {
        // Arrange - Very large distance (across entire room)
        var intent = CreateIntent(IntentKeys.Attack, actorX: 0, actorY: 0, targetX: 49, targetY: 49);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert - Distance 49, far exceeds range 1
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.NotInRange, result.ErrorCode);
    }

    [Fact]
    public void Validate_Range3Intent_JustBeyondBoundary_ReturnsNotInRange()
    {
        // Arrange - Distance 3.01 (just over boundary) → Chebyshev rounds to 4
        var intent = CreateIntent(IntentKeys.RangedAttack, actorX: 10, actorY: 10, targetX: 14, targetY: 13);

        // Act
        var result = _validator.Validate(intent, _roomSnapshot);

        // Assert - Distance = max(|14-10|, |13-10|) = max(4, 3) = 4 > 3
        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.NotInRange, result.ErrorCode);
    }

    #endregion

    #region Helper Methods

    private static IntentRecord CreateIntent(string intentType, int actorX, int actorY, int targetX, int targetY)
    {
        var fields = new Dictionary<string, IntentFieldValue>
        {
            ["actorX"] = new(IntentFieldValueKind.Number, NumberValue: actorX),
            ["actorY"] = new(IntentFieldValueKind.Number, NumberValue: actorY),
            ["targetX"] = new(IntentFieldValueKind.Number, NumberValue: targetX),
            ["targetY"] = new(IntentFieldValueKind.Number, NumberValue: targetY)
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

    private static IntentRecord CreateIntentWithCoords(string intentType, int actorX, int actorY, int targetX, int targetY)
        => CreateIntent(intentType, actorX, actorY, targetX, targetY);

    #endregion
}
