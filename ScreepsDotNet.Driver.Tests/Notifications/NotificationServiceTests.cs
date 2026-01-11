using MongoDB.Driver;
using ScreepsDotNet.Driver.Abstractions.Notifications;
using ScreepsDotNet.Driver.Services.Notifications;
using ScreepsDotNet.Driver.Tests.TestSupport;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using StackExchange.Redis;

namespace ScreepsDotNet.Driver.Tests.Notifications;

public sealed class NotificationServiceTests(MongoRedisFixture fixture) : IClassFixture<MongoRedisFixture>
{
    private readonly MongoRedisFixture _fixture = fixture;

    private NotificationService CreateService() => new(_fixture.MongoProvider, _fixture.RedisProvider);
    private IMongoCollection<UserNotificationDocument> NotificationCollection
        => _fixture.GetCollection<UserNotificationDocument>(_fixture.Options.UserNotificationsCollection);

    private static TaskCompletionSource<string> CreateTcs()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task SendNotificationAsync_GroupsByInterval()
    {
        var service = CreateService();
        await NotificationCollection.DeleteManyAsync(FilterDefinition<UserNotificationDocument>.Empty);

        await service.SendNotificationAsync("user1", "hello world", new NotificationOptions(5, "msg"));
        await service.SendNotificationAsync("user1", "hello world", new NotificationOptions(5, "msg"));

        var doc = await NotificationCollection.Find(document => document.UserId == "user1").FirstOrDefaultAsync();

        Assert.NotNull(doc);
        Assert.Equal(2, doc!.Count);
    }

    [Fact]
    public async Task PublishConsoleMessagesAsync_WritesToRedisChannel()
    {
        var service = CreateService();
        var mux = _fixture.RedisProvider.GetConnection();
        var subscriber = mux.GetSubscriber();
        var tcs = CreateTcs();
        await subscriber.SubscribeAsync(RedisChannel.Literal("user:user2/console"), (_, value) => tcs.TrySetResult(value!));

        var payload = new ConsoleMessagesPayload(["log-entry"], ["res"]);
        await service.PublishConsoleMessagesAsync("user2", payload);

        var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("log-entry", result);
        await subscriber.UnsubscribeAsync(RedisChannel.Literal("user:user2/console"));
    }

    [Fact]
    public async Task PublishConsoleErrorAsync_PersistsNotificationAndPublishes()
    {
        var service = CreateService();
        await NotificationCollection.DeleteManyAsync(FilterDefinition<UserNotificationDocument>.Empty);

        var mux = _fixture.RedisProvider.GetConnection();
        var subscriber = mux.GetSubscriber();
        var tcs = CreateTcs();
        await subscriber.SubscribeAsync(RedisChannel.Literal("user:err/console"), (_, value) => tcs.TrySetResult(value!));

        await service.PublishConsoleErrorAsync("err", "stack trace");

        var payload = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("stack trace", payload);

        var doc = await NotificationCollection.Find(d => d.UserId == "err" && d.Type == "error").FirstOrDefaultAsync();
        Assert.NotNull(doc);
        Assert.Equal(1, doc!.Count);

        await subscriber.UnsubscribeAsync(RedisChannel.Literal("user:err/console"));
    }

    [Fact]
    public async Task NotifyRoomsDoneAsync_PublishesGameTime()
    {
        var service = CreateService();
        var mux = _fixture.RedisProvider.GetConnection();
        var subscriber = mux.GetSubscriber();
        var tcs = CreateTcs();
        await subscriber.SubscribeAsync(RedisChannel.Literal("roomsDone"), (_, value) => tcs.TrySetResult(value!));

        await service.NotifyRoomsDoneAsync(12345);

        var payload = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("12345", payload);
        await subscriber.UnsubscribeAsync(RedisChannel.Literal("roomsDone"));
    }
}
