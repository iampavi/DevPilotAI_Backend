using System;
using System.Linq;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Infrastructure.Services;

namespace DevPilotAI.UnitTests.Services;

public class RoslynCSharpParserTests
{
    private readonly ICSharpParser _parser;

    public RoslynCSharpParserTests()
    {
        _parser = new RoslynCSharpParser();
    }

    [Fact]
    public void ParseContent_ShouldExtractAllSyntaxStructuresCorrectly()
    {
        // Arrange
        var sourceCode = @"
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace TestNamespace.Sub;

[ApiController]
[Route(""api/[controller]"")]
public class OrderService : IOrderService, IDisposable
{
    private readonly ILogger _logger;
    public string ConnectionString { get; set; }

    [Authorize]
    [HttpPost]
    public async Task<string> CreateOrderAsync(int id, string name)
    {
        return ""success"";
    }

    public OrderService()
    {
    }
}

public interface IOrderService {}
public record OrderRecord(int Id, string Code);
";

        // Act
        var result = _parser.ParseContent(sourceCode);

        // Assert
        Assert.True(result.IsSuccess);
        
        // 1. Usings check
        Assert.Contains("System", result.Value.Usings);
        Assert.Contains("System.Threading.Tasks", result.Value.Usings);
        Assert.Contains("Microsoft.AspNetCore.Mvc", result.Value.Usings);

        // 2. Classes/Interfaces/Records check
        Assert.Equal(3, result.Value.Classes.Count);

        // Class details
        var orderService = result.Value.Classes.First(c => c.Name == "OrderService");
        Assert.Equal("TestNamespace.Sub.OrderService", orderService.FullName);
        Assert.Equal("TestNamespace.Sub", orderService.Namespace);
        Assert.Equal("Class", orderService.SymbolType);
        Assert.Contains("IOrderService", orderService.BaseTypes);
        Assert.Contains("IDisposable", orderService.BaseTypes);
        Assert.Contains("ApiController", orderService.Attributes);
        Assert.Contains("Route", orderService.Attributes);

        // Field check
        Assert.Single(orderService.Fields);
        var loggerField = orderService.Fields.First();
        Assert.Equal("_logger", loggerField.Name);
        Assert.Equal("ILogger", loggerField.Type);
        Assert.Equal("private", loggerField.AccessModifier);

        // Property check
        Assert.Single(orderService.Properties);
        var connProp = orderService.Properties.First();
        Assert.Equal("ConnectionString", connProp.Name);
        Assert.Equal("string", connProp.Type);
        Assert.Equal("public", connProp.AccessModifier);

        // Method check
        Assert.Equal(2, orderService.Methods.Count); // CreateOrderAsync and constructor

        var method = orderService.Methods.First(m => m.Name == "CreateOrderAsync");
        Assert.Equal("Task<string>", method.ReturnType);
        Assert.Equal("public", method.AccessModifier);
        Assert.Contains("int id", method.Parameters);
        Assert.Contains("string name", method.Parameters);
        Assert.Contains("Authorize", method.Attributes);
        Assert.Contains("HttpPost", method.Attributes);

        // Interface details
        var orderInterface = result.Value.Classes.First(c => c.Name == "IOrderService");
        Assert.Equal("Interface", orderInterface.SymbolType);

        // Record details
        var orderRecord = result.Value.Classes.First(c => c.Name == "OrderRecord");
        Assert.Equal("Record", orderRecord.SymbolType);
    }
}
