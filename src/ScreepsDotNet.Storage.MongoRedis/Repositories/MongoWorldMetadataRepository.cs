namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

using System.Globalization;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoWorldMetadataRepository(IMongoDatabaseProvider databaseProvider) : IWorldMetadataRepository
{
    private const int DefaultGameTime = 0;
    private const int DefaultTickDuration = 500;

    private readonly IMongoCollection<WorldInfoDocument> _worldInfoCollection = databaseProvider.GetCollection<WorldInfoDocument>(databaseProvider.Settings.WorldInfoCollection);
    private readonly IMongoCollection<RoomDocument> _roomsCollection = databaseProvider.GetCollection<RoomDocument>(databaseProvider.Settings.RoomsCollection);

    public async Task<int> GetGameTimeAsync(CancellationToken cancellationToken = default)
    {
        var document = await GetWorldInfoAsync(cancellationToken).ConfigureAwait(false);
        return document?.GameTime ?? DefaultGameTime;
    }

    public async Task<int> GetTickDurationAsync(CancellationToken cancellationToken = default)
    {
        var document = await GetWorldInfoAsync(cancellationToken).ConfigureAwait(false);
        return document?.TickDuration ?? DefaultTickDuration;
    }

    public async Task<WorldSize> GetWorldSizeAsync(CancellationToken cancellationToken = default)
    {
        var roomIds = await _roomsCollection.Find(FilterDefinition<RoomDocument>.Empty)
                                            .Project(room => room.Id)
                                            .ToListAsync(cancellationToken)
                                            .ConfigureAwait(false);

        if (roomIds.Count == 0)
            return new WorldSize(0, 0);

        var coordinates = roomIds.Select(ParseRoomName)
                                 .Where(coord => coord.HasValue)
                                 .Select(coord => coord!.Value)
                                 .ToList();

        if (coordinates.Count == 0)
            return new WorldSize(0, 0);

        var minX = coordinates.Min(coord => coord.X);
        var maxX = coordinates.Max(coord => coord.X);
        var minY = coordinates.Min(coord => coord.Y);
        var maxY = coordinates.Max(coord => coord.Y);

        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        return new WorldSize(width, height);
    }

    private Task<WorldInfoDocument?> GetWorldInfoAsync(CancellationToken cancellationToken)
        => _worldInfoCollection.Find(document => document.Id == WorldInfoDocument.DefaultId)
                               .FirstOrDefaultAsync(cancellationToken)!;

    private static (int X, int Y)? ParseRoomName(string? roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            return null;

        var directionIndex = FindVerticalDirectionIndex(roomName);
        if (directionIndex < 0)
            return null;

        var horizontalDirection = char.ToUpperInvariant(roomName[0]);
        var horizontalSpan = roomName.AsSpan(1, directionIndex - 1);
        if (!int.TryParse(horizontalSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var horizontalValue))
            return null;

        var verticalDirection = char.ToUpperInvariant(roomName[directionIndex]);
        var verticalSpan = roomName.AsSpan(directionIndex + 1);
        if (!int.TryParse(verticalSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var verticalValue))
            return null;

        var x = ConvertAxis(horizontalDirection, horizontalValue);
        var y = ConvertAxis(verticalDirection, verticalValue);
        return (x, y);
    }

    private static int FindVerticalDirectionIndex(string roomName)
    {
        for (var i = 1; i < roomName.Length; i++) {
            var c = char.ToUpperInvariant(roomName[i]);
            if (c is 'N' or 'S')
                return i;
        }

        return -1;
    }

    private static int ConvertAxis(char direction, int value)
        => direction switch
        {
            'W' or 'N' => -(value + 1),
            'E' or 'S' => value,
            _ => 0
        };
}
