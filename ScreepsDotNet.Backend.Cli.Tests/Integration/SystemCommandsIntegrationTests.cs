namespace ScreepsDotNet.Backend.Cli.Tests.Integration;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Cli.Commands.System;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using ScreepsDotNet.Storage.MongoRedis.Seeding;
using Spectre.Console.Cli;
using StackExchange.Redis;
using SystemControlConstants = ScreepsDotNet.Backend.Core.Constants.SystemControlConstants;

public sealed class SystemCommandsIntegrationTests(SystemCommandsIntegrationFixture fixture) : IClassFixture<SystemCommandsIntegrationFixture>
{
    private readonly SystemCommandsIntegrationFixture _fixture = fixture;

    [Fact]
    public async Task PauseAndResumeCommandsToggleFlag()
    {
        await _fixture.ResetStateAsync();
        var service = _fixture.CreateSystemControlService();
        var pauseCommand = new SystemPauseCommand(service);
        var resumeCommand = new SystemResumeCommand(service);

        await pauseCommand.ExecuteAsync(null!, new EmptySettings(), CancellationToken.None);
        var value = await _fixture.RedisConnection.GetDatabase().StringGetAsync(SystemControlConstants.MainLoopPausedKey);
        Assert.Equal("1", value.ToString());

        await resumeCommand.ExecuteAsync(null!, new EmptySettings(), CancellationToken.None);
        value = await _fixture.RedisConnection.GetDatabase().StringGetAsync(SystemControlConstants.MainLoopPausedKey);
        Assert.Equal("0", value.ToString());
    }

    [Fact]
    public async Task TickSetAndGetCommandsRoundTripDuration()
    {
        await _fixture.ResetStateAsync();
        var service = _fixture.CreateSystemControlService();
        var setCommand = new SystemTickSetCommand(service);
        var getCommand = new SystemTickGetCommand(service);

        await setCommand.ExecuteAsync(null!, new SystemTickSetCommand.Settings { DurationMilliseconds = 750 }, CancellationToken.None);
        var stored = await _fixture.RedisConnection.GetDatabase().StringGetAsync(SystemControlConstants.MainLoopMinimumDurationKey);
        Assert.Equal("750", stored.ToString());

        var exitCode = await getCommand.ExecuteAsync(null!, new SystemTickGetCommand.Settings { OutputJson = true }, CancellationToken.None);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task SystemStatusCommandReadsPauseState()
    {
        await _fixture.ResetStateAsync();
        var db = _fixture.RedisConnection.GetDatabase();
        await db.StringSetAsync(SystemControlConstants.MainLoopPausedKey, "1");
        await db.StringSetAsync(SystemControlConstants.MainLoopMinimumDurationKey, "800");

        var service = _fixture.CreateSystemControlService();
        var command = new SystemStatusCommand(service);
        var exitCode = await command.ExecuteAsync(null!, new SystemStatusCommand.Settings { OutputJson = true }, CancellationToken.None);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task SystemMessageCommandPublishesChannel()
    {
        await _fixture.ResetStateAsync();
        var service = _fixture.CreateSystemControlService();
        var command = new SystemMessageCommand(service);
        var subscriber = _fixture.RedisConnection.GetSubscriber();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var channel = new RedisChannel(SystemControlConstants.ServerMessageChannel, RedisChannel.PatternMode.Literal);
        await subscriber.SubscribeAsync(channel,
                                        (_, value) => tcs.TrySetResult(value.ToString()));

        const string payload = "Integration broadcast";
        var exitCode = await command.ExecuteAsync(null!, new SystemMessageCommand.Settings { Message = payload }, CancellationToken.None);
        Assert.Equal(0, exitCode);

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(payload, received);
        await subscriber.UnsubscribeAsync(channel);
    }

    [Fact]
    public async Task SystemResetCommandReseedsDatabase()
    {
        await _fixture.ResetStateAsync();
        var rooms = _fixture.GetCollection<RoomDocument>("rooms");
        await rooms.InsertOneAsync(new RoomDocument { Id = "W99N99", Status = "normal" });

        var resetCommand = new SystemResetCommand(new SeedDataService(), Options.Create(_fixture.StorageOptions), NullLogger<SystemResetCommand>.Instance);
        var exitCode = await resetCommand.ExecuteAsync(null!, new SystemResetCommand.Settings { Force = true, Confirm = "RESET" }, CancellationToken.None);
        Assert.Equal(0, exitCode);

        var exists = await rooms.Find(document => document.Id == "W99N99").AnyAsync();
        Assert.False(exists);
    }

    private sealed class EmptySettings : CommandSettings;
}
