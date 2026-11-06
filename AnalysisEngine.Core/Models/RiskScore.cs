namespace AnalysisEngine.Core.Models;

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public record RuleResult(string RuleName, int Score, string Reason);

public record RiskScore(
    int TechnicalScore,
    int BusinessScore,
    int FinalScore,
    RiskLevel Level,
    List<RuleResult> RuleResults
)
{
    public static RiskLevel CalculateLevel(int score) => score switch
    {
        <= 3 => RiskLevel.Low,
        <= 5 => RiskLevel.Medium,
        <= 7 => RiskLevel.High,
        _ => RiskLevel.Critical
    };
}
