namespace ScreepsDotNet.Backend.Core.Seeding;

using System;
using System.Collections.Generic;

public static class SeedDataDefaults
{
    public static class Database
    {
        public const string Name = "screeps_integration";
    }

    public static class User
    {
        public const string Id = "integration-user";
        public const string Username = "IntegrationUser";
    }

    public static class Auth
    {
        public const string Ticket = "integration-ticket";
        public const string SteamId = "integration-steam";
    }

    public static class World
    {
        public const string StartRoom = "W20N20";
        public const string SecondaryRoom = "W21N21";
        public const string SecondaryShardRoom = "W21N20";
        public const string SecondaryShardName = "shard1";
        public const string InvaderUser = "Invader";
        public const string MineralType = "H";
        public const string SecondaryShardMineralType = "O";
        public const int MineralDensity = 3;
        public const int SafeModeExpiry = 200000;
        public const int SecondaryShardSafeModeExpiry = 150000;
        public const string ControllerSign = "Integration FTW";
        public const string SecondaryShardControllerSign = "Shard One Online";
        public const int GameTime = 123456;
        public const int TickDuration = 650;
    }

    public static class Money
    {
        public const string Description = "Sold energy";
        public const int Change = 5000;
        public const int Balance = 15000;
        public const string Type = "market.sell";
    }

    public static class Power
    {
        public const double Total = 1_000_000;
        public const double Experimentations = 2;
    }

    public static class PowerCreeps
    {
        public const string ActiveId = "64d000000000000000000001";
        public const string DormantId = "64d000000000000000000002";
        public const string ActiveName = "IntegrationOperator";
        public const string DormantName = "BenchOperator";
        public const string ClassName = "operator";
        public const int ActiveX = 20;
        public const int ActiveY = 20;
        public const int ActiveHits = 2800;
        public const int ActiveHitsMax = 3000;
        public const int ActiveTicksToLive = 4500;
        public const int ActiveStoreCapacity = 400;
        public const int ActiveStoreOps = 120;
    }

    public static class Console
    {
        public const string Expression = "console.log('integration test');";
    }

    public static class Memory
    {
        public const int SegmentId = 5;
        public const string SegmentValue = "integration-segment";
    }

    public static class ServerData
    {
        public const string WelcomeText = "<h4>Integration Harness</h4>";
        public const int HistoryChunkSize = 42;
        public const int SocketUpdateThrottle = 250;

        public static Dictionary<string, object?> CreateCustomObjectTypes()
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

    public static class Version
    {
        public const int Protocol = 14;
        public const bool UseNativeAuth = false;
        public const string PackageVersion = "0.0.1-dev";
    }
}
