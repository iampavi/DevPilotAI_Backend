using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Common.Mappings;
using DevPilotAI.Application.DTOs.Chat;
using DevPilotAI.Application.DTOs.Project;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DevPilotAI.UnitTests.Services;

/// <summary>
/// Unit tests for RepositoryContextExpander.
/// Mocks IRepositoryGraphService to isolate BFS traversal logic from the database.
/// </summary>
public class RepositoryContextExpanderTests
{
    private readonly IMapper _mapper;

    public RepositoryContextExpanderTests()
    {
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
        }, NullLoggerFactory.Instance);
        _mapper = mapperConfig.CreateMapper();
    }

    private static IConfiguration BuildConfig(int depth = 2, int maxSymbols = 50) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "RagSettings:ContextExpansion:DefaultDepth", depth.ToString() },
                { "RagSettings:ContextExpansion:MaxSymbols", maxSymbols.ToString() }
            }!)
            .Build();

    /// Helper to create a fully initialized GraphSymbolNode with SymbolId set.
    private static GraphSymbolNode MakeNode(
        string name,
        string ns = "DevPilot",
        List<string>? baseTypes = null,
        List<string>? ctorParams = null,
        List<string>? fields = null,
        List<string>? properties = null,
        List<string>? methodTypes = null) =>
        new()
        {
            SymbolId = $"{ns}::{name}",
            Name = name,
            Namespace = ns,
            FilePath = $"{ns.Replace('.', '/')}/{name}.cs",
            BaseTypes = baseTypes ?? new List<string>(),
            ConstructorParameters = ctorParams ?? new List<string>(),
            Fields = fields ?? new List<string>(),
            Properties = properties ?? new List<string>(),
            MethodParameterAndReturnTypes = methodTypes ?? new List<string>()
        };

    // ───────────────────────────────────────────────────────────────
    // CORE BEHAVIOUR
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExpandContextAsync_EmptySeeds_ReturnsEmptyContext()
    {
        var graphMock = new Mock<IRepositoryGraphService>();
        graphMock.Setup(g => g.GetProjectGraphNodesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<GraphSymbolNode>());
        graphMock.Setup(g => g.GetChunksForSymbolsAsync(It.IsAny<Guid>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<CodeChunk>());

        var expander = new RepositoryContextExpander(graphMock.Object, _mapper, BuildConfig());

        var result = await expander.ExpandContextAsync(
            Guid.NewGuid(), new List<CodeChunkDto>(), new List<string>(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result.SeedChunks);
        Assert.Empty(result.ExpandedChunks);
        Assert.Empty(result.Relationships);
        Assert.Empty(result.ReferencedSymbols);
    }

    [Fact]
    public async Task ExpandContextAsync_SeedWithBaseType_BuildsImplementsRelationship()
    {
        var projectId = Guid.NewGuid();

        var repoNode = MakeNode("UserRepository", baseTypes: new List<string> { "IUserRepository" });

        var graphMock = new Mock<IRepositoryGraphService>();
        graphMock.Setup(g => g.GetProjectGraphNodesAsync(projectId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<GraphSymbolNode> { repoNode });
        graphMock.Setup(g => g.GetChunksForSymbolsAsync(projectId, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<CodeChunk>());

        var seed = new CodeChunkDto
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ChunkType = "Class",
            Content = "public class UserRepository : IUserRepository { }",
            Metadata = "{\"class_name\":\"UserRepository\",\"symbol_name\":\"UserRepository\",\"file_path\":\"Repositories/UserRepository.cs\"}"
        };

        var expander = new RepositoryContextExpander(graphMock.Object, _mapper, BuildConfig());
        var result = await expander.ExpandContextAsync(projectId, new List<CodeChunkDto> { seed }, new List<string>(), CancellationToken.None);

        Assert.Contains(result.Relationships,
            r => r.FromSymbol == "UserRepository" && r.RelationshipType == "implements" && r.ToSymbol == "IUserRepository");
        Assert.Contains(result.ReferencedSymbols, s => s.Equals("UserRepository", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExpandContextAsync_SeedWithConstructorInjection_BuildsInjectsRelationship()
    {
        var projectId = Guid.NewGuid();

        var serviceNode = MakeNode("OrderService", ctorParams: new List<string> { "IOrderRepository" });

        var graphMock = new Mock<IRepositoryGraphService>();
        graphMock.Setup(g => g.GetProjectGraphNodesAsync(projectId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<GraphSymbolNode> { serviceNode });
        graphMock.Setup(g => g.GetChunksForSymbolsAsync(projectId, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<CodeChunk>());

        var seed = new CodeChunkDto
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ChunkType = "Class",
            Content = "public class OrderService { }",
            Metadata = "{\"class_name\":\"OrderService\",\"symbol_name\":\"OrderService\"}"
        };

        var expander = new RepositoryContextExpander(graphMock.Object, _mapper, BuildConfig());
        var result = await expander.ExpandContextAsync(projectId, new List<CodeChunkDto> { seed }, new List<string>(), CancellationToken.None);

        Assert.Contains(result.Relationships,
            r => r.FromSymbol == "OrderService" && r.RelationshipType == "injects" && r.ToSymbol == "IOrderRepository");
    }

    [Fact]
    public async Task ExpandContextAsync_ExpandedChunks_DeduplicatedAcrossSeeds()
    {
        var projectId = Guid.NewGuid();
        var sharedChunkId = Guid.NewGuid();

        var nodeA = MakeNode("ServiceA", ctorParams: new List<string> { "SharedHelper" });
        var nodeB = MakeNode("ServiceB", ctorParams: new List<string> { "SharedHelper" });

        var sharedChunk = new CodeChunk
        {
            Id = sharedChunkId,
            ProjectId = projectId,
            ChunkType = "Class",
            Content = "public class SharedHelper { }",
            Metadata = "{\"class_name\":\"SharedHelper\"}"
        };

        var graphMock = new Mock<IRepositoryGraphService>();
        graphMock.Setup(g => g.GetProjectGraphNodesAsync(projectId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<GraphSymbolNode> { nodeA, nodeB });
        graphMock.Setup(g => g.GetChunksForSymbolsAsync(projectId, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<CodeChunk> { sharedChunk });

        var seed1Id = Guid.NewGuid();
        var seed2Id = Guid.NewGuid();

        var expander = new RepositoryContextExpander(graphMock.Object, _mapper, BuildConfig());
        var result = await expander.ExpandContextAsync(
            projectId,
            new List<CodeChunkDto>
            {
                new() { Id = seed1Id, ProjectId = projectId, ChunkType = "Class", Content = "class ServiceA {}", Metadata = "{\"class_name\":\"ServiceA\"}" },
                new() { Id = seed2Id, ProjectId = projectId, ChunkType = "Class", Content = "class ServiceB {}", Metadata = "{\"class_name\":\"ServiceB\"}" }
            },
            new List<string>(),
            CancellationToken.None);

        // SharedHelper should appear exactly once even though both seeds reference it
        Assert.Single(result.ExpandedChunks, c => c.Id == sharedChunkId);
    }

    [Fact]
    public async Task ExpandContextAsync_AdditionalSymbols_SeedBFSTraversal()
    {
        var projectId = Guid.NewGuid();

        var repoNode = MakeNode("OrderRepository");

        var extraChunk = new CodeChunk
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ChunkType = "Class",
            Content = "public class OrderRepository { }",
            Metadata = "{\"class_name\":\"OrderRepository\",\"symbol_name\":\"OrderRepository\"}"
        };

        var graphMock = new Mock<IRepositoryGraphService>();
        graphMock.Setup(g => g.GetProjectGraphNodesAsync(projectId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<GraphSymbolNode> { repoNode });
        graphMock.Setup(g => g.GetChunksForSymbolsAsync(projectId, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<CodeChunk> { extraChunk });

        var expander = new RepositoryContextExpander(graphMock.Object, _mapper, BuildConfig());
        var result = await expander.ExpandContextAsync(
            projectId,
            new List<CodeChunkDto>(),                     // no seed chunks
            new List<string> { "OrderRepository" },        // but an explicit additional symbol
            CancellationToken.None);

        Assert.Contains(result.ExpandedChunks, c => c.Id == extraChunk.Id);
        Assert.Contains(result.ReferencedSymbols, s => s.Equals("OrderRepository", StringComparison.OrdinalIgnoreCase));
    }

    // ───────────────────────────────────────────────────────────────
    // DUPLICATE SYMBOL NAME SAFETY
    // These tests verify the root-cause fix: BFS must NOT crash when
    // two classes in different namespaces share the same short name.
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExpandContextAsync_DuplicateShortNames_DoesNotThrow()
    {
        // Arrange — AddProfileImage exists in BOTH UserController AND AdminController
        var projectId = Guid.NewGuid();

        var userCtrl   = MakeNode("AddProfileImage", ns: "Practice.Controllers.User");
        var adminCtrl  = MakeNode("AddProfileImage", ns: "Practice.Controllers.Admin");

        var graphMock = new Mock<IRepositoryGraphService>();
        graphMock.Setup(g => g.GetProjectGraphNodesAsync(projectId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<GraphSymbolNode> { userCtrl, adminCtrl });
        graphMock.Setup(g => g.GetChunksForSymbolsAsync(projectId, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<CodeChunk>());

        var seed = new CodeChunkDto
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ChunkType = "Class",
            Content = "// some chunk referencing AddProfileImage",
            Metadata = "{\"class_name\":\"AddProfileImage\"}"
        };

        var expander = new RepositoryContextExpander(graphMock.Object, _mapper, BuildConfig());

        // Act & Assert — must NOT throw "An item with the same key has already been added"
        var result = await expander.ExpandContextAsync(
            projectId, new List<CodeChunkDto> { seed }, new List<string>(), CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExpandContextAsync_InterfaceAndImplementationSameMethodName_ExpandsBoth()
    {
        // IUserRepository.Get and UserRepository.Get share the symbol name "Get"
        // when extracted from type references — BFS should handle both without crashing.
        var projectId = Guid.NewGuid();

        var iRepo  = MakeNode("IUserRepository", ns: "Practice.Contracts");
        var repo   = MakeNode("UserRepository",  ns: "Practice.Repositories",
                               baseTypes: new List<string> { "IUserRepository" });

        var graphMock = new Mock<IRepositoryGraphService>();
        graphMock.Setup(g => g.GetProjectGraphNodesAsync(projectId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<GraphSymbolNode> { iRepo, repo });
        graphMock.Setup(g => g.GetChunksForSymbolsAsync(projectId, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<CodeChunk>());

        var seed = new CodeChunkDto
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ChunkType = "Class",
            Content = "public class UserRepository : IUserRepository { }",
            Metadata = "{\"class_name\":\"UserRepository\"}"
        };

        var expander = new RepositoryContextExpander(graphMock.Object, _mapper, BuildConfig());
        var result = await expander.ExpandContextAsync(
            projectId, new List<CodeChunkDto> { seed }, new List<string>(), CancellationToken.None);

        // implements relationship must be present
        Assert.Contains(result.Relationships,
            r => r.FromSymbol == "UserRepository" && r.RelationshipType == "implements" && r.ToSymbol == "IUserRepository");
    }

    [Fact]
    public async Task ExpandContextAsync_DuplicateNamesAcrossNamespaces_VisitsAllUniqueSymbolIds()
    {
        // Update (method) exists in UserService, OrderService, ProductService
        // BFS should visit all three via SymbolId, not collapse them into one.
        var projectId = Guid.NewGuid();

        var userSvc    = MakeNode("Update", ns: "Practice.Services.User");
        var orderSvc   = MakeNode("Update", ns: "Practice.Services.Order");
        var productSvc = MakeNode("Update", ns: "Practice.Services.Product");

        var graphMock = new Mock<IRepositoryGraphService>();
        graphMock.Setup(g => g.GetProjectGraphNodesAsync(projectId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<GraphSymbolNode> { userSvc, orderSvc, productSvc });
        graphMock.Setup(g => g.GetChunksForSymbolsAsync(projectId, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<CodeChunk>());

        var seed = new CodeChunkDto
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ChunkType = "Class",
            Content = "// referencing Update",
            Metadata = "{\"class_name\":\"Update\"}"
        };

        var expander = new RepositoryContextExpander(graphMock.Object, _mapper, BuildConfig());

        // Must not throw, and must have visited all three SymbolIds
        var result = await expander.ExpandContextAsync(
            projectId, new List<CodeChunkDto> { seed }, new List<string>(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3, result.ExpandedSymbolCount); // visited all 3 unique SymbolIds
    }
}
