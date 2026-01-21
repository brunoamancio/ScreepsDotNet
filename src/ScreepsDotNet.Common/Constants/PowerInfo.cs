using ScreepsDotNet.Common.Types;

namespace ScreepsDotNet.Common.Constants;

/// <summary>
/// Information about a power ability, including level requirements, cooldown, duration, range, ops cost, and effects.
/// </summary>
/// <param name="ClassName">The power class name (e.g., "operator").</param>
/// <param name="Level">Array of 5 level requirements for unlocking each tier of the ability.</param>
/// <param name="Cooldown">Cooldown in ticks between uses (null if not applicable).</param>
/// <param name="Duration">Duration in ticks (null if not applicable or varies by level).</param>
/// <param name="DurationLevels">Duration values for each level (null if duration is constant or not applicable).</param>
/// <param name="Range">Range in tiles (null if not applicable).</param>
/// <param name="Ops">Ops cost (null if not applicable or varies by level).</param>
/// <param name="OpsLevels">Ops cost for each level (null if ops cost is constant or not applicable).</param>
/// <param name="Effect">Effect values for each level (null if not applicable).</param>
/// <param name="EffectMultipliers">Effect multiplier values for each level (null if not applicable).</param>
/// <param name="Energy">Energy cost (null if not applicable).</param>
/// <param name="Period">Period in ticks for periodic effects (null if not applicable).</param>
public record PowerAbilityInfo(
    string ClassName,
    int[] Level,
    int? Cooldown = null,
    int? Duration = null,
    int[]? DurationLevels = null,
    int? Range = null,
    int? Ops = null,
    int[]? OpsLevels = null,
    int[]? Effect = null,
    double[]? EffectMultipliers = null,
    int? Energy = null,
    int? Period = null
);

