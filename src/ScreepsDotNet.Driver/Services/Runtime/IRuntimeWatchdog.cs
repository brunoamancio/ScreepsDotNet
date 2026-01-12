namespace ScreepsDotNet.Driver.Services.Runtime;

internal interface IRuntimeWatchdog
{
    bool TryConsumeColdStartRequest(string userId);
}
