using AnalysisEngine.Core.Models;

namespace AnalysisEngine.Core.DependencyGraph;

/// <summary>
/// Resolves which services are affected by changes to a given service,
/// including transitive dependencies up to 2 hops.
/// </summary>
public class DependencyResolver
{
    private readonly ServiceRegistry _registry;

    public DependencyResolver(ServiceRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Create a DependencyResolver from a JSON file path.
    /// </summary>
    public DependencyResolver(string registryFilePath)
        : this(ServiceRegistry.LoadFromFile(registryFilePath))
    {
    }

    /// <summary>
    /// Find all affected services when the given service changes.
    /// Returns direct consumers (hop 1) and transitive consumers (hop 2).
    /// </summary>
    public List<AffectedService> Resolve(string changedServiceKey)
    {
        var affected = new List<AffectedService>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { changedServiceKey };

        // Hop 1: direct consumers
        var directConsumers = _registry.GetDirectConsumers(changedServiceKey);
        foreach (var (consumerKey, call) in directConsumers)
        {
            if (visited.Contains(consumerKey))
                continue;

            visited.Add(consumerKey);
            affected.Add(new AffectedService(
                ServiceName: _registry.GetService(consumerKey)?.Name ?? consumerKey,
                ClientFile: call.ClientFile,
                Relevance: call.Purpose,
                Hop: 1
            ));
        }

        // Hop 2: transitive consumers (services that call the direct consumers)
        var hop1Keys = directConsumers.Select(c => c.ConsumerKey).ToList();
        foreach (var hop1Key in hop1Keys)
        {
            var transitiveConsumers = _registry.GetDirectConsumers(hop1Key);
            foreach (var (consumerKey, call) in transitiveConsumers)
            {
                if (visited.Contains(consumerKey))
                    continue;

                visited.Add(consumerKey);
                var hop1Name = _registry.GetService(hop1Key)?.Name ?? hop1Key;
                affected.Add(new AffectedService(
                    ServiceName: _registry.GetService(consumerKey)?.Name ?? consumerKey,
                    ClientFile: call.ClientFile,
                    Relevance: call.Purpose,
                    Hop: 2,
                    Via: hop1Name
                ));
            }
        }

        return affected;
    }
}
