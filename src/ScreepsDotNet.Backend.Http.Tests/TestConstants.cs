namespace ScreepsDotNet.Backend.Http.Tests;

internal static class VersionTestValues
{
    public const int Protocol = 14;
    public const bool UseNativeAuth = true;
    public const int Users = 1;
    public const string PackageVersion = "test-build";
    public const string WelcomeText = "Test Welcome";
    public const int HistoryChunkSize = 20;
    public const int SocketUpdateThrottle = 100;
}

internal static class AuthTestValues
{
    public const string UserId = "test-user";
    public const string Username = "TestUser";
    public const string Email = "test@screeps.local";
    public const string Ticket = "test-ticket";
    public const string SteamId = "90071992547409920";
}
