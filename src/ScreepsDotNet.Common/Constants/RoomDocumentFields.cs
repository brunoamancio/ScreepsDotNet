namespace ScreepsDotNet.Common.Constants;

/// <summary>
/// Canonical Mongo document field names for rooms and room objects.
/// </summary>
public static class RoomDocumentFields
{
    public static class RoomObject
    {
        public const string Id = "_id";
        public const string Type = "type";
        public const string Room = "room";
        public const string Shard = "shard";
        public const string User = "user";
        public const string Name = "name";
        public const string X = "x";
        public const string Y = "y";
        public const string StructureType = "structureType";
        public const string Hits = "hits";
        public const string HitsMax = "hitsMax";
        public const string Fatigue = "fatigue";
        public const string TicksToLive = "ticksToLive";
        public const string StructureHits = "_structureHits";
        public const string DowngradeTimer = "downgradeTimer";
        public const string UpgradeBlocked = "upgradeBlocked";
        public const string SpawnCooldownTime = "spawnCooldownTime";
        public const string DeathTime = "deathTime";
        public const string DecayTime = "decayTime";
        public const string CreepId = "creepId";
        public const string CreepName = "creepName";
        public const string CreepTicksToLive = "creepTicksToLive";
        public const string CreepBody = "creepBody";
        public const string CreepSaying = "creepSaying";
        public const string ResourceType = "resourceType";
        public const string Amount = "amount";
        public const string Spawning = "spawning";
        public const string NotifyWhenAttacked = "notifyWhenAttacked";
        public const string InterRoom = "interRoom";
        public const string InvaderHarvested = "invaderHarvested";
        public const string ActionLog = "_actionLog";
        public const string Body = "body";
        public const string UserSummoned = "userSummoned";
        public const string StrongholdId = "strongholdId";
        public const string Progress = "progress";
        public const string ProgressTotal = "progressTotal";
        public const string Reservation = "reservation";
        public const string Effects = "effects";

        public static class ActionLogFields
        {
            public const string Die = "die";
            public const string Time = "time";
        }

        public static class SpawningFields
        {
            public const string Name = "name";
            public const string NeedTime = "needTime";
            public const string SpawnTime = "spawnTime";
            public const string Directions = "directions";
        }

        public static class Store
        {
            public const string Root = "store";
            public const string Capacity = "storeCapacity";
            public const string CapacityResource = "storeCapacityResource";
            public const string Energy = "energy";
        }

        public static class StructureFields
        {
            public const string Root = "structure";
            public const string Id = "id";
            public const string Type = "type";
            public const string Hits = "hits";
            public const string HitsMax = "hitsMax";
            public const string User = "user";
        }

        public static class BodyPart
        {
            public const string Type = "type";
            public const string Hits = "hits";
            public const string Boost = "boost";
        }

        public static class ReservationFields
        {
            public const string User = "user";
            public const string EndTime = "endTime";
        }

        public static class EffectFields
        {
            public const string Effect = "effect";
            public const string Power = "power";
            public const string EndTime = "endTime";
            public const string Duration = "duration";
        }
    }

    public static class Controller
    {
        public const string Level = "level";
        public const string Progress = "progress";
        public const string DowngradeTime = "downgradeTime";
        public const string SafeMode = "safeMode";
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
        public const string Shard = "shard";
    }

    public static class RoomStatusValues
    {
        public const string Normal = "normal";
        public const string Closed = "closed";
        public const string OutOfBorders = "out of borders";
    }

    public static class Ruin
    {
        public const string DestroyTime = "destroyTime";
        public const string DecayTime = "decayTime";
    }
}
