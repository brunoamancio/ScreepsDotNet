using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Validation;
using ScreepsDotNet.Engine.Validation.Models;

namespace ScreepsDotNet.Engine.Tests.Validation;

public sealed class IntentValidationPipelineTests
{
    [Fact]
    public void Validate_EmptyIntentsList_ReturnsEmptyList()
    {
        // Arrange
        var validators = new List<IIntentValidator>();
        var pipeline = new IntentValidationPipeline(validators);
        var intents = new List<IntentRecord>();
        var roomSnapshot = CreateMinimalRoomSnapshot();

        // Act
        var result = pipeline.Validate(intents, roomSnapshot);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Validate_NullRoomSnapshotIntents_ReturnsEmptyList()
    {
        // Arrange
        var validators = new List<IIntentValidator>();
        var pipeline = new IntentValidationPipeline(validators);
        var intents = new List<IntentRecord> { CreateTestIntent("attack") };
        var roomSnapshot = CreateRoomSnapshotWithoutIntents();

        // Act
        var result = pipeline.Validate(intents, roomSnapshot);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Validate_AllIntentsValid_ReturnsAllIntents()
    {
        // Arrange
        var validator = new AlwaysValidValidator();
        var validators = new List<IIntentValidator> { validator };
        var pipeline = new IntentValidationPipeline(validators);

        var intents = new List<IntentRecord>
        {
            CreateTestIntent("attack"),
            CreateTestIntent("harvest"),
            CreateTestIntent("move")
        };

        var roomSnapshot = CreateMinimalRoomSnapshot();

        // Act
        var result = pipeline.Validate(intents, roomSnapshot);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, i => i.Name == "attack");
        Assert.Contains(result, i => i.Name == "harvest");
        Assert.Contains(result, i => i.Name == "move");
    }

    [Fact]
    public void Validate_SomeIntentsInvalid_ReturnsOnlyValidIntents()
    {
        // Arrange
        var validator = new SelectiveValidator(["attack", "move"]); // Only these pass
        var validators = new List<IIntentValidator> { validator };
        var pipeline = new IntentValidationPipeline(validators);

        var intents = new List<IntentRecord>
        {
            CreateTestIntent("attack"),
            CreateTestIntent("harvest"), // Will fail
            CreateTestIntent("move"),
            CreateTestIntent("build") // Will fail
        };

        var roomSnapshot = CreateMinimalRoomSnapshot();

        // Act
        var result = pipeline.Validate(intents, roomSnapshot);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, i => i.Name == "attack");
        Assert.Contains(result, i => i.Name == "move");
        Assert.DoesNotContain(result, i => i.Name == "harvest");
        Assert.DoesNotContain(result, i => i.Name == "build");
    }

    [Fact]
    public void Validate_EarlyExit_StopsAtFirstFailedValidator()
    {
        // Arrange
        var firstValidator = new CountingValidator(shouldPass: true);
        var secondValidator = new CountingValidator(shouldPass: false); // Fails all
        var thirdValidator = new CountingValidator(shouldPass: true);

        var validators = new List<IIntentValidator> { firstValidator, secondValidator, thirdValidator };
        var pipeline = new IntentValidationPipeline(validators);

        var intents = new List<IntentRecord> { CreateTestIntent("attack") };
        var roomSnapshot = CreateMinimalRoomSnapshot();

        // Act
        var result = pipeline.Validate(intents, roomSnapshot);

        // Assert
        Assert.Empty(result); // All intents rejected by second validator
        Assert.Equal(1, firstValidator.CallCount); // First validator called
        Assert.Equal(1, secondValidator.CallCount); // Second validator called (and failed)
        Assert.Equal(0, thirdValidator.CallCount); // Third validator NOT called (early exit)
    }

    [Fact]
    public void Validate_ValidatorOrder_ExecutesInRegistrationOrder()
    {
        // Arrange
        var executionOrder = new List<string>();

        var validator1 = new OrderTrackingValidator("Schema", executionOrder);
        var validator2 = new OrderTrackingValidator("State", executionOrder);
        var validator3 = new OrderTrackingValidator("Range", executionOrder);

        var validators = new List<IIntentValidator> { validator1, validator2, validator3 };
        var pipeline = new IntentValidationPipeline(validators);

        var intents = new List<IntentRecord> { CreateTestIntent("attack") };
        var roomSnapshot = CreateMinimalRoomSnapshot();

        // Act
        var _result = pipeline.Validate(intents, roomSnapshot);

        // Assert
        Assert.Equal(3, executionOrder.Count);
        Assert.Equal("Schema", executionOrder[0]);
        Assert.Equal("State", executionOrder[1]);
        Assert.Equal("Range", executionOrder[2]);
    }

