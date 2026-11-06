using AnalysisEngine.Core.DependencyGraph;

namespace AnalysisEngine.Tests.Unit;

public class DependencyResolverTests
{
    private static readonly string TestRegistryJson = """
    {
      "version": "1.0.0",
      "lastUpdated": "2026-04-09",
      "services": {
        "customer-service": {
          "name": "CustomerService",
          "path": "../customer-service/src",
          "port": 5001,
          "endpoints": [
            { "method": "GET", "path": "/api/customers/{id}", "responseModel": "CustomerResponse" }
          ],
          "calls": [],
          "filePatterns": {}
        },
        "transfer-service": {
          "name": "TransferService",
          "path": "../transfer-service/src",
          "port": 5002,
          "endpoints": [
            { "method": "POST", "path": "/api/transfers", "responseModel": "TransferResponse" },
            { "method": "GET", "path": "/api/transfers/{id}", "responseModel": "TransferResponse" }
          ],
          "calls": [
            {
              "service": "customer-service",
              "endpoint": "GET /api/customers/{id}",
              "clientFile": "TransferService.Infrastructure/HttpClients/CustomerServiceClient.cs",
              "purpose": "Customer validation"
            }
          ],
          "filePatterns": {}
        },
        "invoice-service": {
          "name": "InvoiceService",
          "path": "../invoice-service/src",
          "port": 5003,
          "endpoints": [],
          "calls": [
            {
              "service": "customer-service",
              "endpoint": "GET /api/customers/{id}",
              "clientFile": "InvoiceService.Infrastructure/HttpClients/CustomerServiceClient.cs",
              "purpose": "Customer info for invoice"
            }
          ],
          "filePatterns": {}
        },
        "payment-service": {
          "name": "PaymentService",
          "path": "../payment-service/src",
          "port": 5005,
          "endpoints": [],
          "calls": [
            {
              "service": "customer-service",
              "endpoint": "GET /api/customers/{id}",
              "clientFile": "PaymentService.Infrastructure/HttpClients/CustomerServiceClient.cs",
              "purpose": "Merchant validation"
            },
            {
              "service": "transfer-service",
              "endpoint": "POST /api/transfers",
              "clientFile": "PaymentService.Infrastructure/HttpClients/TransferServiceClient.cs",
              "purpose": "Settlement transfer"
            }
          ],
          "filePatterns": {}
        },
        "refund-service": {
          "name": "RefundService",
          "path": "../refund-service/src",
          "port": 5004,
          "endpoints": [],
          "calls": [
            {
              "service": "transfer-service",
              "endpoint": "GET /api/transfers/{id}",
              "clientFile": "RefundService.Infrastructure/HttpClients/TransferServiceClient.cs",
              "purpose": "Original transfer verification"
            }
          ],
          "filePatterns": {}
        }
      }
    }
    """;

    private DependencyResolver CreateResolver()
    {
        var registry = ServiceRegistry.LoadFromJson(TestRegistryJson);
        return new DependencyResolver(registry);
    }

    [Fact]
    public void Resolve_CustomerService_FindsDirectConsumers()
    {
        var resolver = CreateResolver();
        var affected = resolver.Resolve("customer-service");

        // Direct consumers: transfer, invoice, payment
        var directNames = affected.Where(a => a.Hop == 1).Select(a => a.ServiceName).ToList();

        Assert.Contains("TransferService", directNames);
        Assert.Contains("InvoiceService", directNames);
        Assert.Contains("PaymentService", directNames);
    }

    [Fact]
    public void Resolve_CustomerService_FindsTransitiveConsumers()
    {
        var resolver = CreateResolver();
        var affected = resolver.Resolve("customer-service");

        // Transitive: refund-service calls transfer-service which calls customer-service
        var transitiveNames = affected.Where(a => a.Hop == 2).Select(a => a.ServiceName).ToList();

        Assert.Contains("RefundService", transitiveNames);
    }

    [Fact]
    public void Resolve_CustomerService_TotalAffectedCount()
    {
        var resolver = CreateResolver();
        var affected = resolver.Resolve("customer-service");

        // 3 direct + 1 transitive = 4
        // But PaymentService is already direct, so refund is the only transitive
        Assert.Equal(4, affected.Count);
    }

    [Fact]
    public void Resolve_TransferService_FindsConsumers()
    {
        var resolver = CreateResolver();
        var affected = resolver.Resolve("transfer-service");

        var names = affected.Select(a => a.ServiceName).ToList();
        Assert.Contains("PaymentService", names);
        Assert.Contains("RefundService", names);
    }

    [Fact]
    public void Resolve_RefundService_NoConsumers()
    {
        var resolver = CreateResolver();
        var affected = resolver.Resolve("refund-service");

        Assert.Empty(affected);
    }

    [Fact]
    public void Resolve_TransitiveHasViaField()
    {
        var resolver = CreateResolver();
        var affected = resolver.Resolve("customer-service");

        var transitive = affected.Where(a => a.Hop == 2).ToList();
        Assert.All(transitive, a => Assert.NotNull(a.Via));
    }

    [Fact]
    public void Resolve_DirectConsumersHaveClientFile()
    {
        var resolver = CreateResolver();
        var affected = resolver.Resolve("customer-service");

        var direct = affected.Where(a => a.Hop == 1).ToList();
        Assert.All(direct, a => Assert.False(string.IsNullOrEmpty(a.ClientFile)));
    }

    [Fact]
    public void Registry_LoadFromJson_ParsesCorrectly()
    {
        var registry = ServiceRegistry.LoadFromJson(TestRegistryJson);

        Assert.Equal(5, registry.Services.Count);
        Assert.True(registry.HasService("customer-service"));
        Assert.True(registry.HasService("transfer-service"));
        Assert.False(registry.HasService("nonexistent-service"));
    }

    [Fact]
    public void Registry_GetDirectConsumers_ReturnsCorrectCount()
    {
        var registry = ServiceRegistry.LoadFromJson(TestRegistryJson);
        var consumers = registry.GetDirectConsumers("customer-service");

        Assert.Equal(3, consumers.Count);
    }
}
