namespace ScreepsDotNet.Engine.Processors.GlobalSteps;

public interface IGlobalProcessor
{
    Task ExecuteAsync(int gameTime, CancellationToken token = default);
}
