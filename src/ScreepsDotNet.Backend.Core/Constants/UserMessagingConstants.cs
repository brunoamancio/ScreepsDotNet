namespace ScreepsDotNet.Backend.Core.Constants;

public static class UserMessagingConstants
{
    public const string NotificationTypeMessage = "msg";

    public const string UserOnlineKeyPrefix = "userOnline:";

    public const int MaxMessageLength = 100 * 1024;

    public const int ThreadFetchLimit = 100;

    public const int NotificationOfflineWindowMinutes = 10;

    public static class MessageTypes
    {
        public const string Incoming = "in";
        public const string Outgoing = "out";
    }
}
