using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Backend.Cli.Application;
using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Core.Storage;
using ScreepsDotNet.Storage.MongoRedis.Adapters;
using ScreepsDotNet.Storage.MongoRedis.Options;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Seeding;
using ScreepsDotNet.Storage.MongoRedis.Services;

var builder = Host.CreateApplicationBuilder(args);
ConfigureConfiguration(builder, args);
ConfigureLogging(builder);
ConfigureServices(builder);

using var host = builder.Build();
await host.StartAsync();

var application = host.Services.GetRequiredService<ICliApplication>();
var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
var exitCode = await application.RunAsync(args, lifetime.ApplicationStopping);

await host.StopAsync();
return exitCode;

static void ConfigureConfiguration(HostApplicationBuilder builder, string[] args)
{
    builder.Configuration.Sources.Clear();
    builder.Configuration.AddEnvironmentVariables(prefix: "SCREEPSCLI_");
    builder.Configuration.AddCommandLine(args);
}

static void ConfigureLogging(HostApplicationBuilder builder)
{
    builder.Logging.ClearProviders();
    builder.Logging.AddSimpleConsole(options => {
        options.SingleLine = true;
    });
    builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.Extensions.Hosting.Internal.Host", LogLevel.Warning);
}

static void ConfigureServices(HostApplicationBuilder builder)
{
    builder.Services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);
    builder.Services.Configure<MongoRedisStorageOptions>(builder.Configuration.GetSection(MongoRedisStorageOptions.SectionName));
    builder.Services.Configure<BotManifestOptions>(options =>
    {
        options.ManifestFile = builder.Configuration["modfile"]
                               ?? builder.Configuration["MODFILE"]
                               ?? Environment.GetEnvironmentVariable("MODFILE");
    });

    builder.Services.AddSingleton<IStorageAdapter, MongoRedisStorageAdapter>();
    builder.Services.AddSingleton<IMongoDatabaseProvider, MongoDatabaseProvider>();
    builder.Services.AddSingleton<IRedisConnectionProvider, RedisConnectionProvider>();
    builder.Services.AddSingleton<IUserRepository, MongoUserRepository>();
    builder.Services.AddSingleton<IRoomRepository, MongoRoomRepository>();
    builder.Services.AddSingleton<IUserWorldRepository, MongoUserWorldRepository>();
    builder.Services.AddSingleton<IServerDataRepository, MongoServerDataRepository>();
    builder.Services.AddSingleton<IUserCodeRepository, MongoUserCodeRepository>();
    builder.Services.AddSingleton<IUserMemoryRepository, MongoUserMemoryRepository>();
    builder.Services.AddSingleton<IUserConsoleRepository, MongoUserConsoleRepository>();
    builder.Services.AddSingleton<IUserMoneyRepository, MongoUserMoneyRepository>();
    builder.Services.AddSingleton<IMarketOrderRepository, MongoMarketOrderRepository>();
    builder.Services.AddSingleton<IMarketStatsRepository, MongoMarketStatsRepository>();
    builder.Services.AddSingleton<IRoomStatusRepository, MongoRoomStatusRepository>();
    builder.Services.AddSingleton<IRoomTerrainRepository, MongoRoomTerrainRepository>();
    builder.Services.AddSingleton<IWorldStatsRepository, MongoWorldStatsRepository>();
    builder.Services.AddSingleton<IWorldMetadataRepository, MongoWorldMetadataRepository>();
    builder.Services.AddSingleton<IVersionMetadataRepository, MongoVersionMetadataRepository>();
    builder.Services.AddSingleton<IUserRespawnService, MongoUserRespawnService>();
    builder.Services.AddSingleton<IPlayerSpawnService, MongoPlayerSpawnService>();
    builder.Services.AddSingleton<IMapControlService, MongoMapControlService>();
    builder.Services.AddSingleton<IBotDefinitionProvider, FileSystemBotDefinitionProvider>();
    builder.Services.AddSingleton<IBotControlService, MongoBotControlService>();
    builder.Services.AddSingleton<IVersionInfoProvider, VersionInfoProvider>();
    builder.Services.AddSingleton<ISeedDataService, SeedDataService>();
    builder.Services.AddSingleton<ISystemControlService, RedisSystemControlService>();
    builder.Services.AddSingleton<IStrongholdTemplateProvider, EmbeddedStrongholdTemplateProvider>();
    builder.Services.AddSingleton<IStrongholdControlService, MongoStrongholdControlService>();
    builder.Services.AddSingleton<IFlagService, MongoFlagService>();
    builder.Services.AddSingleton<IInvaderService, MongoInvaderService>();

    builder.Services.AddSingleton<ICliApplication, CliApplication>();
}