    [Fact]
    public void Validate_MultipleValidators_AllMustPass()
    {
        // Arrange
        var validator1 = new SelectiveValidator(["attack", "harvest"]);
        var validator2 = new SelectiveValidator(["attack", "move"]);

        var validators = new List<IIntentValidator> { validator1, validator2 };
        var pipeline = new IntentValidationPipeline(validators);

        var intents = new List<IntentRecord>
        {
            CreateTestIntent("attack"), // Passes both validators
            CreateTestIntent("harvest"), // Fails validator2
            CreateTestIntent("move") // Fails validator1
        };

        var roomSnapshot = CreateMinimalRoomSnapshot();

        // Act
        var result = pipeline.Validate(intents, roomSnapshot);

        // Assert
        Assert.Single(result);
        Assert.Equal("attack", result[0].Name); // Only "attack" passes both validators
    }

    [Fact]
    public void Validate_NoValidators_AllIntentsPass()
    {
        // Arrange
        var validators = new List<IIntentValidator>(); // No validators registered
        var pipeline = new IntentValidationPipeline(validators);

        var intents = new List<IntentRecord>
        {
            CreateTestIntent("attack"),
            CreateTestIntent("harvest")
        };

        var roomSnapshot = CreateMinimalRoomSnapshot();

        // Act
        var result = pipeline.Validate(intents, roomSnapshot);

        // Assert
        Assert.Equal(2, result.Count);
    }

    // Helper methods
    private static IntentRecord CreateTestIntent(string name)
    {
        var intent = new IntentRecord(name, []);
        return intent;
    }

    private static RoomSnapshot CreateMinimalRoomSnapshot()
    {
        var snapshot = new RoomSnapshot(
            RoomName: "W1N1",
            GameTime: 1000,
            Info: null,
            Objects: new Dictionary<string, RoomObjectSnapshot>(),
            Users: new Dictionary<string, UserState>(),
            Intents: new RoomIntentSnapshot("W1N1", null, new Dictionary<string, IntentEnvelope>()),
            Terrain: new Dictionary<string, RoomTerrainSnapshot>(),
            Flags: []);
        return snapshot;
    }

    private static RoomSnapshot CreateRoomSnapshotWithoutIntents()
    {
        var snapshot = new RoomSnapshot(
            RoomName: "W1N1",
            GameTime: 1000,
            Info: null,
            Objects: new Dictionary<string, RoomObjectSnapshot>(),
            Users: new Dictionary<string, UserState>(),
            Intents: null, // No intents
            Terrain: new Dictionary<string, RoomTerrainSnapshot>(),
            Flags: []);
        return snapshot;
    }

    // Test validator implementations
    private sealed class AlwaysValidValidator : IIntentValidator
    {
        public ValidationResult Validate(IntentRecord intent, RoomSnapshot roomSnapshot)
        {
            var result = ValidationResult.Success;
            return result;
        }
    }

    private sealed class SelectiveValidator(HashSet<string> validIntentNames) : IIntentValidator
    {
        public ValidationResult Validate(IntentRecord intent, RoomSnapshot roomSnapshot)
        {
            var isValid = validIntentNames.Contains(intent.Name);
            var result = isValid ? ValidationResult.Success : ValidationResult.Failure(ValidationErrorCode.ActorNotFound);
            return result;
        }
    }

    private sealed class CountingValidator(bool shouldPass) : IIntentValidator
    {
        public int CallCount { get; private set; }

        public ValidationResult Validate(IntentRecord intent, RoomSnapshot roomSnapshot)
        {
            CallCount++;
            var result = shouldPass ? ValidationResult.Success : ValidationResult.Failure(ValidationErrorCode.ActorNotFound);
            return result;
        }
    }

    private sealed class OrderTrackingValidator(string name, List<string> executionOrder) : IIntentValidator
    {
        public ValidationResult Validate(IntentRecord intent, RoomSnapshot roomSnapshot)
        {
            executionOrder.Add(name);
            var result = ValidationResult.Success;
            return result;
        }
    }
}
