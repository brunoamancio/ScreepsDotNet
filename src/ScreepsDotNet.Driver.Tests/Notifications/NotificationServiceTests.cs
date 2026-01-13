using MongoDB.Driver;
using ScreepsDotNet.Driver.Abstractions.Notifications;
using ScreepsDotNet.Driver.Services.Notifications;
using ScreepsDotNet.Driver.Tests.TestSupport;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;
using StackExchange.Redis;

namespace ScreepsDotNet.Driver.Tests.Notifications;

public sealed class NotificationServiceTests(MongoRedisFixture fixture) : IClassFixture<MongoRedisFixture>
{
    private NotificationService CreateService() => new(fixture.MongoProvider, fixture.RedisProvider);
    private IMongoCollection<UserNotificationDocument> NotificationCollection
        => fixture.GetCollection<UserNotificationDocument>(fixture.Options.UserNotificationsCollection);

    private static TaskCompletionSource<string> CreateTcs()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task SendNotificationAsync_GroupsByInterval()
    {
        var service = CreateService();
        var token = TestContext.Current.CancellationToken;
        await NotificationCollection.DeleteManyAsync(FilterDefinition<UserNotificationDocument>.Empty, cancellationToken: token);

        await service.SendNotificationAsync("user1", "hello world", new NotificationOptions(5, "msg"), token);
        await service.SendNotificationAsync("user1", "hello world", new NotificationOptions(5, "msg"), token);

        var doc = await NotificationCollection.Find(document => document.UserId == "user1").FirstOrDefaultAsync(token);

        Assert.NotNull(doc);
        Assert.Equal(2, doc!.Count);
    }

    [Fact]
    public async Task PublishConsoleMessagesAsync_WritesToRedisChannel()
    {
        var service = CreateService();
        var mux = fixture.RedisProvider.GetConnection();
        var subscriber = mux.GetSubscriber();
        var tcs = CreateTcs();
        var token = TestContext.Current.CancellationToken;
        await subscriber.SubscribeAsync(RedisChannel.Literal("user:user2/console"), (_, value) => tcs.TrySetResult(value!));

        var payload = new ConsoleMessagesPayload(["log-entry"], ["res"]);
        await service.PublishConsoleMessagesAsync("user2", payload, token);

        var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        Assert.Contains("log-entry", result);
        await subscriber.UnsubscribeAsync(RedisChannel.Literal("user:user2/console"));
    }

    [Fact]
    public async Task PublishConsoleErrorAsync_PersistsNotificationAndPublishes()
    {
        var service = CreateService();
        var token = TestContext.Current.CancellationToken;
        await NotificationCollection.DeleteManyAsync(FilterDefinition<UserNotificationDocument>.Empty, cancellationToken: token);

        var mux = fixture.RedisProvider.GetConnection();
        var subscriber = mux.GetSubscriber();
        var tcs = CreateTcs();
        await subscriber.SubscribeAsync(RedisChannel.Literal("user:err/console"), (_, value) => tcs.TrySetResult(value!));

        await service.PublishConsoleErrorAsync("err", "stack trace", token);

        var payload = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        Assert.Contains("stack trace", payload);

        var doc = await NotificationCollection.Find(d => d.UserId == "err" && d.Type == "error").FirstOrDefaultAsync(token);
        Assert.NotNull(doc);
        Assert.Equal(1, doc!.Count);

        await subscriber.UnsubscribeAsync(RedisChannel.Literal("user:err/console"));
    }

    [Fact]
    public async Task NotifyRoomsDoneAsync_PublishesGameTime()
    {
        var service = CreateService();
        var mux = fixture.RedisProvider.GetConnection();
        var subscriber = mux.GetSubscriber();
        var tcs = CreateTcs();
        var token = TestContext.Current.CancellationToken;
        await subscriber.SubscribeAsync(RedisChannel.Literal("roomsDone"), (_, value) => tcs.TrySetResult(value!));

        await service.NotifyRoomsDoneAsync(12345, token);

        var payload = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        Assert.Equal("12345", payload);
        await subscriber.UnsubscribeAsync(RedisChannel.Literal("roomsDone"));
    }
}
