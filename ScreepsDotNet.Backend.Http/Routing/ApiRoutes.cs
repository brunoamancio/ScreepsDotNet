namespace ScreepsDotNet.Backend.Http.Routing;

public static class ApiRoutes
{
    public const string Version = "/api/version";

    public const string AuthSteamTicket = "/api/auth/steam-ticket";

    public const string AuthMe = "/api/auth/me";

    public static class Game
    {
        private const string Base = "/api/game";

        public static class Market
        {
            private const string MarketBase = $"{Base}/market";
            public const string OrdersIndex = $"{MarketBase}/orders-index";
            public const string Orders = $"{MarketBase}/orders";
            public const string MyOrders = $"{MarketBase}/my-orders";
            public const string Stats = $"{MarketBase}/stats";
        }

        public static class World
        {
            public const string MapStats = $"{Base}/map-stats";
            public const string RoomStatus = $"{Base}/room-status";
            public const string RoomTerrain = $"{Base}/room-terrain";
            public const string Rooms = $"{Base}/rooms";
            public const string WorldSize = $"{Base}/world-size";
            public const string Time = $"{Base}/time";
            public const string Tick = $"{Base}/tick";
            public const string PlaceSpawn = $"{Base}/place-spawn";
            public const string CreateConstruction = $"{Base}/create-construction";
            public const string CreateFlag = $"{Base}/create-flag";
            public const string ChangeFlagColor = $"{Base}/change-flag-color";
            public const string RemoveFlag = $"{Base}/remove-flag";
            public const string CreateInvader = $"{Base}/create-invader";
            public const string RemoveInvader = $"{Base}/remove-invader";
        }

        public static class Bot
        {
            private const string BotBase = $"{Base}/bot";
            public const string List = $"{BotBase}/list";
            public const string Spawn = $"{BotBase}/spawn";
            public const string Reload = $"{BotBase}/reload";
            public const string Remove = $"{BotBase}/remove";
        }

        public static class Stronghold
        {
            private const string StrongholdBase = $"{Base}/stronghold";
            public const string Templates = $"{StrongholdBase}/templates";
            public const string Spawn = $"{StrongholdBase}/spawn";
            public const string Expand = $"{StrongholdBase}/expand";
        }

        public static class Map
        {
            private const string MapBase = $"{Base}/map";
            public const string Generate = $"{MapBase}/generate";
            public const string Open = $"{MapBase}/open";
            public const string Close = $"{MapBase}/close";
            public const string Remove = $"{MapBase}/remove";
            public const string AssetsUpdate = $"{MapBase}/assets-update";
            public const string TerrainRefresh = $"{MapBase}/terrain-refresh";
        }

        public static class System
        {
            private const string SystemBase = $"{Base}/system";
            public const string Status = $"{SystemBase}/status";
            public const string Pause = $"{SystemBase}/pause";
            public const string Resume = $"{SystemBase}/resume";
            public const string Tick = $"{SystemBase}/tick";
            public const string TickSet = $"{SystemBase}/tick-set";
            public const string Message = $"{SystemBase}/message";
            public const string Reset = $"{SystemBase}/reset";
        }

        public static class PowerCreeps
        {
            private const string PowerBase = $"{Base}/power-creeps";
            public const string List = $"{PowerBase}/list";
            public const string Create = $"{PowerBase}/create";
            public const string Delete = $"{PowerBase}/delete";
            public const string CancelDelete = $"{PowerBase}/cancel-delete";
            public const string Upgrade = $"{PowerBase}/upgrade";
            public const string Rename = $"{PowerBase}/rename";
            public const string Experimentation = $"{PowerBase}/experimentation";
        }

        public static class Intent
        {
            public const string AddObject = $"{Base}/add-object-intent";
            public const string AddGlobal = $"{Base}/add-global-intent";
        }
    }

    public static class Server
    {
        public const string Info = "/api/server/info";
    }

    public static class User
    {
        private const string Base = "/api/user";
        public const string WorldStartRoom = $"{Base}/world-start-room";
        public const string WorldStatus = $"{Base}/world-status";
        public const string Branches = $"{Base}/branches";
        public const string Code = $"{Base}/code";
        public const string Badge = $"{Base}/badge";
        public const string RespawnProhibitedRooms = $"{Base}/respawn-prohibited-rooms";
        public const string Respawn = $"{Base}/respawn";
        public const string SetActiveBranch = $"{Base}/set-active-branch";
        public const string CloneBranch = $"{Base}/clone-branch";
        public const string DeleteBranch = $"{Base}/delete-branch";
        public const string Memory = $"{Base}/memory";
        public const string MemorySegment = $"{Base}/memory-segment";
        public const string Console = $"{Base}/console";
        public const string Find = $"{Base}/find";
        public const string Stats = $"{Base}/stats";
        public const string Rooms = $"{Base}/rooms";
        public const string NotifyPrefs = $"{Base}/notify-prefs";
        public const string Overview = $"{Base}/overview";
        public const string TutorialDone = $"{Base}/tutorial-done";
        public const string Email = $"{Base}/email";
        public const string MoneyHistory = $"{Base}/money-history";
        public const string BadgeSvg = $"{Base}/badge-svg";
        public const string SetSteamVisible = $"{Base}/set-steam-visible";
        public const string MessagesBase = $"{Base}/messages";
    }
}
