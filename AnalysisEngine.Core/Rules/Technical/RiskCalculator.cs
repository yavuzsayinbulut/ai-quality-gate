using System.Text.RegularExpressions;
using AnalysisEngine.Core.Models;
using AnalysisEngine.Core.DiffAnalysis;

namespace AnalysisEngine.Core.Rules.Technical;

public class RiskCalculator
{
    /// <summary>Son Check() cagrisindaki ADR ihlalleri — rapor icin.</summary>
    private List<AdrViolation> _lastAdrViolations = new();
    public List<AdrViolation> LastAdrViolations => _lastAdrViolations;

    private static readonly Dictionary<FileLayer, string> LayerNames = new()
    {
        { FileLayer.Models, "Model" }, { FileLayer.Controllers, "API" },
        { FileLayer.Services, "Is Mantigi" }, { FileLayer.Contracts, "Kontrat" },
        { FileLayer.HttpClients, "HTTP Client" }, { FileLayer.Infrastructure, "Altyapi" },
        { FileLayer.Helpers, "Yardimci" }, { FileLayer.Tests, "Test" },
        { FileLayer.Unknown, "Diger" }
    };

    public (int Score, List<RuleResult> Results) Calculate(
        List<ChangeAtom> changes, List<AffectedService> affectedServices,
        List<DiffParser.DiffFile>? diffFiles = null)
    {
        var results = new List<RuleResult>();
        int totalScore = 0;

        // ═══ K1: Katman riski ═══
        var layerScore = CalculateLayerRisk(changes);
        var layers = string.Join(", ", changes.Select(c => LayerNames.GetValueOrDefault(c.Layer, c.Layer.ToString())).Distinct());
        results.Add(new RuleResult("K1 Katman Riski", layerScore,
            $"{layers} katmaninda degisiklik"));
        totalScore += layerScore;

        // ═══ K2: Kirilma degisikligi ═══
        var breakingCount = changes.Count(c => IsBreaking(c.Type));
        var breakingScore = breakingCount switch { 0 => 1, 1 => 2, 2 => 3, _ => 4 };
        results.Add(new RuleResult("K2 Kirilma Degisikligi", breakingScore,
            breakingCount > 0
                ? $"{breakingCount} adet kirilma degisikligi tespit edildi"
                : "Kirilma degisikligi tespit edilmedi"));
        totalScore += breakingScore;

        // ═══ K3: Etkilenen servis sayisi ═══
        var consumerScore = affectedServices.Count switch { 0 => 1, 1 => 2, 2 => 3, _ => 4 };
        var directCount = affectedServices.Count(a => a.Hop == 1);
        var transitCount = affectedServices.Count(a => a.Hop == 2);
        results.Add(new RuleResult("K3 Etkilenen Servis", consumerScore,
            $"{directCount} dogrudan + {transitCount} transitif = {affectedServices.Count} servis etkileniyor"));
        totalScore += consumerScore;

        // ═══ K4: Degisiklik hacmi ═══
        var volumeScore = changes.Count switch { <= 2 => 1, <= 5 => 2, <= 10 => 3, _ => 4 };
        results.Add(new RuleResult("K4 Degisiklik Hacmi", volumeScore,
            $"{changes.Count} adet degisiklik atomu"));
        totalScore += volumeScore;

        // ═══ K5: TypeCompatibility (mevcut) ═══
        var typeCompScore = CalculateTypeCompatibility(changes);
        if (typeCompScore > 1)
        {
            results.Add(new RuleResult("K5 Tip Uyumluluk", typeCompScore,
                "Tip degisikligi tespit edildi — uyumluluk riski"));
            totalScore += typeCompScore;
        }

        // ═══ K6: CqrsCompliance (mevcut) ═══
        var cqrsScore = CalculateCqrsCompliance(changes);
        if (cqrsScore > 1)
        {
            results.Add(new RuleResult("K6 CQRS Uyum", cqrsScore,
                "Mimari ihlal tespit edildi"));
            totalScore += cqrsScore;
        }

        // ═══ K7: ContractChange (mevcut) ═══
        var contractScore = CalculateContractChange(changes);
        if (contractScore > 1)
        {
            results.Add(new RuleResult("K7 Kontrat Degisikligi", contractScore,
                "Disariya acik kontrat degisti"));
            totalScore += contractScore;
        }

        // ═══ K8: ReturnBehaviorRule (YENI) ═══
        var returnScore = CalculateReturnBehaviorRisk(changes);
        results.Add(new RuleResult("K8 Return Davranis", returnScore.Score,
            returnScore.Reason));
        totalScore += returnScore.Score;

        // ═══ K9: ApiSurfaceRule (YENI) ═══
        var apiScore = CalculateApiSurfaceRisk(changes, diffFiles);
        results.Add(new RuleResult("K9 API Yuzey", apiScore.Score,
            apiScore.Reason));
        totalScore += apiScore.Score;

        // ═══ K10: SerializationRule (YENI) ═══
        var serScore = CalculateSerializationRisk(diffFiles);
        results.Add(new RuleResult("K10 Serialization", serScore.Score,
            serScore.Reason));
        totalScore += serScore.Score;

        // ═══ K11: AdrComplianceRule (YENI) ═══
        var adrChecker = new AdrComplianceChecker();
        var adrViolations = diffFiles != null ? adrChecker.Check(diffFiles) : new List<AdrViolation>();
        _lastAdrViolations = adrViolations; // Pipeline'da rapor icin sakla

        var adrScore = adrViolations.Count switch { 0 => 1, <= 2 => 2, <= 5 => 3, _ => 4 };
        var criticalCount = adrViolations.Count(v => v.Severity == AdrSeverity.Critical);
        var highCount = adrViolations.Count(v => v.Severity == AdrSeverity.High);
        if (criticalCount > 0) adrScore = Math.Max(adrScore, 4);
        if (highCount >= 3) adrScore = Math.Max(adrScore, 3);

        var adrReason = adrViolations.Count == 0
            ? "ADR ihlali tespit edilmedi"
            : $"{adrViolations.Count} ADR ihlali ({criticalCount} kritik, {highCount} yuksek)";
        results.Add(new RuleResult("K11 ADR Uyumluluk", adrScore, adrReason));
        totalScore += adrScore;

        int ruleCount = results.Count;
        int normalized = Math.Clamp((int)Math.Ceiling(totalScore / (double)ruleCount * 2.5), 1, 10);
        return (normalized, results);
    }

