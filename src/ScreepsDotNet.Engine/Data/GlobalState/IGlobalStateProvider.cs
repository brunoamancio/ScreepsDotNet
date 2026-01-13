namespace ScreepsDotNet.Engine.Data.GlobalState;

using ScreepsDotNet.Engine.Data.Models;

public interface IGlobalStateProvider
{
    Task<GlobalState> GetGlobalStateAsync(CancellationToken token = default);
    void Invalidate();
}
