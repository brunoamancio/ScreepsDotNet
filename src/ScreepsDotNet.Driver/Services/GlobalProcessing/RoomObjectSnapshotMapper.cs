namespace ScreepsDotNet.Driver.Services.GlobalProcessing;

using MongoDB.Bson;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

internal static class RoomObjectSnapshotMapper
{
    public static RoomObjectDocument ToDocument(RoomObjectSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var document = new RoomObjectDocument
        {
            Id = ObjectId.TryParse(snapshot.Id, out var objectId) ? objectId : ObjectId.GenerateNewId(),
            UserId = snapshot.UserId,
            Type = snapshot.Type,
            Room = snapshot.RoomName,
            X = snapshot.X,
            Y = snapshot.Y,
            Hits = snapshot.Hits,
            HitsMax = snapshot.HitsMax,
            Name = snapshot.Name,
            Level = snapshot.Level,
            MineralType = snapshot.MineralType,
            Density = snapshot.Density,
        };

        if (snapshot.Store.Count > 0)
            document.Store = new Dictionary<string, int>(snapshot.Store);

        if (snapshot.StoreCapacity.HasValue)
            document.StoreCapacity = snapshot.StoreCapacity.Value;

        if (snapshot.Reservation is not null) {
            document.Reservation = new RoomReservationDocument
            {
                UserId = snapshot.Reservation.UserId,
                EndTime = snapshot.Reservation.EndTime,
            };
        }

        if (snapshot.Sign is not null) {
            document.Sign = new RoomSignDocument
            {
                UserId = snapshot.Sign.UserId,
                Text = snapshot.Sign.Text,
                Time = snapshot.Sign.Time,
            };
        }

        return document;
    }
}