    // ═══ MEVCUT KURALLAR ═══

    private int CalculateLayerRisk(List<ChangeAtom> changes)
    {
        if (changes.Any(c => c.Layer is FileLayer.Models or FileLayer.Contracts)) return 4;
        if (changes.Any(c => c.Layer == FileLayer.Controllers)) return 3;
        if (changes.Any(c => c.Layer == FileLayer.Services)) return 2;
        return 1;
    }

    private int CalculateTypeCompatibility(List<ChangeAtom> changes)
    {
        var typeChanges = changes.Where(c => c.Type == ChangeType.PropertyTypeChanged).ToList();
        if (!typeChanges.Any()) return 1;

        // decimal → int gibi kritik tip degisiklikleri
        foreach (var tc in typeChanges)
        {
            var old = (tc.OldValue ?? "").ToLowerInvariant();
            var @new = (tc.NewValue ?? "").ToLowerInvariant();
            if (old.Contains("decimal") && (@new.Contains("int") || @new.Contains("long"))) return 4;
            if (old.Contains("string") && (@new.Contains("int") || @new.Contains("guid"))) return 4;
        }
        return typeChanges.Count >= 2 ? 3 : 2;
    }

    private int CalculateCqrsCompliance(List<ChangeAtom> changes)
    {
        // Controller'da is mantigi varsa
        var controllerChanges = changes.Where(c => c.Layer == FileLayer.Controllers);
        if (controllerChanges.Any(c => c.Type is ChangeType.ArithmeticChanged or ChangeType.ValidationChanged))
            return 2;
        return 1;
    }

