using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ScreepsDotNet.Driver.Services.Runtime;

internal sealed class V8RuntimeSandboxFactory : IRuntimeSandboxFactory
{
    private readonly RuntimeSandboxOptions _options;
    private readonly ILoggerFactory? _loggerFactory;

    public V8RuntimeSandboxFactory(IOptions<RuntimeSandboxOptions> options, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _loggerFactory = loggerFactory;
    }

    public IRuntimeSandbox CreateSandbox()
    {
        var logger = _loggerFactory?.CreateLogger<V8RuntimeSandbox>();
        return new V8RuntimeSandbox(_options, logger);
    }
}
