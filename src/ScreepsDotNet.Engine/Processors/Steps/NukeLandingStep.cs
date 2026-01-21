namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Processes nuke landing mechanics (pre-landing, landing, cleanup).
/// Node.js parity: engine/src/processor/intents/nukes/pretick.js and tick.js
/// </summary>
internal sealed class NukeLandingStep : IRoomProcessorStep
{
    private const string StoreKeyUpgradeBlocked = "upgradeBlocked";

    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var gameTime = context.State.GameTime;

        foreach (var nuke in context.State.Objects.Values) {
            if (nuke.Type != RoomObjectTypes.Nuke)
                continue;

            if (!nuke.NextRegenerationTime.HasValue)
                continue;

            var landTime = nuke.NextRegenerationTime.Value;

            // Cleanup phase: Remove nuke after landing
            if (gameTime >= landTime) {
                context.MutationWriter.Remove(nuke.Id);
                continue;
            }

            // Landing phase: gameTime == landTime - 1
            if (gameTime == landTime - 1) {
                ProcessNukeLanding(context, nuke, gameTime);
            }
        }

        return Task.CompletedTask;
    }

    private static void ProcessNukeLanding(RoomProcessorContext context, RoomObjectSnapshot nuke, int gameTime)
    {
        // Collect all patches per object to avoid duplicates (init-only properties require merging)
        var patches = new Dictionary<string, PatchBuilder>(StringComparer.Ordinal);
        var removals = new HashSet<string>(StringComparer.Ordinal);

        // Phase 1: Kill all creeps instantly, set power creep hits to 0
        foreach (var obj in context.State.Objects.Values) {
            if (obj.Type == RoomObjectTypes.Creep) {
                removals.Add(obj.Id);
            }
            else if (obj.Type == RoomObjectTypes.PowerCreep) {
                GetOrCreatePatchBuilder(patches, obj.Id).Hits = 0;
            }
        }

        // Phase 2: Remove construction sites, energy drops, tombstones, ruins
        foreach (var obj in context.State.Objects.Values) {
            if (obj.Type is RoomObjectTypes.ConstructionSite or
                ResourceTypes.Energy or
                RoomObjectTypes.Tombstone or
                RoomObjectTypes.Ruin) {
                removals.Add(obj.Id);
            }
        }

        // Phase 3: Cancel spawn.spawning if active
        foreach (var obj in context.State.Objects.Values) {
            if (obj.Type == RoomObjectTypes.Spawn && obj.Spawning is not null) {
                GetOrCreatePatchBuilder(patches, obj.Id).ClearSpawning = true;
            }
        }

        // Phase 4: Apply damage in 5x5 area around nuke
        for (var dx = -2; dx <= 2; dx++) {
            for (var dy = -2; dy <= 2; dy++) {
                var targetX = nuke.X + dx;
                var targetY = nuke.Y + dy;
                var range = Math.Max(Math.Abs(dx), Math.Abs(dy));
                var damage = range == 0 ? ScreepsGameConstants.NukeDamageCenter : ScreepsGameConstants.NukeDamageOuter;

                ApplyDamageAtPosition(context, patches, removals, targetX, targetY, damage);
            }
        }

        // Phase 5: Block controller upgrades and cancel safe mode
        var controller = context.State.Objects.Values
            .FirstOrDefault(o => o.Type == RoomObjectTypes.Controller && o.UserId is not null);

        if (controller is not null) {
            var controllerPatch = GetOrCreatePatchBuilder(patches, controller.Id);

            // Block controller upgrades
            controllerPatch.Store = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [StoreKeyUpgradeBlocked] = gameTime + ScreepsGameConstants.ControllerNukeBlockedUpgrade
            };

            // Cancel safe mode if active
            if (controller.SafeMode.HasValue && controller.SafeMode > gameTime) {
                controllerPatch.SafeMode = 0;
            }
        }

        // Apply all removals and patches
        foreach (var id in removals) {
            context.MutationWriter.Remove(id);
        }

        foreach (var (id, builder) in patches) {
            context.MutationWriter.Patch(id, builder.Build());
        }
    }

    private static PatchBuilder GetOrCreatePatchBuilder(Dictionary<string, PatchBuilder> patches, string objectId)
    {
        if (!patches.TryGetValue(objectId, out var builder)) {
            builder = new PatchBuilder();
            patches[objectId] = builder;
        }
        return builder;
    }

    private sealed class PatchBuilder
    {
        public int? Hits { get; set; }
        public bool ClearSpawning { get; set; }
        public Dictionary<string, int>? Store { get; set; }
        public int? SafeMode { get; set; }

        public RoomObjectPatchPayload Build()
        {
            var result = new RoomObjectPatchPayload
            {
                Hits = Hits,
                ClearSpawning = ClearSpawning,
                Store = Store,
                SafeMode = SafeMode
            };
            return result;
        }
    }

    private static void ApplyDamageAtPosition(RoomProcessorContext context, Dictionary<string, PatchBuilder> patches, HashSet<string> removals, int x, int y, int damage)
    {
        // Find all objects at this position
        var objectsAtPos = context.State.Objects.Values
            .Where(o => o.X == x && o.Y == y && !removals.Contains(o.Id))
            .ToList();

        // Find rampart at this position
        var rampart = objectsAtPos.FirstOrDefault(o => o.Type == RoomObjectTypes.Rampart);

        if (rampart is not null) {
            // Rampart absorbs damage first
            var rampartHits = rampart.Hits ?? 0;
            var remainingDamage = damage;

            if (rampartHits > 0) {
                var newRampartHits = Math.Max(0, rampartHits - damage);
                GetOrCreatePatchBuilder(patches, rampart.Id).Hits = newRampartHits;

                remainingDamage = damage - rampartHits;
            }

            if (remainingDamage <= 0)
                return;

            damage = remainingDamage;
        }

        // Apply remaining damage to other structures at this position
        foreach (var obj in objectsAtPos) {
            if (obj.Id == rampart?.Id)
                continue;

            // Power creeps already had hits set to 0 in Phase 1, don't damage them again
            if (obj.Type == RoomObjectTypes.PowerCreep)
                continue;

            if (obj.Hits.HasValue && obj.Hits > 0) {
                var newHits = Math.Max(0, obj.Hits.Value - damage);
                GetOrCreatePatchBuilder(patches, obj.Id).Hits = newHits;
            }
        }
    }
}
