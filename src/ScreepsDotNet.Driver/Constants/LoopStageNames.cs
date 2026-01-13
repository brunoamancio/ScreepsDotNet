namespace ScreepsDotNet.Driver.Constants;

public static class LoopStageNames
{
    public static class Main
    {
        public const string Start = "start";
        public const string GetUsers = "getUsers";
        public const string AddUsersToQueue = "addUsersToQueue";
        public const string WaitForUsers = "waitForUsers";
        public const string GetRooms = "getRooms";
        public const string AddRoomsToQueue = "addRoomsToQueue";
        public const string WaitForRooms = "waitForRooms";
        public const string CommitDbBulkPre = "commitDbBulk:pre";
        public const string Global = "global";
        public const string CommitDbBulkPost = "commitDbBulk:post";
        public const string IncrementGameTime = "incrementGameTime";
        public const string UpdateAccessibleRooms = "updateAccessibleRooms";
        public const string NotifyRoomsDone = "notifyRoomsDone";
        public const string Finish = "finish";

        public const string TelemetryEnqueueUsers = "enqueueUsers";
        public const string TelemetryEnqueueRooms = "enqueueRooms";
        public const string TelemetryDrainUsers = "drainUsers";
        public const string TelemetryDrainRooms = "drainRooms";
    }

    public static class Runner
    {
        public const string Start = "start";
        public const string RunUser = "runUser";
        public const string Finish = "finish";

        public const string TelemetryIdle = "idle";
        public const string TelemetryDequeue = "dequeue";
        public const string TelemetryThrottleDelay = "throttleDelay";
        public const string TelemetryExecute = "execute";
    }

    public static class Processor
    {
        public const string Start = "start";
        public const string ProcessRoom = "processRoom";
        public const string Finish = "finish";
        public const string RoomStatsUpdated = "roomStatsUpdated";

        public const string TelemetryIdle = "idle";
        public const string TelemetryDequeue = "dequeue";
        public const string TelemetryProcessRoom = "processRoom";
    }

    public static class Scheduler
    {
        public const string TelemetryStage = "scheduler";
    }
}
