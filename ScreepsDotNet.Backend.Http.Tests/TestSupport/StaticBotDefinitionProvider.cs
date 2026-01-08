namespace ScreepsDotNet.Backend.Http.Tests.TestSupport;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ScreepsDotNet.Backend.Core.Models.Bots;
using ScreepsDotNet.Backend.Core.Services;

internal sealed class StaticBotDefinitionProvider : IBotDefinitionProvider
{
    private readonly IReadOnlyDictionary<string, BotDefinition> _definitions = new Dictionary<string, BotDefinition>(StringComparer.OrdinalIgnoreCase)
    {
        ["alpha"] = new("alpha",
                        "Test bot definition for integration fixtures.",
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["main"] = "module.exports.loop = function () { console.log('alpha'); };"
                        })
    };

    public Task<IReadOnlyList<BotDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BotDefinition>>(_definitions.Values.ToList());

    public Task<BotDefinition?> FindDefinitionAsync(string name, CancellationToken cancellationToken = default)
    {
        _definitions.TryGetValue(name, out var definition);
        return Task.FromResult(definition);
    }
}
