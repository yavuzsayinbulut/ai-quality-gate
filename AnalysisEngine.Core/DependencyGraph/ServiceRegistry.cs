using System.Text.Json;
using System.Text.Json.Serialization;

namespace AnalysisEngine.Core.DependencyGraph;

/// <summary>
/// Loads and queries the service-registry.json file.
/// </summary>
public class ServiceRegistry
{
    public Dictionary<string, ServiceDefinition> Services { get; }

    public ServiceRegistry(Dictionary<string, ServiceDefinition> services)
    {
        Services = services;
    }

    /// <summary>
    /// Load the registry from a JSON file path.
    /// </summary>
    public static ServiceRegistry LoadFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return LoadFromJson(json);
    }

    /// <summary>
    /// Load the registry from a JSON string.
    /// </summary>
    public static ServiceRegistry LoadFromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var root = JsonSerializer.Deserialize<RegistryRoot>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize service registry.");
        return new ServiceRegistry(root.Services);
    }

    /// <summary>
    /// Get all services that directly call the given service.
    /// </summary>
    public List<(string ConsumerKey, ServiceCall Call)> GetDirectConsumers(string serviceKey)
    {
        var consumers = new List<(string, ServiceCall)>();

        foreach (var (key, svc) in Services)
        {
            foreach (var call in svc.Calls)
            {
                if (string.Equals(call.Service, serviceKey, StringComparison.OrdinalIgnoreCase))
                {
                    consumers.Add((key, call));
                }
            }
        }

        return consumers;
    }

    /// <summary>
    /// Check if a service exists in the registry.
    /// </summary>
    public bool HasService(string serviceKey) => Services.ContainsKey(serviceKey);

    public ServiceDefinition? GetService(string serviceKey) =>
        Services.TryGetValue(serviceKey, out var svc) ? svc : null;
}

public class RegistryRoot
{
    public string Version { get; set; } = "";
    public string LastUpdated { get; set; } = "";
    public Dictionary<string, ServiceDefinition> Services { get; set; } = new();
}

public class ServiceDefinition
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public int Port { get; set; }
    public List<EndpointDefinition> Endpoints { get; set; } = new();
    public List<ServiceCall> Calls { get; set; } = new();
    public Dictionary<string, string> FilePatterns { get; set; } = new();
    /// <summary>Servisin dili: "dotnet", "java", "go", "node". Default "dotnet".</summary>
    public string Language { get; set; } = "dotnet";
}

public class EndpointDefinition
{
    public string Method { get; set; } = "";
    public string Path { get; set; } = "";
    public string? RequestModel { get; set; }
    public string? ResponseModel { get; set; }
}

public class ServiceCall
{
    public string Service { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string ClientFile { get; set; } = "";
    public string Purpose { get; set; } = "";
}
