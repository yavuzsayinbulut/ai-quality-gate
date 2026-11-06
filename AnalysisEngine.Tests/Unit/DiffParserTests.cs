using AnalysisEngine.Core.DiffAnalysis;

namespace AnalysisEngine.Tests.Unit;

public class DiffParserTests
{
    private readonly DiffParser _parser = new();

    [Fact]
    public void Parse_EmptyDiff_ReturnsEmpty()
    {
        var result = _parser.Parse("");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_SingleFile_PropertyRemoved()
    {
        var diff = """
            diff --git a/Models/Customer.cs b/Models/Customer.cs
            index abc1234..def5678 100644
            --- a/Models/Customer.cs
            +++ b/Models/Customer.cs
            @@ -10,7 +10,6 @@ public class Customer
                 public string FirstName { get; set; }
                 public string LastName { get; set; }
            -    public string MiddleName { get; set; }
                 public string Email { get; set; }
            """;

        var result = _parser.Parse(diff);

        Assert.Single(result);
        Assert.Equal("Models/Customer.cs", result[0].FilePath);
        Assert.Single(result[0].RemovedLines);
        Assert.Contains("public string MiddleName { get; set; }", result[0].RemovedLines);
        Assert.Empty(result[0].AddedLines);
    }

    [Fact]
    public void Parse_PropertyTypeChanged()
    {
        var diff = """
            diff --git a/Models/Transfer.cs b/Models/Transfer.cs
            index abc1234..def5678 100644
            --- a/Models/Transfer.cs
            +++ b/Models/Transfer.cs
            @@ -5,7 +5,7 @@ public class Transfer
                 public Guid Id { get; set; }
            -    public decimal Amount { get; set; }
            +    public double Amount { get; set; }
                 public string Currency { get; set; }
            """;

        var result = _parser.Parse(diff);

        Assert.Single(result);
        Assert.Single(result[0].RemovedLines);
        Assert.Single(result[0].AddedLines);
        Assert.Contains("public decimal Amount { get; set; }", result[0].RemovedLines);
        Assert.Contains("public double Amount { get; set; }", result[0].AddedLines);
    }

    [Fact]
    public void Parse_MultipleFiles()
    {
        var diff = """
            diff --git a/Models/Customer.cs b/Models/Customer.cs
            index abc..def 100644
            --- a/Models/Customer.cs
            +++ b/Models/Customer.cs
            @@ -1,4 +1,4 @@
            -    public string Name { get; set; }
            +    public string FullName { get; set; }
            diff --git a/Services/CustomerService.cs b/Services/CustomerService.cs
            index abc..def 100644
            --- a/Services/CustomerService.cs
            +++ b/Services/CustomerService.cs
            @@ -10,6 +10,7 @@
            +    public async Task<Customer> GetByEmail(string email)
            """;

        var result = _parser.Parse(diff);

        Assert.Equal(2, result.Count);
        Assert.Equal("Models/Customer.cs", result[0].FilePath);
        Assert.Equal("Services/CustomerService.cs", result[1].FilePath);
    }

    [Fact]
    public void Parse_MethodAdded()
    {
        var diff = """
            diff --git a/Services/TransferService.cs b/Services/TransferService.cs
            index abc..def 100644
            --- a/Services/TransferService.cs
            +++ b/Services/TransferService.cs
            @@ -20,6 +20,10 @@ public class TransferService
            +    public async Task<Transfer> ExecuteTransfer(TransferRequest request)
            +    {
            +        throw new NotImplementedException();
            +    }
            """;

        var result = _parser.Parse(diff);

        Assert.Single(result);
        Assert.Equal(4, result[0].AddedLines.Count);
        Assert.Empty(result[0].RemovedLines);
    }

    [Fact]
    public void Parse_MultipleHunks()
    {
        var diff = """
            diff --git a/Models/Invoice.cs b/Models/Invoice.cs
            index abc..def 100644
            --- a/Models/Invoice.cs
            +++ b/Models/Invoice.cs
            @@ -5,7 +5,7 @@ public class Invoice
            -    public decimal TotalAmount { get; set; }
            +    public decimal? TotalAmount { get; set; }
            @@ -20,6 +20,7 @@ public class Invoice
            +    public string TaxNumber { get; set; }
            """;

        var result = _parser.Parse(diff);

        Assert.Single(result);
        Assert.Equal(2, result[0].Hunks.Count);
        Assert.Single(result[0].RemovedLines);
        Assert.Equal(2, result[0].AddedLines.Count);
    }
}
