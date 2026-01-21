using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Engine.Processors.Steps;

/// <summary>
/// Applies passive mineral regeneration with density changes and PWR_REGEN_MINERAL power effect handling.
/// </summary>
internal sealed class MineralRegenerationStep : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var gameTime = context.State.GameTime;

        foreach (var mineral in context.State.Objects.Values) {
            if (mineral.Type != RoomObjectTypes.Mineral)
                continue;

            ProcessMineral(context, mineral, gameTime);
        }

        return Task.CompletedTask;
    }

    private static void ProcessMineral(RoomProcessorContext context, RoomObjectSnapshot mineral, int gameTime)
    {
        var mineralAmount = mineral.MineralAmount ?? 0;
        var density = mineral.Density ?? ScreepsGameConstants.DensityModerate;

        // Build patch with all needed changes
        int? newNextRegenerationTime = null;
        int? newMineralAmount = null;
        int? newDensity = null;
        var hasRegenerationChange = false;
        var hasPowerEffectChange = false;

        // 1. Handle passive regeneration
        if (mineralAmount == 0) {
            if (!mineral.NextRegenerationTime.HasValue) {
                // Set initial regeneration time
                newNextRegenerationTime = gameTime + ScreepsGameConstants.MineralRegenTime;
                hasRegenerationChange = true;
            }
            else if (gameTime >= mineral.NextRegenerationTime.Value - 1) {
                // Regeneration time reached
                newNextRegenerationTime = null;

                // Create deterministic seed from game time and mineral ID
                var randomSeed = ComputeSeed(gameTime, mineral.Id);

                // Determine new density (and regenerate mineral amount)
                var shouldChangeDensity = ShouldChangeDensity(density, randomSeed);
                if (shouldChangeDensity) {
                    var newDensityValue = SelectNewDensity(density, randomSeed);
                    newDensity = newDensityValue;
                    newMineralAmount = ScreepsGameConstants.MineralDensityAmounts[newDensityValue];
                }
                else {
                    // Density stays the same - only set mineral amount
                    newMineralAmount = ScreepsGameConstants.MineralDensityAmounts[density];
                }

                hasRegenerationChange = true;
            }
        }

        // 2. Handle PWR_REGEN_MINERAL effect
        if (mineralAmount > 0 && mineral.Effects.TryGetValue(PowerTypes.RegenMineral, out var regenEffect) && regenEffect.EndTime > gameTime) {
            var powerInfo = PowerInfo.Abilities[PowerTypes.RegenMineral];
            if (powerInfo.Period.HasValue) {
                var period = powerInfo.Period.Value;
                var ticksUntilEnd = regenEffect.EndTime - gameTime;

                if (ticksUntilEnd % period == 0) {
                    var effectAmount = powerInfo.Effect![regenEffect.Level - 1];
                    var currentAmount = newMineralAmount ?? mineralAmount;
                    newMineralAmount = currentAmount + effectAmount;
                    hasPowerEffectChange = true;
                }
            }
        }

        // Emit single patch if any changes
        if (hasRegenerationChange || hasPowerEffectChange) {
            var patch = new RoomObjectPatchPayload
            {
                MineralAmount = newMineralAmount,
                Density = newDensity,
                NextRegenerationTime = newNextRegenerationTime
            };
            context.MutationWriter.Patch(mineral.Id, patch);
        }
    }

    private static int ComputeSeed(int gameTime, string mineralId)
    {
        // Create deterministic seed from game time and mineral ID hash
        var hash = mineralId.GetHashCode();
        var seed = gameTime ^ hash;
        return seed;
    }

    private static bool ShouldChangeDensity(int density, int randomSeed)
    {
        // DENSITY_LOW and DENSITY_ULTRA always change
        if (density is ScreepsGameConstants.DensityLow or ScreepsGameConstants.DensityUltra)
            return true;

        // DENSITY_MODERATE and DENSITY_HIGH change with MINERAL_DENSITY_CHANGE probability (5%)
        var random = new Random(randomSeed);
        var roll = random.NextDouble();
        var shouldChange = roll < ScreepsGameConstants.MineralDensityChange;
        return shouldChange;
    }

    private static int SelectNewDensity(int currentDensity, int randomSeed)
    {
        var random = new Random(randomSeed);
        int newDensity;

        // Keep selecting until we get a different density
        do {
            var roll = random.NextDouble();

            // Use cumulative probability distribution
            newDensity = roll < ScreepsGameConstants.MineralDensityProbability[1]
                ? ScreepsGameConstants.DensityLow
                : roll < ScreepsGameConstants.MineralDensityProbability[2]
                ? ScreepsGameConstants.DensityModerate
                : roll < ScreepsGameConstants.MineralDensityProbability[3]
                ? ScreepsGameConstants.DensityHigh
                : ScreepsGameConstants.DensityUltra;
        } while (newDensity == currentDensity);

        return newDensity;
    }
}
