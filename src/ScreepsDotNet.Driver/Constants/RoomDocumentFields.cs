namespace ScreepsDotNet.Driver.Constants;

using ScreepsDotNet.Common.Constants;

/// <summary>
/// Canonical Mongo document field names for rooms and room objects.
/// </summary>
public static class RoomDocumentFields
{
    public static class RoomObject
    {
        public const string Id = "_id";
        public const string Hits = "hits";
        public const string HitsMax = "hitsMax";
        public const string Fatigue = "fatigue";
        public const string TicksToLive = "ticksToLive";
        public const string StructureHits = "_structureHits";
        public const string DowngradeTimer = "downgradeTimer";
        public const string UpgradeBlocked = "upgradeBlocked";
        public const string SpawnCooldownTime = "spawnCooldownTime";
        public const string X = "x";
        public const string Y = "y";
        public const string InterRoom = "interRoom";
        public const string ActionLog = "_actionLog";

        public static class ActionLogFields
        {
            public const string Die = "die";
            public const string Time = "time";
        }

        public static class Store
        {
            public const string Root = "store";
            public const string Energy = "energy";
        }
    }

    public static class Info
    {
        public const string Status = "status";
        public const string Novice = "novice";
        public const string RespawnArea = "respawnArea";
        public const string OpenTime = "openTime";
        public const string Owner = "owner";
        public const string Controller = RoomObjectTypes.Controller;
        public const string ControllerLevel = "level";
        public const string EnergyAvailable = "energyAvailable";
        public const string NextNpcMarketOrder = "nextNpcMarketOrder";
        public const string PowerBankTime = "powerBankTime";
        public const string InvaderGoal = "invaderGoal";
    }

    public static class RoomStatusValues
    {
        public const string Normal = "normal";
        public const string OutOfBorders = "out of borders";
    }
}
