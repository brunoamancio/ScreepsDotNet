namespace ScreepsDotNet.Driver.Services.Runtime;

internal interface IRuntimeSandboxPool
{
    IRuntimeSandbox Rent();
    void Return(IRuntimeSandbox sandbox);
}
