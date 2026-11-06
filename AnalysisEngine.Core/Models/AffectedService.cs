namespace AnalysisEngine.Core.Models;

public record AffectedService(
    string ServiceName,
    string ClientFile,
    string Relevance,
    int Hop,
    string? Via = null
);
