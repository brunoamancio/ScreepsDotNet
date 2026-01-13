namespace ScreepsDotNet.Engine.Data.Bulk;

public interface IRoomMutationWriterFactory
{
    IRoomMutationWriter Create(string roomName);
}
