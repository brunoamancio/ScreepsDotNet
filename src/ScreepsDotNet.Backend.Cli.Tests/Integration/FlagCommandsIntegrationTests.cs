namespace ScreepsDotNet.Backend.Cli.Tests.Integration;

using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Cli.Commands.Flag;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Storage.MongoRedis.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using ScreepsDotNet.Storage.MongoRedis.Services;

[Trait("Category", "Integration")]
public sealed class FlagCommandsIntegrationTests(MongoMapIntegrationFixture fixture) : IClassFixture<MongoMapIntegrationFixture>
{
    [Fact]
    public async Task FlagCreateCommand_CreatesFlagInMongo()
    {
        await fixture.ResetAsync();
        var token = TestContext.Current.CancellationToken;
        var service = CreateFlagService();
        var userRepository = new MongoUserRepository(fixture.DatabaseProvider);
        var command = new FlagCreateCommand(service, userRepository);

        var settings = new FlagCreateCommand.Settings
        {
            UserId = SeedDataDefaults.User.Id,
            RoomName = "W1N1",
            X = 25,
            Y = 25,
            Name = "CliFlag",
            Color = Color.Red,
            SecondaryColor = Color.Blue
        };

        var exitCode = await command.ExecuteAsync(null!, settings, token);

        Assert.Equal(0, exitCode);

        var flags = fixture.GetCollection<RoomFlagDocument>("rooms.flags");
        var flag = await flags.Find(f => f.Id == "CliFlag").FirstOrDefaultAsync(token);
        Assert.NotNull(flag);
        Assert.Equal("25|25|1|3", flag.Data);
    }

    [Fact]
    public async Task FlagChangeColorCommand_UpdatesFlagInMongo()
    {
        await fixture.ResetAsync();
        var token = TestContext.Current.CancellationToken;
        var service = CreateFlagService();
        var userRepository = new MongoUserRepository(fixture.DatabaseProvider);

        var flags = fixture.GetCollection<RoomFlagDocument>("rooms.flags");
        await flags.InsertOneAsync(new RoomFlagDocument
        {
            Id = "ColorFlag",
            UserId = SeedDataDefaults.User.Id,
            Room = "W1N1",
            Data = "10|10|1|1"
        }, cancellationToken: token);

        var command = new FlagChangeColorCommand(service, userRepository);
        var settings = new FlagChangeColorCommand.Settings
        {
            UserId = SeedDataDefaults.User.Id,
            RoomName = "W1N1",
            Name = "ColorFlag",
            Color = Color.Purple,
            SecondaryColor = Color.Cyan
        };

        var exitCode = await command.ExecuteAsync(null!, settings, token);

        Assert.Equal(0, exitCode);

        var flag = await flags.Find(f => f.Id == "ColorFlag").FirstOrDefaultAsync(token);
        Assert.Equal("10|10|2|4", flag.Data);
    }

    [Fact]
    public async Task FlagRemoveCommand_DeletesFlagFromMongo()
    {
        await fixture.ResetAsync();
        var token = TestContext.Current.CancellationToken;
        var service = CreateFlagService();
        var userRepository = new MongoUserRepository(fixture.DatabaseProvider);

        var flags = fixture.GetCollection<RoomFlagDocument>("rooms.flags");
        await flags.InsertOneAsync(new RoomFlagDocument
        {
            Id = "RemoveFlag",
            UserId = SeedDataDefaults.User.Id,
            Room = "W1N1",
            Data = "10|10|1|1"
        }, cancellationToken: token);

        var command = new FlagRemoveCommand(service, userRepository);
        var settings = new FlagRemoveCommand.Settings
        {
            UserId = SeedDataDefaults.User.Id,
            RoomName = "W1N1",
            Name = "RemoveFlag"
        };

        var exitCode = await command.ExecuteAsync(null!, settings, token);

        Assert.Equal(0, exitCode);

        var flag = await flags.Find(f => f.Id == "RemoveFlag").FirstOrDefaultAsync(token);
        Assert.Null(flag);
    }

    private MongoFlagService CreateFlagService()
        => new(fixture.DatabaseProvider, NullLogger<MongoFlagService>.Instance);
}
