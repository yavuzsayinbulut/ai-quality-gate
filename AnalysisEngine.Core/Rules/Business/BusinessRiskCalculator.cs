using AnalysisEngine.Core.Models;

namespace AnalysisEngine.Core.Rules.Business;

/// <summary>
/// Scores business-facing risk using deterministic, domain-neutral signals.
/// </summary>
public class BusinessRiskCalculator
{
    private static readonly string[] FinancialTerms =
    [
        "amount", "balance", "currency", "fee", "commission", "money",
        "payment", "refund", "transfer", "decimal", "tutar", "bakiye", "komisyon"
    ];

    private static readonly string[] RegulatoryTerms =
    [
        "identity", "kyc", "aml", "tax", "iban", "tckn", "privacy",
        "consent", "audit", "retention", "regulatory", "compliance"
    ];

    public (int Score, List<RuleResult> Results) Calculate(List<ChangeAtom> changes)
    {
        var results = new List<RuleResult>();
        var financialMatches = changes.Count(change => ContainsAny(change, FinancialTerms));
        var regulatoryMatches = changes.Count(change => ContainsAny(change, RegulatoryTerms));
        var arithmeticChanges = changes.Count(change => change.Type == ChangeType.ArithmeticChanged);
        var validationChanges = changes.Count(change => change.Type == ChangeType.ValidationChanged);

        var score = 1;

        if (financialMatches > 0)
        {
            var financialScore = Math.Min(4, 2 + financialMatches);
            results.Add(new RuleResult(
                "FinancialFieldRisk",
                financialScore,
                $"{financialMatches} financial-domain change signal detected"));
            score += financialScore - 1;
        }

        if (regulatoryMatches > 0)
        {
            var regulatoryScore = Math.Min(5, 3 + regulatoryMatches);
            results.Add(new RuleResult(
                "RegulatoryRisk",
                regulatoryScore,
                $"{regulatoryMatches} compliance-sensitive change signal detected"));
            score += regulatoryScore - 1;
        }

        if (arithmeticChanges > 0)
        {
            results.Add(new RuleResult(
                "CalculationRisk",
                3,
                $"{arithmeticChanges} arithmetic behavior change detected"));
            score += 2;
        }

        if (validationChanges > 0)
        {
            results.Add(new RuleResult(
                "ValidationRisk",
                2,
                $"{validationChanges} validation behavior change detected"));
            score += 1;
        }

        if (results.Count == 0)
        {
            results.Add(new RuleResult("BusinessSignalRisk", 1, "No elevated business-risk signal detected"));
        }

        return (Math.Clamp(score, 1, 10), results);
    }

    private static bool ContainsAny(ChangeAtom change, IEnumerable<string> terms)
    {
        var searchable = string.Join(' ',
            change.FilePath,
            change.Description,
            change.OldValue ?? string.Empty,
            change.NewValue ?? string.Empty);

        return terms.Any(term => searchable.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
