namespace AnalysisEngine.Core.Models;

public record AnalysisReport(
    string ChangedService,
    List<ChangeAtom> ChangeAtoms,
    List<AffectedService> AffectedServices,
    RiskScore RiskScore,
    List<string> Recommendations
);