    private int CalculateContractChange(List<ChangeAtom> changes)
    {
        var contractChanges = changes.Where(c => c.Layer is FileLayer.Models or FileLayer.Contracts);
        var breakingInContract = contractChanges.Count(c => IsBreaking(c.Type));
        return breakingInContract switch { 0 => 1, 1 => 2, 2 => 3, _ => 4 };
    }

    // ═══ YENI KURALLAR ═══

    /// <summary>
    /// K8: Return davranis degisikligi — return null eklenmesi, metod govdesi degisimi.
    /// ChangeClassifier'dan ReturnValueChanged ve MethodBodyChanged atomlarina bakar.
    /// </summary>
    private (int Score, string Reason) CalculateReturnBehaviorRisk(List<ChangeAtom> changes)
    {
        var returnChanges = changes.Where(c =>
            c.Type is ChangeType.ReturnValueChanged or ChangeType.MethodBodyChanged).ToList();

        if (!returnChanges.Any())
            return (1, "Return davranis degisikligi tespit edilmedi");

        var hasReturnNull = returnChanges.Any(c => c.Type == ChangeType.ReturnValueChanged);
        var hasBodyRewrite = returnChanges.Any(c => c.Type == ChangeType.MethodBodyChanged);

        if (hasReturnNull && hasBodyRewrite)
            return (4, $"Return null eklendi + metod govdesi degisti — consumer'lar null handle etmeli");
        if (hasReturnNull)
            return (3, "Return null eklendi — consumer'larda NullReferenceException riski");
        if (hasBodyRewrite)
            return (2, $"{returnChanges.Count} metod govdesi degisti — davranis degisikligi olabilir");

        return (1, "Minor return degisikligi");
    }

