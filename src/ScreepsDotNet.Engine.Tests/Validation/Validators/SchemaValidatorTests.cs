using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Validation.Models;
using ScreepsDotNet.Engine.Validation.Validators;

namespace ScreepsDotNet.Engine.Tests.Validation.Validators;

public sealed class SchemaValidatorTests
{
    private readonly SchemaValidator _validator = new();
    private readonly RoomSnapshot _emptySnapshot = CreateEmptySnapshot();

    // Required Fields Tests

    [Fact]
    public void Validate_AttackIntent_MissingId_ReturnsInvalid()
    {
        var intent = new IntentRecord(
            IntentKeys.Attack,
            [new IntentArgument(new Dictionary<string, IntentFieldValue>
            {
                [IntentKeys.ActorId] = new(IntentFieldValueKind.Text, TextValue: "creep1")
                // Missing "id" field
            })]);

        var result = _validator.Validate(intent, _emptySnapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.MissingRequiredField, result.ErrorCode);
    }

    [Fact]
    public void Validate_TransferIntent_MissingResourceType_ReturnsInvalid()
    {
        var intent = new IntentRecord(
            IntentKeys.Transfer,
            [new IntentArgument(new Dictionary<string, IntentFieldValue>
            {
                [IntentKeys.ActorId] = new(IntentFieldValueKind.Text, TextValue: "creep1"),
                [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: "target1"),
                [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: 100)
                // Missing "resourceType" field
            })]);

        var result = _validator.Validate(intent, _emptySnapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.MissingRequiredField, result.ErrorCode);
    }

    [Fact]
    public void Validate_TransferIntent_MissingAmount_ReturnsInvalid()
    {
        var intent = new IntentRecord(
            IntentKeys.Transfer,
            [new IntentArgument(new Dictionary<string, IntentFieldValue>
            {
                [IntentKeys.ActorId] = new(IntentFieldValueKind.Text, TextValue: "creep1"),
                [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: "target1"),
                [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: ResourceTypes.Energy)
                // Missing "amount" field
            })]);

        var result = _validator.Validate(intent, _emptySnapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.MissingRequiredField, result.ErrorCode);
    }

    // Type Validation Tests

    [Fact]
    public void Validate_AttackIntent_IdIsNumber_ReturnsInvalid()
    {
        var intent = new IntentRecord(
            IntentKeys.Attack,
            [new IntentArgument(new Dictionary<string, IntentFieldValue>
            {
                [IntentKeys.ActorId] = new(IntentFieldValueKind.Text, TextValue: "creep1"),
                [IntentKeys.TargetId] = new(IntentFieldValueKind.Number, NumberValue: 123) // Should be Text
            })]);

        var result = _validator.Validate(intent, _emptySnapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.InvalidFieldType, result.ErrorCode);
    }

    [Fact]
    public void Validate_TransferIntent_AmountIsText_ReturnsInvalid()
    {
        var intent = new IntentRecord(
            IntentKeys.Transfer,
            [new IntentArgument(new Dictionary<string, IntentFieldValue>
            {
                [IntentKeys.ActorId] = new(IntentFieldValueKind.Text, TextValue: "creep1"),
                [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: "target1"),
                [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: ResourceTypes.Energy),
                [IntentKeys.Amount] = new(IntentFieldValueKind.Text, TextValue: "100") // Should be Number
            })]);

        var result = _validator.Validate(intent, _emptySnapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.InvalidFieldType, result.ErrorCode);
    }

    // Null/Empty String Validation Tests

    [Fact]
    public void Validate_AttackIntent_EmptyId_ReturnsInvalid()
    {
        var intent = new IntentRecord(
            IntentKeys.Attack,
            [new IntentArgument(new Dictionary<string, IntentFieldValue>
            {
                [IntentKeys.ActorId] = new(IntentFieldValueKind.Text, TextValue: "creep1"),
                [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: "") // Empty string
            })]);

        var result = _validator.Validate(intent, _emptySnapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.InvalidFieldType, result.ErrorCode);
    }

    [Fact]
    public void Validate_AttackIntent_NullId_ReturnsInvalid()
    {
        var intent = new IntentRecord(
            IntentKeys.Attack,
            [new IntentArgument(new Dictionary<string, IntentFieldValue>
            {
                [IntentKeys.ActorId] = new(IntentFieldValueKind.Text, TextValue: "creep1"),
                [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: null) // Null value
            })]);

        var result = _validator.Validate(intent, _emptySnapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.InvalidFieldType, result.ErrorCode);
    }

