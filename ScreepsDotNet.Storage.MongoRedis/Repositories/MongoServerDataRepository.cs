using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

public sealed class MongoServerDataRepository(IMongoDatabaseProvider databaseProvider) : IServerDataRepository
{
    private readonly IMongoCollection<ServerDataDocument> _collection = databaseProvider.GetCollection<ServerDataDocument>(databaseProvider.Settings.ServerDataCollection);

    public async Task<ServerData> GetServerDataAsync(CancellationToken cancellationToken = default)
    {
        var document = await _collection.Find(doc => doc.Id == ServerDataDocument.DefaultId)
                                        .FirstOrDefaultAsync(cancellationToken)
                                        .ConfigureAwait(false);

        if (document is not null) return ToModel(document);

        var fallback = BuildFallback();
        var replacement = ToDocument(fallback);
        await _collection.ReplaceOneAsync(doc => doc.Id == ServerDataDocument.DefaultId,
                                          replacement, new ReplaceOptions { IsUpsert = true }, cancellationToken)
                         .ConfigureAwait(false);
        return fallback;

    }

    private static ServerData BuildFallback()
        => new(SeedDataDefaults.ServerData.WelcomeText,
               new Dictionary<string, object>(SeedDataDefaults.ServerData.CreateCustomObjectTypes(), StringComparer.Ordinal),
               SeedDataDefaults.ServerData.HistoryChunkSize,
               SeedDataDefaults.ServerData.SocketUpdateThrottle,
               new RendererData(new Dictionary<string, object>(SeedDataDefaults.ServerData.CreateRendererResources(), StringComparer.Ordinal),
                                new Dictionary<string, object>(SeedDataDefaults.ServerData.CreateRendererMetadata(), StringComparer.Ordinal)));

    private static ServerData ToModel(ServerDataDocument document)
    {
        return new ServerData(document.WelcomeText,
                              new Dictionary<string, object>(document.CustomObjectTypes, StringComparer.Ordinal),
                              document.HistoryChunkSize, document.SocketUpdateThrottle,
                              new RendererData(new Dictionary<string, object>(document.Renderer.Resources, StringComparer.Ordinal),
                                               new Dictionary<string, object>(document.Renderer.Metadata, StringComparer.Ordinal)));
    }

    private static ServerDataDocument ToDocument(ServerData data)
        => new()
        {
            WelcomeText = data.WelcomeText,
            CustomObjectTypes = new Dictionary<string, object>(data.CustomObjectTypes, StringComparer.Ordinal),
            HistoryChunkSize = data.HistoryChunkSize,
            SocketUpdateThrottle = data.SocketUpdateThrottle,
            Renderer = new ServerRendererDocument
            {
                Resources = new Dictionary<string, object>(data.Renderer.Resources, StringComparer.Ordinal),
                Metadata = new Dictionary<string, object>(data.Renderer.Metadata, StringComparer.Ordinal)
            }
        };
}
