namespace ScreepsDotNet.Backend.Cli.Infrastructure;

using System.Threading;
using Microsoft.Extensions.Hosting;

internal sealed class NullHostApplicationLifetime : IHostApplicationLifetime
{
    public static IHostApplicationLifetime Instance { get; } = new NullHostApplicationLifetime();

    private NullHostApplicationLifetime()
    {
    }

    public CancellationToken ApplicationStarted => CancellationToken.None;
    public CancellationToken ApplicationStopping => CancellationToken.None;
    public CancellationToken ApplicationStopped => CancellationToken.None;

    public void StopApplication()
    {
    }
}
