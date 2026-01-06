namespace ScreepsDotNet.Backend.Http.Tests.Integration;

internal static class IntegrationTestValues
{
    internal static class Database
    {
        public const string Name = "screeps_integration";
    }

    internal static class User
    {
        public const string Id = "integration-user";
        public const string Username = "IntegrationUser";
    }

    internal static class Auth
    {
        public const string Ticket = "integration-ticket";
        public const string SteamId = "integration-steam";
    }

    internal static class World
    {
        public const string StartRoom = "W20N20";
    }

    internal static class Money
    {
        public const string Description = "Sold energy";
        public const int Change = 5000;
        public const int Balance = 15000;
        public const string Type = "market.sell";
    }

    internal static class Console
    {
        public const string Expression = "console.log('integration test');";
    }

    internal static class Memory
    {
        public const int SegmentId = 5;
        public const string SegmentValue = "integration-segment";
    }

    internal static class ServerData
    {
        public const string WelcomeText = "<h4>Integration Harness</h4>";
        public const int HistoryChunkSize = 42;
        public const int SocketUpdateThrottle = 250;

        public static Dictionary<string, object> CreateCustomObjectTypes()
            => new(StringComparer.Ordinal)
            {
                ["testObject"] = "demo"
            };

        public static Dictionary<string, object> CreateRendererResources()
            => new(StringComparer.Ordinal)
            {
                ["sprite"] = "integration.png"
            };

        public static Dictionary<string, object> CreateRendererMetadata()
            => new(StringComparer.Ordinal)
            {
                ["build"] = "integration"
            };
    }
}
