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

    [Fact]
    public async Task SendNotificationAsync_GroupsByInterval()
    {
        var service = CreateService();
        var collection = _fixture.GetCollection<UserNotificationDocument>(_fixture.Options.UserNotificationsCollection);

        await service.SendNotificationAsync("user1", "hello world", new NotificationOptions(5, "msg"));
        await service.SendNotificationAsync("user1", "hello world", new NotificationOptions(5, "msg"));

        var doc = await collection.Find(document => document.UserId == "user1").FirstOrDefaultAsync();

        Assert.NotNull(doc);
        Assert.Equal(2, doc!.Count);
    }

    [Fact]
    public async Task PublishConsoleMessagesAsync_WritesToRedisChannel()
    {
        var service = CreateService();
        var mux = _fixture.RedisProvider.GetConnection();
        var subscriber = mux.GetSubscriber();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await subscriber.SubscribeAsync(RedisChannel.Literal("user:user2/console"), (_, value) => tcs.TrySetResult(value!));

        var payload = new ConsoleMessagesPayload(["log-entry"], ["res"]);
        await service.PublishConsoleMessagesAsync("user2", payload);

        var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("log-entry", result);
    }
}