    // Numeric Validation Tests

    [Fact]
    public void Validate_TransferIntent_NegativeAmount_ReturnsInvalid()
    {
        var intent = new IntentRecord(
            IntentKeys.Transfer,
            [new IntentArgument(new Dictionary<string, IntentFieldValue>
            {
                [IntentKeys.ActorId] = new(IntentFieldValueKind.Text, TextValue: "creep1"),
                [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: "target1"),
                [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: ResourceTypes.Energy),
                [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: -10)
            })]);

        var result = _validator.Validate(intent, _emptySnapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.NegativeAmount, result.ErrorCode);
    }

    // Resource Type Validation Tests

    [Fact]
    public void Validate_TransferIntent_InvalidResourceType_ReturnsInvalid()
    {
        var intent = new IntentRecord(
            IntentKeys.Transfer,
            [new IntentArgument(new Dictionary<string, IntentFieldValue>
            {
                [IntentKeys.ActorId] = new(IntentFieldValueKind.Text, TextValue: "creep1"),
                [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: "target1"),
                [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: "invalidResource"),
                [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: 100)
            })]);

        var result = _validator.Validate(intent, _emptySnapshot);

        Assert.False(result.IsValid);
        Assert.Equal(ValidationErrorCode.InvalidResourceType, result.ErrorCode);
    }

    // Valid Intent Tests

    [Fact]
    public void Validate_AttackIntent_AllFieldsValid_ReturnsValid()
    {
        var intent = new IntentRecord(
            IntentKeys.Attack,
            [new IntentArgument(new Dictionary<string, IntentFieldValue>
            {
                [IntentKeys.ActorId] = new(IntentFieldValueKind.Text, TextValue: "creep1"),
                [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: "target1")
            })]);

        var result = _validator.Validate(intent, _emptySnapshot);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Validate_TransferIntent_AllFieldsValid_ReturnsValid()
    {
        var intent = new IntentRecord(
            IntentKeys.Transfer,
            [new IntentArgument(new Dictionary<string, IntentFieldValue>
            {
                [IntentKeys.ActorId] = new(IntentFieldValueKind.Text, TextValue: "creep1"),
                [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: "target1"),
                [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: ResourceTypes.Energy),
                [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: 100)
            })]);

        var result = _validator.Validate(intent, _emptySnapshot);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Validate_HarvestIntent_AllFieldsValid_ReturnsValid()
    {
        var intent = new IntentRecord(
            IntentKeys.Harvest,
            [new IntentArgument(new Dictionary<string, IntentFieldValue>
            {
                [IntentKeys.ActorId] = new(IntentFieldValueKind.Text, TextValue: "creep1"),
                [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: "source1")
            })]);

        var result = _validator.Validate(intent, _emptySnapshot);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Validate_WithdrawIntent_AllFieldsValid_ReturnsValid()
    {
        var intent = new IntentRecord(
            IntentKeys.Withdraw,
            [new IntentArgument(new Dictionary<string, IntentFieldValue>
            {
                [IntentKeys.ActorId] = new(IntentFieldValueKind.Text, TextValue: "creep1"),
                [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: "container1"),
                [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: ResourceTypes.Hydrogen),
                [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: 50)
            })]);

        var result = _validator.Validate(intent, _emptySnapshot);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Validate_DropIntent_AllFieldsValid_ReturnsValid()
    {
        var intent = new IntentRecord(
            IntentKeys.Drop,
            [new IntentArgument(new Dictionary<string, IntentFieldValue>
            {
                [IntentKeys.ActorId] = new(IntentFieldValueKind.Text, TextValue: "creep1"),
                [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: ResourceTypes.Metal),
                [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: 25)
            })]);

        var result = _validator.Validate(intent, _emptySnapshot);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Validate_IntentWithNoArguments_ReturnsValid()
    {
        var intent = new IntentRecord(IntentKeys.Say, []);

        var result = _validator.Validate(intent, _emptySnapshot);

        // SchemaValidator should allow intents with no arguments (other validators will handle logic)
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    // Helper Methods

    private static RoomSnapshot CreateEmptySnapshot()
        => new(
            "test-room",
            0,
            null,
            new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal),
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            null,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            []);
}
