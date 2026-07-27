using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Chat;
using DevPilotAI.Application.DTOs.Copilot;
using DevPilotAI.Application.DTOs.Project;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Entities.Identity;
using DevPilotAI.Infrastructure.Persistence;
using DevPilotAI.Infrastructure.Services;
using DevPilotAI.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DevPilotAI.UnitTests.Services;

public class CopilotServiceTests
{
    private readonly Mock<ISemanticRetrievalService> _retrievalMock;
    private readonly Mock<IPromptBuilder> _promptBuilderMock;
    private readonly Mock<IChatProviderFactory> _factoryMock;
    private readonly Mock<IChatProvider> _providerMock;
    private readonly Mock<IRepositoryContextExpander> _expanderMock;
    private readonly Mock<IRepositoryGraphService> _graphServiceMock;
    private readonly ApplicationDbContext _context;

    public CopilotServiceTests()
    {
        _retrievalMock = new Mock<ISemanticRetrievalService>();
        _promptBuilderMock = new Mock<IPromptBuilder>();
        _factoryMock = new Mock<IChatProviderFactory>();
        _providerMock = new Mock<IChatProvider>();
        _expanderMock = new Mock<IRepositoryContextExpander>();
        _graphServiceMock = new Mock<IRepositoryGraphService>();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=DevPilotAI_CopilotServiceTests;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        _context = new ApplicationDbContext(options, new DateTimeProvider(), new Mock<ICurrentUserService>().Object);
        _context.Database.EnsureDeleted();
        _context.Database.EnsureCreated();

        _expanderMock.Setup(e => e.ExpandContextAsync(It.IsAny<Guid>(), It.IsAny<List<CodeChunkDto>>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, List<CodeChunkDto> seeds, List<string> _, CancellationToken _) => new RepositoryContextDto
            {
                SeedChunks = seeds,
                Relationships = new List<RepositoryRelationshipDto> { new() { FromSymbol = "AuthService", RelationshipType = "implements", ToSymbol = "IAuthService" } }
            });

        _graphServiceMock
            .Setup(g => g.GetArchitectureChunksAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CodeChunk>());

        // Setup common provider mocks
        _providerMock.Setup(p => p.ProviderName).Returns("Mock");
        _providerMock.Setup(p => p.GetResponseAsync(It.IsAny<List<ChatMessageDto>>(), It.IsAny<ChatSettingsDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponseDto { Content = "Generated Copilot Analysis", TokenCount = 100 });

        _factoryMock.Setup(f => f.GetProvider(It.IsAny<string>())).Returns(_providerMock.Object);
    }

    [Theory]
    [InlineData(CopilotMode.Review, "ReviewCode")]
    [InlineData(CopilotMode.BugAnalysis, "FindBugs")]
    [InlineData(CopilotMode.Refactor, "RefactorCode")]
    [InlineData(CopilotMode.UnitTests, "GenerateTests")]
    [InlineData(CopilotMode.ApiDocumentation, "ExplainApi")]
    [InlineData(CopilotMode.Architecture, "ExplainArchitecture")]
    [InlineData(CopilotMode.Navigation, "NavigateCode")]
    [InlineData(CopilotMode.ImpactAnalysis, "AnalyzeImpact")]
    [InlineData(CopilotMode.DependencyGraph, "ExplainDependencies")]
    public async Task ExecuteAsync_ShouldMapModeToTemplateCorrectly(CopilotMode mode, string expectedTemplateName)
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var request = new CopilotRequest
        {
            Mode = mode,
            Target = "AuthService.cs",
            AdditionalInstructions = "Be thorough"
        };

        var chunks = new List<CodeChunkDto>
        {
            new() { Id = Guid.NewGuid(), Content = "public class AuthService { }", ChunkType = "Class", Metadata = "{\"file_path\":\"Services/AuthService.cs\",\"symbol_name\":\"AuthService\"}", RetrievalExplanation = "Exact SymbolName Match" }
        };

        var detailedResult = new DetailedRetrievalResult
        {
            Chunks = chunks,
            CandidateChunks = 1,
            FilteredChunks = 0,
            FinalChunks = 1,
            IgnoredReasons = new List<string>(),
            AverageSimilarity = 0.95,
            RetrievalTime = TimeSpan.FromMilliseconds(50)
        };

        _retrievalMock.Setup(r => r.RetrieveDetailedContextAsync(projectId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(detailedResult);

        _promptBuilderMock.Setup(p => p.BuildRagPrompt(expectedTemplateName, It.IsAny<string>(), It.IsAny<RepositoryContextDto>(), It.IsAny<List<ChatMessageDto>>()))
            .Returns(new List<ChatMessageDto>());

        var inMemorySettings = new Dictionary<string, string> {
            {"ChatSettings:Provider", "Mock"},
            {"ChatSettings:Model", "gpt-4"}
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();

        var copilotService = new CopilotService(
            _retrievalMock.Object,
            _promptBuilderMock.Object,
            _factoryMock.Object,
            config,
            NullLogger<CopilotService>.Instance,
            _expanderMock.Object,
            _graphServiceMock.Object
        );

        // Act
        var response = await copilotService.ExecuteAsync(projectId, request, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(expectedTemplateName, response.PromptMode);
        Assert.Equal("Mock", response.Provider);
        Assert.Equal(100, response.TokenCount);
        Assert.Single(response.Sources);
        Assert.Equal("Services/AuthService.cs", response.Sources[0].FilePath);
        Assert.Equal("AuthService", response.Sources[0].SymbolName);
        Assert.True(response.Duration > TimeSpan.Zero);
        Assert.NotNull(response.Metrics);
        Assert.Equal(1, response.Metrics.FinalChunks);
    }
}
