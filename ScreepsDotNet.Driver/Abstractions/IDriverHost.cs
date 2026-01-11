using ScreepsDotNet.Driver.Abstractions.Bulk;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Environment;
using ScreepsDotNet.Driver.Abstractions.History;
using ScreepsDotNet.Driver.Abstractions.Notifications;
using ScreepsDotNet.Driver.Abstractions.Pathfinding;
using ScreepsDotNet.Driver.Abstractions.Queues;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Abstractions.Runtime;
using ScreepsDotNet.Driver.Abstractions.Users;

namespace ScreepsDotNet.Driver.Abstractions;

/// <summary>
/// High-level contract exposed to the Screeps engine. Aggregates the individual
/// services defined in the Driver API spec so legacy <c>driver.*</c> calls have
/// managed counterparts.
/// </summary>
public interface IDriverHost
{
    IDriverConfig Config { get; }
    IQueueService Queues { get; }
    IBulkWriterFactory BulkWriters { get; }
    IPathfinderService Pathfinder { get; }
    IRuntimeService Runtime { get; }
    IRoomDataService Rooms { get; }
    IUserDataService Users { get; }
    INotificationService Notifications { get; }
    IHistoryService History { get; }
    IEnvironmentService Environment { get; }

    /// <summary>
    /// Establish underlying connections (Mongo, Redis, native modules) and raise
    /// the equivalent of <c>config.engine.emit('init')</c> for the specified process type.
    /// </summary>
    Task ConnectAsync(DriverProcessType processType, CancellationToken token = default);
}
