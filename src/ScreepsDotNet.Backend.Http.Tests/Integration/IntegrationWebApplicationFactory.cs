namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;

internal sealed class IntegrationWebApplicationFactory(string mongoConnectionString,
                                                      string mongoDatabase,
                                                      string redisConnectionString,
                                                      string userId,
                                                      string ticket,
                                                      string steamId)
    : WebApplicationFactory<Program>
{
    private const string IntegrationEnvironmentName = "Integration";
    private const string StorageSection = "Storage:MongoRedis";
    private const string MongoConnectionStringKey = StorageSection + ":MongoConnectionString";
    private const string MongoDatabaseKey = StorageSection + ":MongoDatabase";
    private const string RedisConnectionStringKey = StorageSection + ":RedisConnectionString";
    private const string AuthSection = "Auth";
    private const string UseNativeAuthKey = AuthSection + ":UseNativeAuth";
    private const string TicketsSection = AuthSection + ":Tickets:0";
    private const string TicketKey = TicketsSection + ":Ticket";
    private const string TicketUserIdKey = TicketsSection + ":UserId";
    private const string TicketSteamIdKey = TicketsSection + ":SteamId";
    private const string ManifestFileKey = nameof(BotManifestOptions) + ":ManifestFile";
    private static readonly string ModsManifestPath = Path.Combine(AppContext.BaseDirectory, "TestSupport", "Fixtures", "mods", "test-mods.json");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(IntegrationEnvironmentName);
        builder.ConfigureAppConfiguration((_, configBuilder) => {
            var settings = new Dictionary<string, string?>
            {
                [MongoConnectionStringKey] = mongoConnectionString,
                [MongoDatabaseKey] = mongoDatabase,
                [RedisConnectionStringKey] = redisConnectionString,
                [UseNativeAuthKey] = "false",
                [TicketKey] = ticket,
                [TicketUserIdKey] = userId,
                [TicketSteamIdKey] = steamId,
                [ManifestFileKey] = ModsManifestPath
            };
            configBuilder.AddInMemoryCollection(settings);
        });
        builder.ConfigureServices(services => {
            services.RemoveAll<IBotDefinitionProvider>();
            services.AddSingleton<IBotDefinitionProvider, StaticBotDefinitionProvider>();
        });
    }
}
