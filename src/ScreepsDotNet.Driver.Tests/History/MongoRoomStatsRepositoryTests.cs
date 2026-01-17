using MongoDB.Driver;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Driver.Services.History;
using ScreepsDotNet.Driver.Tests.TestSupport;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Driver.Tests.History;

public sealed class MongoRoomStatsRepositoryTests(MongoRedisFixture fixture) : IClassFixture<MongoRedisFixture>
{
    [Fact]
    public async Task AppendAsync_UpsertsDocument()
    {
        var repository = new MongoRoomStatsRepository(fixture.MongoProvider);
        var token = TestContext.Current.CancellationToken;

        var metrics = new Dictionary<string, IReadOnlyDictionary<string, int>>
        {
            ["user1"] = new Dictionary<string, int> { [RoomStatsMetricNames.EnergyHarvested] = 500 }
        };

        var update = new RoomStatsUpdate("W5S2", 12345, metrics);
        await repository.AppendAsync(update, token);

        var collection = fixture.GetCollection<RoomStatsDocument>(fixture.Options.RoomStatsCollection);
        var document = await collection.Find(doc => doc.Room == "W5S2" && doc.Tick == 12345)
                                       .FirstOrDefaultAsync(token);

        Assert.NotNull(document);
        Assert.Equal("W5S2:12345", document!.Id);
        Assert.Equal(500, document.Metrics["user1"][RoomStatsMetricNames.EnergyHarvested]);
    }
}
