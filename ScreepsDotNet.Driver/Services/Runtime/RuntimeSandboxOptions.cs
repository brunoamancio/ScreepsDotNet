namespace ScreepsDotNet.Driver.Services.Runtime;

internal sealed class RuntimeSandboxOptions
{
    public int MaxHeapSizeMegabytes { get; init; } = 256;
    public int ScriptInterruptBufferMs { get; init; } = 25;
    public int DefaultCpuLimitMs { get; init; } = 250;
}