    /// <summary>
    /// K9: API yuzey degisikligi — endpoint silme, method degisimi, parametre ekleme.
    /// Controller katmanindaki degisikliklere bakar.
    /// </summary>
    private (int Score, string Reason) CalculateApiSurfaceRisk(
        List<ChangeAtom> changes, List<DiffParser.DiffFile>? diffFiles)
    {
        // Controller'da metod silme/degisikligi
        var controllerChanges = changes.Where(c =>
            c.Layer == FileLayer.Controllers &&
            c.Type is ChangeType.MethodRemoved or ChangeType.MethodSignatureChanged or ChangeType.MethodAdded).ToList();

        if (!controllerChanges.Any() && diffFiles == null)
            return (1, "API yuzey degisikligi tespit edilmedi");

        // Diff'te HTTP attribute degisiklikleri
        int httpAttrChanges = 0;
        int routeChanges = 0;

        if (diffFiles != null)
        {
            var httpAttrRegex = new Regex(@"\[Http(Get|Post|Put|Delete|Patch)\]", RegexOptions.Compiled);
            var routeRegex = new Regex(@"\[Route\(|\""/api/", RegexOptions.Compiled);

            foreach (var file in diffFiles)
            {
                var normalized = file.FilePath.Replace('\\', '/').ToLowerInvariant();
                if (!normalized.Contains("/controllers/")) continue;

                httpAttrChanges += file.RemovedLines.Count(l => httpAttrRegex.IsMatch(l));
                routeChanges += file.RemovedLines.Count(l => routeRegex.IsMatch(l));
                routeChanges += file.AddedLines.Count(l => routeRegex.IsMatch(l)) > 0 &&
                                file.RemovedLines.Count(l => routeRegex.IsMatch(l)) > 0 ? 1 : 0;
            }
        }

        var methodRemovedCount = controllerChanges.Count(c => c.Type == ChangeType.MethodRemoved);
        var methodChangedCount = controllerChanges.Count(c => c.Type == ChangeType.MethodSignatureChanged);

        if (methodRemovedCount > 0)
            return (4, $"{methodRemovedCount} endpoint SILINDI — consumer'lar 404 alacak!");
        if (routeChanges > 0)
            return (3, "Endpoint URL degisti — consumer'lar eski URL'ye istek gonderiyor");
        if (httpAttrChanges > 0)
            return (3, "HTTP method degisti — consumer'lar 405 Method Not Allowed alacak");
        if (methodChangedCount > 0)
            return (2, $"{methodChangedCount} endpoint imzasi degisti — parametre uyumsuzlugu olabilir");

        return (1, "API yuzey degisikligi tespit edilmedi");
    }

    /// <summary>
    /// K10: Serialization kirilmasi — JsonPropertyName, JsonIgnore, enum deger silme.
    /// Diff satirlarinda JSON attribute degisikliklerini arar.
    /// </summary>
    private (int Score, string Reason) CalculateSerializationRisk(List<DiffParser.DiffFile>? diffFiles)
    {
        if (diffFiles == null)
            return (1, "Serialization degisikligi analiz edilemedi");

        var jsonPropNameRegex = new Regex(@"\[JsonPropertyName\(", RegexOptions.Compiled);
        var jsonIgnoreRegex = new Regex(@"\[JsonIgnore\]", RegexOptions.Compiled);
        var jsonConverterRegex = new Regex(@"\[JsonConverter\(", RegexOptions.Compiled);
        var enumValueRegex = new Regex(@"^\s*\w+\s*[,=]", RegexOptions.Compiled);

        int propNameChanges = 0;
        int ignoreChanges = 0;
        int converterChanges = 0;
        int enumRemovals = 0;

        foreach (var file in diffFiles)
        {
            // JsonPropertyName degisikligi
            var removedJsonProps = file.RemovedLines.Count(l => jsonPropNameRegex.IsMatch(l));
            var addedJsonProps = file.AddedLines.Count(l => jsonPropNameRegex.IsMatch(l));
            if (removedJsonProps > 0 && addedJsonProps > 0) propNameChanges++;
            if (removedJsonProps > 0 && addedJsonProps == 0) propNameChanges += 2; // Silindi

            // JsonIgnore eklendi
            var addedIgnore = file.AddedLines.Count(l => jsonIgnoreRegex.IsMatch(l));
            if (addedIgnore > 0) ignoreChanges += addedIgnore;

            // JsonConverter degisikligi
            var removedConverter = file.RemovedLines.Count(l => jsonConverterRegex.IsMatch(l));
            if (removedConverter > 0) converterChanges++;

            // Enum deger silme (Models katmaninda enum dosyasi)
            var normalized = file.FilePath.Replace('\\', '/').ToLowerInvariant();
            if (normalized.Contains("/models/") && file.RemovedLines.Count > file.AddedLines.Count)
            {
                var removedEnumValues = file.RemovedLines.Count(l =>
                    enumValueRegex.IsMatch(l) && !l.Trim().StartsWith("//") && !l.Contains("public") && !l.Contains("class"));
                enumRemovals += removedEnumValues;
            }
        }

        int totalSerIssues = propNameChanges + ignoreChanges + converterChanges + enumRemovals;
        if (totalSerIssues == 0)
            return (1, "Serialization degisikligi tespit edilmedi");

        var reasons = new List<string>();
        if (propNameChanges > 0) reasons.Add($"JsonPropertyName degisti ({propNameChanges})");
        if (ignoreChanges > 0) reasons.Add($"JsonIgnore eklendi ({ignoreChanges})");
        if (converterChanges > 0) reasons.Add($"JsonConverter degisti ({converterChanges})");
        if (enumRemovals > 0) reasons.Add($"Enum degeri silindi ({enumRemovals})");

        int score = totalSerIssues switch { 1 => 2, 2 => 3, _ => 4 };
        return (score, string.Join(", ", reasons) + " — deserialization kirilabilir!");
    }

    private static bool IsBreaking(ChangeType type) => type is
        ChangeType.PropertyRemoved or ChangeType.PropertyTypeChanged or
        ChangeType.MethodRemoved or ChangeType.MethodSignatureChanged or
        ChangeType.ReturnValueChanged;
}
