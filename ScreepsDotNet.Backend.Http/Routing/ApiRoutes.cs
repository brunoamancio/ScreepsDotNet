namespace ScreepsDotNet.Backend.Http.Routing;

public static class ApiRoutes
{
    public const string Version = "/api/version";

    public const string AuthSteamTicket = "/api/auth/steam-ticket";

    public const string AuthMe = "/api/auth/me";

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
