namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoFlagService(IMongoDatabaseProvider databaseProvider, ILogger<MongoFlagService> logger)
    : IFlagService
{
    private const int FlagsLimit = 10000;
    private const int MaxFlagNameLength = 60;

    private readonly IMongoCollection<RoomFlagDocument> _flagsCollection = databaseProvider.GetCollection<RoomFlagDocument>(databaseProvider.Settings.RoomsFlagsCollection ?? "rooms.flags");

    public async Task<FlagResult> CreateFlagAsync(string userId, CreateFlagRequest request, CancellationToken cancellationToken = default)
    {
        if (request.X < 0 || request.X > 49 || request.Y < 0 || request.Y > 49)
            return new FlagResult(FlagResultStatus.InvalidParams, "Invalid coordinates");

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > MaxFlagNameLength)
            return new FlagResult(FlagResultStatus.InvalidParams, $"Flag name must be between 1 and {MaxFlagNameLength} characters");

        // Remove existing flag with the same name across all rooms for this user (parity with legacy)
        // Note: In legacy, it seems the flag name is globally unique across all rooms for all users if it's the _id.
        // Actually, let's check if flags are unique per user.
        // If two users want to name their flag "Flag1", can they?
        // In legacy: `db['rooms.flags'].update({_id: name, user: user._id}, ...)`
        // If User A has "Flag1", then there is a doc with _id: "Flag1", user: "A".
        // If User B tries to create "Flag1", Mongo will fail because _id "Flag1" already exists.
        // So flag names are unique GLOBALLY in backend-local.

        await _flagsCollection.DeleteManyAsync(f => f.Id == request.Name, cancellationToken).ConfigureAwait(false);

        // Check global flag limit for this user
        var count = await _flagsCollection.CountDocumentsAsync(f => f.UserId == userId, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (count >= FlagsLimit)
            return new FlagResult(FlagResultStatus.TooManyFlags, "Global flag limit reached");

        var document = new RoomFlagDocument
        {
            Id = request.Name,
            UserId = userId,
            Room = request.Room,
            Data = $"{request.X}|{request.Y}|{(int)request.Color}|{(int)request.SecondaryColor}"
        };

        try {
            await _flagsCollection.InsertOneAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey) {
            // This shouldn't happen because we deleted it above, but just in case of race conditions.
            return new FlagResult(FlagResultStatus.InvalidParams, "Flag name already exists");
        }

        logger.LogInformation("User {UserId} created flag {FlagName} in {Room} at ({X}, {Y})", userId, request.Name, request.Room, request.X, request.Y);
        return new FlagResult(FlagResultStatus.Success);
    }

    public async Task<FlagResult> ChangeFlagColorAsync(string userId, string room, string name, Color color, Color secondaryColor, CancellationToken cancellationToken = default)
    {
        var flag = await _flagsCollection.Find(f => f.Id == name && f.UserId == userId && f.Room == room)
                                         .FirstOrDefaultAsync(cancellationToken)
                                         .ConfigureAwait(false);

        if (flag is null)
            return new FlagResult(FlagResultStatus.FlagNotFound, "Flag not found");

        var parts = flag.Data?.Split('|');
        if (parts is null || parts.Length < 2)
            return new FlagResult(FlagResultStatus.InvalidParams, "Corrupt flag data");

        var newData = $"{parts[0]}|{parts[1]}|{(int)color}|{(int)secondaryColor}";
        var update = Builders<RoomFlagDocument>.Update.Set(f => f.Data, newData);

        await _flagsCollection.UpdateOneAsync(f => f.Id == name && f.UserId == userId && f.Room == room, update, cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation("User {UserId} changed color of flag {FlagName} in {Room} to {Color}/{SecondaryColor}", userId, name, room, color, secondaryColor);
        return new FlagResult(FlagResultStatus.Success);
    }

    public async Task<FlagResult> RemoveFlagAsync(string userId, string room, string name, CancellationToken cancellationToken = default)
    {
        var result = await _flagsCollection.DeleteOneAsync(f => f.Id == name && f.UserId == userId && f.Room == room, cancellationToken).ConfigureAwait(false);

        if (result.DeletedCount == 0)
            return new FlagResult(FlagResultStatus.FlagNotFound, "Flag not found");

        logger.LogInformation("User {UserId} removed flag {FlagName} in {Room}", userId, name, room);
        return new FlagResult(FlagResultStatus.Success);
    }
}
