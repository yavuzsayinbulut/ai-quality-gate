using AnalysisEngine.Core.DiffAnalysis;
using AnalysisEngine.Core.Models;

namespace AnalysisEngine.Tests.Unit;

public class ChangeClassifierTests
{
    private readonly DiffParser _parser = new();
    private readonly ChangeClassifier _classifier = new();

    [Fact]
    public void Classify_PropertyRemoved_DetectsCorrectly()
    {
        var diff = """
            diff --git a/CustomerService.Domain/Models/Customer.cs b/CustomerService.Domain/Models/Customer.cs
            index abc..def 100644
            --- a/CustomerService.Domain/Models/Customer.cs
            +++ b/CustomerService.Domain/Models/Customer.cs
            @@ -5,7 +5,6 @@
                 public string FirstName { get; set; }
            -    public string MiddleName { get; set; }
                 public string LastName { get; set; }
            """;

        var files = _parser.Parse(diff);
        var atoms = _classifier.Classify(files);

        Assert.Contains(atoms, a => a.Type == ChangeType.PropertyRemoved);
        var removed = atoms.First(a => a.Type == ChangeType.PropertyRemoved);
        Assert.Contains("MiddleName", removed.Description);
        Assert.Equal(FileLayer.Models, removed.Layer);
    }

    [Fact]
    public void Classify_PropertyTypeChanged_DetectsCorrectly()
    {
        var diff = """
            diff --git a/TransferService.Domain/Models/Transfer.cs b/TransferService.Domain/Models/Transfer.cs
            index abc..def 100644
            --- a/TransferService.Domain/Models/Transfer.cs
            +++ b/TransferService.Domain/Models/Transfer.cs
            @@ -5,7 +5,7 @@
            -    public decimal Amount { get; set; }
            +    public double Amount { get; set; }
            """;

        var files = _parser.Parse(diff);
        var atoms = _classifier.Classify(files);

        Assert.Contains(atoms, a => a.Type == ChangeType.PropertyTypeChanged);
        var changed = atoms.First(a => a.Type == ChangeType.PropertyTypeChanged);
        Assert.Equal("decimal", changed.OldValue);
        Assert.Equal("double", changed.NewValue);
    }

    [Fact]
    public void Classify_ExceptionAdded_DetectsCorrectly()
    {
        var diff = """
            diff --git a/CustomerService.Domain/Services/CustomerService.cs b/CustomerService.Domain/Services/CustomerService.cs
            index abc..def 100644
            --- a/CustomerService.Domain/Services/CustomerService.cs
            +++ b/CustomerService.Domain/Services/CustomerService.cs
            @@ -15,6 +15,7 @@
                 public async Task<Customer> GetCustomer(Guid id)
            +        throw new InvalidOperationException("Customer is blocked");
            """;

        var files = _parser.Parse(diff);
        var atoms = _classifier.Classify(files);

        Assert.Contains(atoms, a => a.Type == ChangeType.ExceptionAdded);
    }

    [Fact]
    public void Classify_ValidationChanged_DetectsCorrectly()
    {
        var diff = """
            diff --git a/CustomerService.Domain/Services/Validator.cs b/CustomerService.Domain/Services/Validator.cs
            index abc..def 100644
            --- a/CustomerService.Domain/Services/Validator.cs
            +++ b/CustomerService.Domain/Services/Validator.cs
            @@ -10,7 +10,7 @@
            -    RuleFor(x => x.Name).MaxLength(50);
            +    RuleFor(x => x.Name).MaxLength(100);
            """;

        var files = _parser.Parse(diff);
        var atoms = _classifier.Classify(files);

        Assert.Contains(atoms, a => a.Type == ChangeType.ValidationChanged);
    }

    [Fact]
    public void Classify_MethodSignatureChanged_DetectsCorrectly()
    {
        var diff = """
            diff --git a/CustomerService.Domain/Services/CustomerService.cs b/CustomerService.Domain/Services/CustomerService.cs
            index abc..def 100644
            --- a/CustomerService.Domain/Services/CustomerService.cs
            +++ b/CustomerService.Domain/Services/CustomerService.cs
            @@ -10,7 +10,7 @@
            -    public async Task<Customer> GetCustomer(Guid id)
            +    public async Task<Customer> GetCustomer(Guid id, bool includeDeleted)
            """;

        var files = _parser.Parse(diff);
        var atoms = _classifier.Classify(files);

        Assert.Contains(atoms, a => a.Type == ChangeType.MethodSignatureChanged);
        var changed = atoms.First(a => a.Type == ChangeType.MethodSignatureChanged);
        Assert.Contains("GetCustomer", changed.Description);
    }

    [Fact]
    public void Classify_ArithmeticChanged_DetectsCorrectly()
    {
        var diff = """
            diff --git a/TransferService.Domain/Services/FeeCalculator.cs b/TransferService.Domain/Services/FeeCalculator.cs
            index abc..def 100644
            --- a/TransferService.Domain/Services/FeeCalculator.cs
            +++ b/TransferService.Domain/Services/FeeCalculator.cs
            @@ -10,7 +10,7 @@
            -    var fee = amount * 0.01m;
            +    var fee = Math.Round(amount * 0.015m, 2);
            """;

        var files = _parser.Parse(diff);
        var atoms = _classifier.Classify(files);

        Assert.Contains(atoms, a => a.Type == ChangeType.ArithmeticChanged);
    }

    [Fact]
    public void DetermineLayer_Models_CorrectLayer()
    {
        Assert.Equal(FileLayer.Models, ChangeClassifier.DetermineLayer("CustomerService.Domain/Models/Customer.cs"));
    }

    [Fact]
    public void DetermineLayer_Services_CorrectLayer()
    {
        Assert.Equal(FileLayer.Services, ChangeClassifier.DetermineLayer("CustomerService.Domain/Services/CustomerService.cs"));
    }

    [Fact]
    public void DetermineLayer_Controllers_CorrectLayer()
    {
        Assert.Equal(FileLayer.Controllers, ChangeClassifier.DetermineLayer("CustomerService.Api/Controllers/CustomerController.cs"));
    }

    [Fact]
    public void DetermineLayer_HttpClients_CorrectLayer()
    {
        Assert.Equal(FileLayer.HttpClients, ChangeClassifier.DetermineLayer("TransferService.Infrastructure/HttpClients/CustomerServiceClient.cs"));
    }
}
