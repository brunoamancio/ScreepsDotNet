namespace ScreepsDotNet.Common.Constants;

/// <summary>
/// Canonical Mongo document field names for user documents (users collection).
/// </summary>
public static class UserDocumentFields
{
    public const string Id = "_id";
    public const string Email = "email";
    public const string EmailDirty = "emailDirty";
    public const string Username = "username";
    public const string UsernameLower = "usernameLower";
    public const string Cpu = "cpu";
    public const string Active = "active";
    public const string Bot = "bot";
    public const string Badge = "badge";
    public const string Password = "password";
    public const string LastRespawnDate = "lastRespawnDate";
    public const string NotifyPrefs = "notifyPrefs";
    public const string Gcl = "gcl";
    public const string Rooms = "rooms";
    public const string Power = "power";
    public const string Money = "money";
    public const string CustomBadge = "customBadge";
    public const string PowerExperimentations = "powerExperimentations";
    public const string PowerExperimentationTime = "powerExperimentationTime";
    public const string Steam = "steam";

    public static class BadgeFields
    {
        public const string Type = "type";
        public const string Color1 = "color1";
        public const string Color2 = "color2";
        public const string Color3 = "color3";
        public const string Flip = "flip";
        public const string Param = "param";
    }

    public static class GclFields
    {
        public const string Level = "level";
        public const string Progress = "progress";
        public const string ProgressTotal = "progressTotal";
    }

    public static class SteamFields
    {
        public const string Id = "id";
        public const string DisplayName = "displayName";
        public const string Ownership = "ownership";
        public const string SteamProfileLinkHidden = "steamProfileLinkHidden";
    }

    public static class NotificationFields
    {
        public const string User = "user";
        public const string Message = "message";
        public const string Date = "date";
        public const string Type = "type";
        public const string Count = "count";
    }
}
