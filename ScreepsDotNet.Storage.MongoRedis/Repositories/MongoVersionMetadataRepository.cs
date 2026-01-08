namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoVersionMetadataRepository(IMongoDatabaseProvider databaseProvider, ILogger<MongoVersionMetadataRepository> logger) : IVersionMetadataRepository
{
    private readonly IMongoCollection<VersionMetadataDocument> _collection = databaseProvider.GetCollection<VersionMetadataDocument>(databaseProvider.Settings.VersionInfoCollection);

    public async Task<VersionMetadata> GetAsync(CancellationToken cancellationToken = default)
    {
        var document = await _collection.Find(doc => doc.Id == VersionMetadataDocument.DefaultId)
                                        .FirstOrDefaultAsync(cancellationToken)
                                        .ConfigureAwait(false);

        if (document is not null)
            return new VersionMetadata(document.Protocol, document.UseNativeAuth, document.PackageVersion);

        logger.LogWarning("Version metadata document missing; falling back to hard-coded defaults.");
        return new VersionMetadata(SeedDataDefaults.Version.Protocol,
                                   SeedDataDefaults.Version.UseNativeAuth,
                                   SeedDataDefaults.Version.PackageVersion);
    }
}
