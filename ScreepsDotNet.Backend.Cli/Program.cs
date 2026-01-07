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
    builder.Services.Configure<ServerDataOptions>(builder.Configuration.GetSection(ServerDataOptions.SectionName));
    builder.Services.Configure<VersionInfoOptions>(builder.Configuration.GetSection(VersionInfoOptions.SectionName));
    builder.Services.Configure<MongoRedisStorageOptions>(builder.Configuration.GetSection(MongoRedisStorageOptions.SectionName));

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
builder.Services.AddSingleton<IUserRespawnService, MongoUserRespawnService>();
builder.Services.AddSingleton<IVersionInfoProvider, VersionInfoProvider>();

builder.Services.AddSingleton<ICliApplication, CliApplication>();
}
