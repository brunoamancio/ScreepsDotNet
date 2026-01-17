namespace ScreepsDotNet.Engine.Processors.GlobalSteps;

public interface IGlobalProcessorStep
{
    Task ExecuteAsync(GlobalProcessorContext context, CancellationToken token = default);
}
