namespace ScreepsDotNet.Backend.Core.Constants;

using System.Collections.Generic;

public static class PowerConstants
{
    public const double PowerLevelMultiply = 1000;
    public const double PowerLevelPow = 2;
    public const int PowerCreepMaxLevel = 25;
    public const int PowerCreepDeleteCooldownMilliseconds = 24 * 3600 * 1000;
    public const int PowerCreepSpawnCooldownMilliseconds = 8 * 3600 * 1000;
    public const int PowerExperimentationCooldownMilliseconds = 24 * 3600 * 1000;
    public const int MaxPowerLevelPerAbility = 5;

    public static class Classes
    {
        public const string Operator = "operator";

        public static readonly IReadOnlyCollection<string> All = [Operator];
    }

    public static class PowerIds
    {
        public const string GenerateOps = "1";
        public const string OperateSpawn = "2";
        public const string OperateTower = "3";
        public const string OperateStorage = "4";
        public const string OperateLab = "5";
        public const string OperateExtension = "6";
        public const string OperateObserver = "7";
        public const string OperateTerminal = "8";
        public const string DisruptSpawn = "9";
        public const string DisruptTower = "10";
        public const string DisruptSource = "11";
        public const string Shield = "12";
        public const string RegenSource = "13";
        public const string RegenMineral = "14";
        public const string DisruptTerminal = "15";
        public const string OperatePower = "16";
        public const string Fortify = "17";
        public const string OperateController = "18";
        public const string OperateFactory = "19";
    }

    public static readonly IReadOnlyDictionary<string, PowerInfoDefinition> PowerInfo =
        new Dictionary<string, PowerInfoDefinition>(StringComparer.Ordinal)
        {
            [PowerIds.GenerateOps] = new(Classes.Operator, [0, 2, 7, 14, 22]),
            [PowerIds.OperateSpawn] = new(Classes.Operator, [0, 2, 7, 14, 22]),
            [PowerIds.OperateTower] = new(Classes.Operator, [0, 2, 7, 14, 22]),
            [PowerIds.OperateStorage] = new(Classes.Operator, [0, 2, 7, 14, 22]),
            [PowerIds.OperateLab] = new(Classes.Operator, [0, 2, 7, 14, 22]),
            [PowerIds.OperateExtension] = new(Classes.Operator, [0, 2, 7, 14, 22]),
            [PowerIds.OperateObserver] = new(Classes.Operator, [0, 2, 7, 14, 22]),
            [PowerIds.OperateTerminal] = new(Classes.Operator, [0, 2, 7, 14, 22]),
            [PowerIds.DisruptSpawn] = new(Classes.Operator, [0, 2, 7, 14, 22]),
            [PowerIds.DisruptTower] = new(Classes.Operator, [0, 2, 7, 14, 22]),
            [PowerIds.DisruptSource] = new(Classes.Operator, [0, 2, 7, 14, 22]),
            [PowerIds.Shield] = new(Classes.Operator, [0, 2, 7, 14, 22]),
            [PowerIds.RegenSource] = new(Classes.Operator, [10, 11, 12, 14, 22]),
            [PowerIds.RegenMineral] = new(Classes.Operator, [10, 11, 12, 14, 22]),
            [PowerIds.DisruptTerminal] = new(Classes.Operator, [20, 21, 22, 23, 24]),
            [PowerIds.OperatePower] = new(Classes.Operator, [10, 11, 12, 14, 22]),
            [PowerIds.Fortify] = new(Classes.Operator, [0, 2, 7, 14, 22]),
            [PowerIds.OperateController] = new(Classes.Operator, [20, 21, 22, 23, 24]),
            [PowerIds.OperateFactory] = new(Classes.Operator, [0, 2, 7, 14, 22])
        };
}

public sealed record PowerInfoDefinition(string ClassName, IReadOnlyList<int> LevelRequirements);
