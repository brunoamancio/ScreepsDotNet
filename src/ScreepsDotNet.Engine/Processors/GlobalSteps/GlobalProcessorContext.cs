namespace ScreepsDotNet.Engine.Processors.GlobalSteps;

using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.GlobalMutations;
using ScreepsDotNet.Engine.Data.Models;

/// <summary>
/// Shared context passed to each global processor step for the current tick.
/// Provides convenient lookups over the snapshot plus a mutation writer for driver persistence.
/// </summary>
public sealed class GlobalProcessorContext(GlobalState state, IGlobalMutationWriter mutationWriter)
{
    public GlobalState State { get; } = state ?? throw new ArgumentNullException(nameof(state));
    public IGlobalMutationWriter Mutations { get; } = mutationWriter ?? throw new ArgumentNullException(nameof(mutationWriter));
    public int GameTime => State.GameTime;

    public IReadOnlyDictionary<string, IReadOnlyList<RoomObjectSnapshot>> ObjectsByType { get; } = GroupByType(state.SpecialRoomObjects);
    public IDictionary<string, UserState> UsersById { get; } = new Dictionary<string, UserState>(state.Market.Users, StringComparer.Ordinal);
    public IDictionary<string, PowerCreepSnapshot> PowerCreepsById { get; } = BuildPowerCreepLookup(state.Market.PowerCreeps);
    public IReadOnlyDictionary<string, GlobalUserIntentSnapshot> UserIntentsByUser { get; } = BuildUserIntentLookup(state.Market.UserIntents);

    public IReadOnlyList<RoomObjectSnapshot> GetObjectsOfType(string type)
        => ObjectsByType.TryGetValue(type, out var list) ? list : [];

    public void UpdatePowerCreep(PowerCreepSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Id))
            return;

        PowerCreepsById[snapshot.Id] = snapshot;
    }

    public void RemovePowerCreep(string powerCreepId)
    {
        if (string.IsNullOrWhiteSpace(powerCreepId))
            return;

        PowerCreepsById.Remove(powerCreepId);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<RoomObjectSnapshot>> GroupByType(IEnumerable<RoomObjectSnapshot> objects)
    {
        var dictionary = new Dictionary<string, IReadOnlyList<RoomObjectSnapshot>>(StringComparer.Ordinal);
        foreach (var group in objects.Where(obj => !string.IsNullOrWhiteSpace(obj.Type))
                                     .GroupBy(obj => obj.Type!, StringComparer.Ordinal)) {
            dictionary[group.Key] = [.. group];
        }

        return dictionary;
    }

    private static IDictionary<string, PowerCreepSnapshot> BuildPowerCreepLookup(IEnumerable<PowerCreepSnapshot> creeps)
    {
        var dictionary = new Dictionary<string, PowerCreepSnapshot>(StringComparer.Ordinal);
        foreach (var creep in creeps) {
            if (string.IsNullOrWhiteSpace(creep.Id))
                continue;
            dictionary[creep.Id] = creep;
        }

        return dictionary;
    }

    private static IReadOnlyDictionary<string, GlobalUserIntentSnapshot> BuildUserIntentLookup(IEnumerable<GlobalUserIntentSnapshot> intents)
    {
        var dictionary = new Dictionary<string, GlobalUserIntentSnapshot>(StringComparer.Ordinal);
        foreach (var intent in intents) {
            if (string.IsNullOrWhiteSpace(intent.UserId))
                continue;
            dictionary[intent.UserId!] = intent;
        }

        return dictionary;
    }
}
