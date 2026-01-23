namespace ScreepsDotNet.Engine.Tests._Examples;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Practical example showing how to use Console.WriteLine for debugging test failures.
/// Run with: dotnet test --filter "FullyQualifiedName~DebuggingWithOutputExample" --logger "console;verbosity=detailed"
/// </summary>
public sealed class DebuggingWithOutputExample
{
    [Fact]
    public void Example_DebuggingComplexObject()
    {
        // Create a complex object to debug
        var creep = new RoomObjectSnapshot(
            Id: "creep1",
            Type: RoomObjectTypes.Creep,
            RoomName: "W1N1",
            Shard: "shard0",
            UserId: "user1",
            X: 25,
            Y: 25,
            Hits: 100,
            HitsMax: 100,
            Fatigue: 0,
            TicksToLive: 1500,
            Name: "TestCreep",
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal) { ["energy"] = 50 },
            StoreCapacity: 50,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [
                new CreepBodyPartSnapshot(BodyPartType.Move, 100, null),
                new CreepBodyPartSnapshot(BodyPartType.Work, 100, null),
                new CreepBodyPartSnapshot(BodyPartType.Carry, 100, null)
            ],
            Spawning: null);

        // Debug output - will appear in test results
        Console.WriteLine("=== Creep State ===");
        Console.WriteLine($"ID: {creep.Id}");
        Console.WriteLine($"Position: ({creep.X}, {creep.Y}) in {creep.RoomName}");
        Console.WriteLine($"Health: {creep.Hits}/{creep.HitsMax}");
        Console.WriteLine($"Energy: {creep.Store.GetValueOrDefault("energy", 0)}/{creep.StoreCapacity}");
        Console.WriteLine($"Body Parts: {creep.Body.Count}");

        foreach (var part in creep.Body) {
            Console.WriteLine($"  - {part.Type}: {part.Hits} HP");
        }

        Console.WriteLine("===================");

        // Assertions
        Assert.Equal(3, creep.Body.Count);
        Assert.Equal(50, creep.Store["energy"]);
    }

    [Fact]
    public void Example_DebuggingIntentProcessing()
    {
        var targetId = "target1";
        var damage = 30;

        Console.WriteLine($"Processing attack intent:");
        Console.WriteLine($"  Target: {targetId}");
        Console.WriteLine($"  Damage: {damage}");

        // Simulate calculation
        var bodyParts = 1;
        var calculatedDamage = bodyParts * 30;

        Console.WriteLine($"  Body Parts: {bodyParts}");
        Console.WriteLine($"  Calculated Damage: {calculatedDamage}");

        Assert.Equal(damage, calculatedDamage);
    }

    [Fact]
    public void Example_DebuggingCollectionOperations()
    {
        var mutations = new List<(string Id, int Damage)>
        {
            ("target1", 30),
            ("target2", 60),
            ("target3", 90)
        };

        Console.WriteLine($"Processing {mutations.Count} mutations:");

        foreach (var (id, damage) in mutations) {
            Console.WriteLine($"  [{id}] damage={damage}");
        }

        var totalDamage = mutations.Sum(m => m.Damage);
        Console.WriteLine($"Total damage: {totalDamage}");

        Assert.Equal(180, totalDamage);
    }

    [Fact]
    public void Example_DebuggingConditionalLogic()
    {
        var bodyParts = new[]
        {
            new CreepBodyPartSnapshot(BodyPartType.Move, 0, null),      // Destroyed
            new CreepBodyPartSnapshot(BodyPartType.Attack, 100, null),  // Active
            new CreepBodyPartSnapshot(BodyPartType.Attack, 0, null),    // Destroyed
            new CreepBodyPartSnapshot(BodyPartType.Move, 100, null)     // Active
        };

        Console.WriteLine("Analyzing body parts:");
        Console.WriteLine($"Total parts: {bodyParts.Length}");

        var activeParts = 0;
        var destroyedParts = 0;

        foreach (var part in bodyParts) {
            var status = part.Hits > 0 ? "ACTIVE" : "DESTROYED";
            Console.WriteLine($"  {part.Type} ({part.Hits} HP) - {status}");

            if (part.Hits > 0)
                activeParts++;
            else
                destroyedParts++;
        }

        Console.WriteLine($"Summary: {activeParts} active, {destroyedParts} destroyed");

        Assert.Equal(2, activeParts);
        Assert.Equal(2, destroyedParts);
    }
}
