using AnalysisEngine.Core.Models;
using AnalysisEngine.Core.Rules.Technical;
using AnalysisEngine.Core.Rules.Business;

namespace AnalysisEngine.Tests.Unit;

public class RiskCalculatorTests
{
    private readonly RiskCalculator _calculator = new();
    private readonly BusinessRiskCalculator _businessCalculator = new();

    [Fact]
    public void Calculate_NoChanges_MinimalRisk()
    {
        var changes = new List<ChangeAtom>();
        var affected = new List<AffectedService>();

        var (score, results) = _calculator.Calculate(changes, affected);

        Assert.InRange(score, 1, 3);
        Assert.NotEmpty(results);
    }

    [Fact]
    public void Calculate_ModelPropertyRemoved_HighRisk()
    {
        var changes = new List<ChangeAtom>
        {
            new(ChangeType.PropertyRemoved, "Models/Customer.cs", FileLayer.Models,
                "Property 'MiddleName' removed", OldValue: "string MiddleName"),
            new(ChangeType.PropertyTypeChanged, "Models/Customer.cs", FileLayer.Models,
                "Property 'Amount' type changed", OldValue: "decimal", NewValue: "double")
        };

        var affected = new List<AffectedService>
        {
            new("TransferService", "TransferService.Infrastructure/HttpClients/CustomerServiceClient.cs", "Customer validation", 1),
            new("InvoiceService", "InvoiceService.Infrastructure/HttpClients/CustomerServiceClient.cs", "Invoice customer info", 1),
            new("PaymentService", "PaymentService.Infrastructure/HttpClients/CustomerServiceClient.cs", "Merchant validation", 1)
        };

        var (score, results) = _calculator.Calculate(changes, affected);

        Assert.InRange(score, 5, 10);
    }

    [Fact]
    public void Calculate_HelperFileChange_LowRisk()
    {
        var changes = new List<ChangeAtom>
        {
            new(ChangeType.MethodAdded, "Helpers/StringExtensions.cs", FileLayer.Helpers,
                "Method 'ToTitleCase' added")
        };

        var affected = new List<AffectedService>();

        var (score, _) = _calculator.Calculate(changes, affected);

        Assert.InRange(score, 1, 4);
    }

    [Fact]
    public void Calculate_ManyConsumers_HigherRisk()
    {
        var changes = new List<ChangeAtom>
        {
            new(ChangeType.PropertyAdded, "Models/Customer.cs", FileLayer.Models,
                "Property 'TaxId' added", NewValue: "string TaxId")
        };

        var fewConsumers = new List<AffectedService>
        {
            new("ServiceA", "client.cs", "test", 1)
        };

        var manyConsumers = new List<AffectedService>
        {
            new("ServiceA", "client.cs", "test", 1),
            new("ServiceB", "client.cs", "test", 1),
            new("ServiceC", "client.cs", "test", 1),
            new("ServiceD", "client.cs", "test", 2, Via: "ServiceA")
        };

        var (scoreFew, _) = _calculator.Calculate(changes, fewConsumers);
        var (scoreMany, _) = _calculator.Calculate(changes, manyConsumers);

        Assert.True(scoreMany >= scoreFew);
    }

    [Fact]
    public void BusinessRisk_FinancialKeywords_HigherScore()
    {
        var changes = new List<ChangeAtom>
        {
            new(ChangeType.PropertyTypeChanged, "Models/Transfer.cs", FileLayer.Models,
                "Property 'Amount' type changed from decimal to double",
                OldValue: "decimal", NewValue: "double"),
            new(ChangeType.ArithmeticChanged, "Services/FeeCalc.cs", FileLayer.Services,
                "Commission calculation changed")
        };

        var (score, results) = _businessCalculator.Calculate(changes);

        Assert.InRange(score, 3, 10);
        Assert.Contains(results, r => r.RuleName == "FinancialFieldRisk" && r.Score > 1);
    }

    [Fact]
    public void BusinessRisk_RegulatoryKeywords_CriticalScore()
    {
        var changes = new List<ChangeAtom>
        {
            new(ChangeType.ValidationChanged, "Services/KycService.cs", FileLayer.Services,
                "KYC validation threshold changed"),
            new(ChangeType.PropertyRemoved, "Models/Customer.cs", FileLayer.Models,
                "Property 'Tckn' removed", OldValue: "string Tckn")
        };

        var (score, results) = _businessCalculator.Calculate(changes);

        Assert.Contains(results, r => r.RuleName == "RegulatoryRisk" && r.Score >= 4);
    }

    [Fact]
    public void BusinessRisk_NoKeywords_LowScore()
    {
        var changes = new List<ChangeAtom>
        {
            new(ChangeType.MethodAdded, "Helpers/Logger.cs", FileLayer.Helpers,
                "Method 'LogDebug' added")
        };

        var (score, _) = _businessCalculator.Calculate(changes);

        Assert.InRange(score, 1, 3);
    }

    [Fact]
    public void RiskLevel_CalculateLevel_CorrectMapping()
    {
        Assert.Equal(RiskLevel.Low, RiskScore.CalculateLevel(1));
        Assert.Equal(RiskLevel.Low, RiskScore.CalculateLevel(3));
        Assert.Equal(RiskLevel.Medium, RiskScore.CalculateLevel(4));
        Assert.Equal(RiskLevel.Medium, RiskScore.CalculateLevel(5));
        Assert.Equal(RiskLevel.High, RiskScore.CalculateLevel(6));
        Assert.Equal(RiskLevel.High, RiskScore.CalculateLevel(7));
        Assert.Equal(RiskLevel.Critical, RiskScore.CalculateLevel(8));
        Assert.Equal(RiskLevel.Critical, RiskScore.CalculateLevel(10));
    }
}
