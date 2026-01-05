using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Endpoints;
using ScreepsDotNet.Backend.Core.Storage;
using ScreepsDotNet.Storage.MongoRedis.Adapters;
using ScreepsDotNet.Storage.MongoRedis.Options;
using ScreepsDotNet.Storage.MongoRedis.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.Configure<ServerInfoOptions>(builder.Configuration.GetSection(ServerInfoOptions.SectionName));
builder.Services.Configure<MongoRedisStorageOptions>(builder.Configuration.GetSection(MongoRedisStorageOptions.SectionName));
builder.Services.AddSingleton<IStorageAdapter, MongoRedisStorageAdapter>();
builder.Services.AddSingleton<IServerInfoRepository, MongoServerInfoRepository>();
builder.Services.AddSingleton<IServerInfoProvider, InMemoryServerInfoProvider>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.MapBackendEndpoints();
app.Run();