/// <summary>
/// Static registry of all power abilities and their properties.
/// Maps power type constants to their ability information.
/// </summary>
public static class PowerInfo
{
    /// <summary>
    /// Dictionary mapping power types to their ability information.
    /// </summary>
    public static IReadOnlyDictionary<PowerTypes, PowerAbilityInfo> Abilities { get; } = new Dictionary<PowerTypes, PowerAbilityInfo>
    {
        [PowerTypes.GenerateOps] = new(
            ClassName: PowerClass.Operator,
            Level: [0, 2, 7, 14, 22],
            Cooldown: 50,
            Effect: [1, 2, 4, 6, 8]
        ),
        [PowerTypes.OperateSpawn] = new(
            ClassName: PowerClass.Operator,
            Level: [0, 2, 7, 14, 22],
            Cooldown: 300,
            Duration: 1000,
            Range: 3,
            Ops: 100,
            EffectMultipliers: [0.9, 0.7, 0.5, 0.35, 0.2]
        ),
        [PowerTypes.OperateTower] = new(
            ClassName: PowerClass.Operator,
            Level: [0, 2, 7, 14, 22],
            Cooldown: 10,
            Duration: 100,
            Range: 3,
            Ops: 10,
            EffectMultipliers: [1.1, 1.2, 1.3, 1.4, 1.5]
        ),
        [PowerTypes.OperateStorage] = new(
            ClassName: PowerClass.Operator,
            Level: [0, 2, 7, 14, 22],
            Cooldown: 800,
            Duration: 1000,
            Range: 3,
            Ops: 100,
            Effect: [500000, 1000000, 2000000, 4000000, 7000000]
        ),
        [PowerTypes.OperateLab] = new(
            ClassName: PowerClass.Operator,
            Level: [0, 2, 7, 14, 22],
            Cooldown: 50,
            Duration: 1000,
            Range: 3,
            Ops: 10,
            Effect: [2, 4, 6, 8, 10]
        ),
        [PowerTypes.OperateExtension] = new(
            ClassName: PowerClass.Operator,
            Level: [0, 2, 7, 14, 22],
            Cooldown: 50,
            Range: 3,
            Ops: 2,
            EffectMultipliers: [0.2, 0.4, 0.6, 0.8, 1.0]
        ),
        [PowerTypes.OperateObserver] = new(
            ClassName: PowerClass.Operator,
            Level: [0, 2, 7, 14, 22],
            Cooldown: 400,
            DurationLevels: [200, 400, 600, 800, 1000],
            Range: 3,
            Ops: 10
        ),
        [PowerTypes.OperateTerminal] = new(
            ClassName: PowerClass.Operator,
            Level: [0, 2, 7, 14, 22],
            Cooldown: 500,
            Duration: 1000,
            Range: 3,
            Ops: 100,
            EffectMultipliers: [0.9, 0.8, 0.7, 0.6, 0.5]
        ),
        [PowerTypes.DisruptSpawn] = new(
            ClassName: PowerClass.Operator,
            Level: [0, 2, 7, 14, 22],
            Cooldown: 5,
            Range: 20,
            Ops: 10,
            DurationLevels: [1, 2, 3, 4, 5]
        ),
        [PowerTypes.DisruptTower] = new(
            ClassName: PowerClass.Operator,
            Level: [0, 2, 7, 14, 22],
            Cooldown: 0,
            Duration: 5,
            Range: 50,
            Ops: 10,
            EffectMultipliers: [0.9, 0.8, 0.7, 0.6, 0.5]
        ),
        [PowerTypes.DisruptSource] = new(
            ClassName: PowerClass.Operator,
            Level: [0, 2, 7, 14, 22],
            Cooldown: 100,
            Range: 3,
            Ops: 100,
            DurationLevels: [100, 200, 300, 400, 500]
        ),
        [PowerTypes.Shield] = new(
            ClassName: PowerClass.Operator,
            Level: [0, 2, 7, 14, 22],
            Effect: [5000, 10000, 15000, 20000, 25000],
            Duration: 50,
            Cooldown: 20,
            Energy: 100
        ),
        [PowerTypes.RegenSource] = new(
            ClassName: PowerClass.Operator,
            Level: [10, 11, 12, 14, 22],
            Cooldown: 100,
            Duration: 300,
            Range: 3,
            Effect: [50, 100, 150, 200, 250],
            Period: 15
        ),
        [PowerTypes.RegenMineral] = new(
            ClassName: PowerClass.Operator,
            Level: [10, 11, 12, 14, 22],
            Cooldown: 100,
            Duration: 100,
            Range: 3,
            Effect: [2, 4, 6, 8, 10],
            Period: 10
        ),
        [PowerTypes.DisruptTerminal] = new(
            ClassName: PowerClass.Operator,
            Level: [20, 21, 22, 23, 24],
            Cooldown: 8,
            Duration: 10,
            Range: 50,
            OpsLevels: [50, 40, 30, 20, 10]
        ),
        [PowerTypes.Fortify] = new(
            ClassName: PowerClass.Operator,
            Level: [0, 2, 7, 14, 22],
            Cooldown: 5,
            Range: 3,
            Ops: 5,
            DurationLevels: [1, 2, 3, 4, 5]
        ),
        [PowerTypes.OperatePower] = new(
            ClassName: PowerClass.Operator,
            Level: [10, 11, 12, 14, 22],
            Cooldown: 800,
            Range: 3,
            Duration: 1000,
            Ops: 200,
            Effect: [1, 2, 3, 4, 5]
        ),
        [PowerTypes.OperateController] = new(
            ClassName: PowerClass.Operator,
            Level: [20, 21, 22, 23, 24],
            Cooldown: 800,
            Range: 3,
            Duration: 1000,
            Ops: 200,
            Effect: [10, 20, 30, 40, 50]
        ),
        [PowerTypes.OperateFactory] = new(
            ClassName: PowerClass.Operator,
            Level: [0, 2, 7, 14, 22],
            Cooldown: 800,
            Range: 3,
            Duration: 1000,
            Ops: 100,
            Effect: [1, 2, 3, 4, 5]
        )
    };
}
