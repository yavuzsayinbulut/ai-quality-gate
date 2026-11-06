using System.Text;
using AnalysisEngine.Core.Models;

namespace AnalysisEngine.Core.Report;

public class ReportGenerator
{
    public string Generate(AnalysisReport report)
    {
        var sb = new StringBuilder();
        var risk = report.RiskScore;
        var emoji = risk.Level switch { RiskLevel.Low => "🟢", RiskLevel.Medium => "🟡", RiskLevel.High => "🔴", _ => "🚨" };

        sb.AppendLine($"# {emoji} AI Quality Gate — Etki Analiz Raporu");
        sb.AppendLine();
        sb.AppendLine($"**Degisen Servis:** {report.ChangedService}");
        sb.AppendLine($"**Teknik Risk:** {risk.TechnicalScore}/10 | **Is Riski:** {risk.BusinessScore}/10 | **Nihai Risk:** {risk.FinalScore}/10 ({LevelTr(risk.Level)})");
        sb.AppendLine($"**Tarih:** {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        // Risk bar
        sb.Append("**Risk:** ");
        sb.Append(new string('█', risk.FinalScore));
        sb.Append(new string('░', 10 - risk.FinalScore));
        sb.AppendLine($" {risk.FinalScore}/10");
        sb.AppendLine();

        // Degisiklikler
        sb.AppendLine("---");
        sb.AppendLine("## Tespit Edilen Degisiklikler");
        sb.AppendLine();
        foreach (var atom in report.ChangeAtoms)
        {
            sb.AppendLine($"- **{AtomTypeTr(atom.Type)}** `{atom.FilePath}` ({LayerTr(atom.Layer)})");
            sb.AppendLine($"  {atom.Description}");
        }
        sb.AppendLine();

        // Etkilenen servisler
        sb.AppendLine("---");
        sb.AppendLine("## Etkilenen Servisler");
        sb.AppendLine();
        if (report.AffectedServices.Count == 0)
        {
            sb.AppendLine("Etkilenen servis yok.");
        }
        else
        {
            sb.AppendLine("| Servis | Etki | Client Dosyasi | Amac |");
            sb.AppendLine("|--------|------|----------------|------|");
            foreach (var svc in report.AffectedServices.OrderBy(s => s.Hop))
            {
                var hopLabel = svc.Hop == 1 ? "DOGRUDAN" : $"TRANSITIF ({svc.Via} uzerinden)";
                sb.AppendLine($"| {svc.ServiceName} | {hopLabel} | `{svc.ClientFile}` | {svc.Relevance} |");
            }
        }
        sb.AppendLine();

        // Bagimlılık zinciri
        sb.AppendLine("---");
        sb.AppendLine("## Bagimlılık Zinciri");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine($"  {report.ChangedService} (DEGISTI)");
        foreach (var svc in report.AffectedServices.Where(s => s.Hop == 1))
        {
            sb.AppendLine($"    ├── {svc.ServiceName} (dogrudan consumer)");
            foreach (var t in report.AffectedServices.Where(s => s.Hop == 2 && s.Via == svc.ServiceName))
                sb.AppendLine($"    │   └── {t.ServiceName} (transitif)");
        }
        sb.AppendLine("```");
        sb.AppendLine();

        // Risk kirilimi
        sb.AppendLine("---");
        sb.AppendLine("## Risk Kirilimi");
        sb.AppendLine();
        sb.AppendLine($"| Kural | Puan | Aciklama |");
        sb.AppendLine($"|-------|------|----------|");
        foreach (var rule in risk.RuleResults)
            sb.AppendLine($"| {rule.RuleName} | {rule.Score}/10 | {rule.Reason} |");
        sb.AppendLine();

        // Oneriler
        sb.AppendLine("---");
        sb.AppendLine("## Oneriler");
        sb.AppendLine();
        for (int i = 0; i < report.Recommendations.Count; i++)
            sb.AppendLine($"{i + 1}. {report.Recommendations[i]}");
        sb.AppendLine();

        // Sonuc
        var sonuc = risk.Level switch
        {
            RiskLevel.Low => "🟢 GUVENLE MERGE EDILEBILIR",
            RiskLevel.Medium => "🟡 INCELEME ONERILIR",
            RiskLevel.High => "🔴 DETAYLI INCELEME GEREKLI",
            _ => "🚨 MERGE ETMEYIN — Kirilma degisikligi tespit edildi"
        };
        sb.AppendLine($"**Sonuc:** {sonuc}");

        return sb.ToString();
    }

    public static List<string> GenerateRecommendations(
        List<ChangeAtom> changes, List<AffectedService> affected, RiskScore risk)
    {
        var recs = new List<string>();

        if (changes.Any(c => c.Type is ChangeType.PropertyRemoved or ChangeType.PropertyTypeChanged))
            recs.Add("KIRILMA: Property silindi veya tipi degisti. Tum consumer DTO'larini ve HttpClient mapping'lerini guncelleyin.");

        if (changes.Any(c => c.Type is ChangeType.MethodRemoved or ChangeType.MethodSignatureChanged))
            recs.Add("KIRILMA: Metod imzasi degisti. Tum cagiranlari kontrol edip guncelleyin.");

        if (affected.Count > 0)
            recs.Add($"Etkilenen ekipleri bilgilendirin: {string.Join(", ", affected.Select(a => a.ServiceName).Distinct())}");

        if (affected.Any(a => a.Hop == 2))
            recs.Add("Transitif bagimlilik tespit edildi. Tam zincir boyunca entegrasyon testi yapin.");

        if (changes.Any(c => c.Type == ChangeType.ExceptionAdded))
            recs.Add("Yeni exception eklendi. Consumer servislerin hata yonetimi kodunu kontrol edin.");

        if (changes.Any(c => c.Type == ChangeType.ValidationChanged))
            recs.Add("Validasyon kurallari degisti. Consumer'larin gonderdi isteklerin hala gecerli oldugunu dogrulayin.");

        if (changes.Any(c => c.Type == ChangeType.ArithmeticChanged))
            recs.Add("Hesaplama mantigi degisti. Finansal mutabakat testleri calistirin.");

        if (risk.Level >= RiskLevel.High)
        {
            recs.Add("YUKSEK RISK: Kademeli deploy (canary) onerilir.");
            recs.Add("Merge oncesi tam regresyon test paketi calistirin.");
        }

        if (risk.Level == RiskLevel.Critical)
            recs.Add("KRITIK: Kidemli muhendis incelemesi ve onayi gereklidir.");

        if (risk.RuleResults.Any(r => r.RuleName.Contains("Hassas") && r.Score >= 4))
            recs.Add("DIKKAT: Resmi sureclerle iliskili olabilecek alanlar degisti. Uyumluluk ekibine danisin.");

        if (recs.Count == 0)
            recs.Add("Dusuk riskli degisiklik. Standart inceleme ve test yeterlidir.");

        return recs;
    }

    private static string LevelTr(RiskLevel l) => l switch
    {
        RiskLevel.Low => "Dusuk",
        RiskLevel.Medium => "Orta",
        RiskLevel.High => "Yuksek",
        RiskLevel.Critical => "Kritik",
        _ => l.ToString()
    };

    private static string AtomTypeTr(ChangeType t) => t switch
    {
        ChangeType.PropertyRemoved => "Property Silindi",
        ChangeType.PropertyAdded => "Property Eklendi",
        ChangeType.PropertyTypeChanged => "Property Tipi Degisti",
        ChangeType.MethodRemoved => "Metod Silindi",
        ChangeType.MethodAdded => "Metod Eklendi",
        ChangeType.MethodSignatureChanged => "Metod Imzasi Degisti",
        ChangeType.ExceptionAdded => "Yeni Exception Eklendi",
        ChangeType.ExceptionRemoved => "Exception Kaldirildi",
        ChangeType.ValidationChanged => "Validasyon Degisti",
        ChangeType.ArithmeticChanged => "Hesaplama Degisti",
        ChangeType.FileRemoved => "Dosyadan Icerik Silindi",
        ChangeType.FileAdded => "Dosyaya Icerik Eklendi",
        _ => "Diger Degisiklik"
    };

    private static string LayerTr(FileLayer l) => l switch
    {
        FileLayer.Controllers => "API Katmani",
        FileLayer.Models => "Model Katmani",
        FileLayer.Services => "Is Mantigi Katmani",
        FileLayer.Contracts => "Kontrat Katmani",
        FileLayer.Infrastructure => "Altyapi Katmani",
        FileLayer.HttpClients => "HTTP Client Katmani",
        FileLayer.Tests => "Test Katmani",
        FileLayer.Helpers => "Yardimci Katman",
        _ => "Diger"
    };
}
