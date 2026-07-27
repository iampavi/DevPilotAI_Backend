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
using DevPilotAI.Domain.Entities.Identity;
using DevPilotAI.Infrastructure.Persistence;
using DevPilotAI.Infrastructure.Services;
using DevPilotAI.Infrastructure.Services.ChatProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace DevPilotAI.UnitTests.Services;

public class AiChatTests
{
    private readonly IMapper _mapper;

    public AiChatTests()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
        }, NullLoggerFactory.Instance);
        _mapper = configuration.CreateMapper();
    }

    [Fact]
    public void PromptBuilder_ShouldBuildPromptAndBudgetHistory()
    {
        var contextChunks = new List<CodeChunkDto>
        {
            new() { Content = "public class Order { }", Metadata = "Order.cs" }
        };

        var history = new List<ChatMessageDto>
        {
            new() { Role = "user", Content = "Message 1", TokenCount = 15 },
            new() { Role = "assistant", Content = "Message 2", TokenCount = 15 },
            new() { Role = "user", Content = "Message 3", TokenCount = 15 },
            new() { Role = "assistant", Content = "Message 4", TokenCount = 15 },
            new() { Role = "user", Content = "Message 5", TokenCount = 15 },
            new() { Role = "assistant", Content = "Message 6", TokenCount = 15 }
        };

        var userQuestion = "How does Order work?"; 

        var tightInMemorySettings = new Dictionary<string, string> {
            // Use a generous token budget so that the full history fits;
            // this validates that BuildRagPrompt correctly threads history through.
            {"RagSettings:MaxPromptTokens", "2000"},
            {"RagSettings:PromptTemplates:ExplainCode", "Explain context: {context}\nQuery: {question}"}
        };
        var tightConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(tightInMemorySettings!)
            .Build();
        var tightPromptBuilder = new PromptBuilder(tightConfig);

        var result = tightPromptBuilder.BuildRagPrompt("ExplainCode", userQuestion, new RepositoryContextDto { SeedChunks = contextChunks }, history);

        // system + 6 history messages + user = 8
        Assert.Equal(8, result.Count);
        Assert.Equal("system", result[0].Role);
        // First history entry
        Assert.Equal("user", result[1].Role);
        Assert.Equal("Message 1", result[1].Content);
        // Last history entry
        Assert.Equal("assistant", result[6].Role);
        Assert.Equal("Message 6", result[6].Content);
        // Final user question
        Assert.Equal("user", result[7].Role);
        Assert.Equal(userQuestion, result[7].Content);
    }

    [Fact]
    public async Task SemanticRetrieval_ShouldRetrieveAndRankContext()
    {
        var qdrantMock = new Mock<IQdrantService>();
        var embeddingMock = new Mock<IEmbeddingService>();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=DevPilotAI_AiChatTests_Retrieval;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        var context = new ApplicationDbContext(options, new DateTimeProvider(), new Mock<ICurrentUserService>().Object);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        try
        {
            var systemUserId = Guid.Parse("D035B9FE-B7FE-438B-B0D1-1C349C3AF21F");
            var systemUser = new ApplicationUser
            {
                Id = systemUserId,
                UserName = "retrieval_system@devpilot.ai",
                Email = "retrieval_system@devpilot.ai",
                FirstName = "System",
                LastName = "User",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            context.Users.Add(systemUser);
            await context.SaveChangesAsync();

            var workspaceId = Guid.NewGuid();
            var workspace = new Workspace 
            { 
                Id = workspaceId, 
                Name = "TestWorkspace",
                UserId = systemUserId
            };
            context.Workspaces.Add(workspace);

            var projectId = Guid.NewGuid();
            var project = new Project
            {
                Id = projectId,
                WorkspaceId = workspaceId,
                Name = "ChatTestProject",
                SourceLocation = "Local"
            };
            context.Projects.Add(project);

            var fileId = Guid.NewGuid();
            var parsedFile = new ParsedFile
            {
                Id = fileId,
                ProjectId = projectId,
                RelativePath = "Order.cs",
                Language = "CSharp",
                SizeInBytes = 100,
                Usings = new List<string>()
            };
            context.ParsedFiles.Add(parsedFile);
            await context.SaveChangesAsync();

            var chunk1 = new CodeChunk { Id = Guid.NewGuid(), ProjectId = projectId, ParsedFileId = fileId, Content = "public class OrderService { }", ChunkType = "Class" };
            var chunk2 = new CodeChunk { Id = Guid.NewGuid(), ProjectId = projectId, ParsedFileId = fileId, Content = "public class CustomerService { }", ChunkType = "Class" };
            var chunk3 = new CodeChunk { Id = Guid.NewGuid(), ProjectId = projectId, ParsedFileId = fileId, Content = "public class LoggingService { }", ChunkType = "Class" };

            context.CodeChunks.AddRange(chunk1, chunk2, chunk3);
            await context.SaveChangesAsync();

            embeddingMock.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[1536]);

            qdrantMock.Setup(q => q.SearchSimilarityAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Guid>
                {
                    chunk1.Id,
                    chunk2.Id,
                    chunk3.Id
                });

            // Set high threshold of 0.92, filtering out chunk2 (score 0.90) and chunk3 (score 0.85)
            var inMemorySettings = new Dictionary<string, string> {
                {"RagSettings:TopK", "3"},
                {"RagSettings:SimilarityThreshold", "0.92"},
                {"RagSettings:MaxContextChunks", "5"}
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();
            var symbolGraphResolver = new SymbolGraphResolver(context);

            var retrievalService = new SemanticRetrievalService(qdrantMock.Object, embeddingMock.Object, context, config, NullLogger<SemanticRetrievalService>.Instance, symbolGraphResolver);

            var results = await retrievalService.RetrieveRelevantContextAsync(projectId, "How does OrderService initialize?", CancellationToken.None);

            Assert.Single(results); 
            Assert.Equal(chunk1.Id, results[0].Id); 
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task AiChatService_ShouldProcessMessageAndTriggerSummarization()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=DevPilotAI_AiChatTests_Orchestrator;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        var context = new ApplicationDbContext(options, new DateTimeProvider(), new Mock<ICurrentUserService>().Object);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        try
        {
            var systemUserId = Guid.Parse("D035B9FE-B7FE-438B-B0D1-1C349C3AF21F");
            var systemUser = new ApplicationUser
            {
                Id = systemUserId,
                UserName = "orchestrator_system@devpilot.ai",
                Email = "orchestrator_system@devpilot.ai",
                FirstName = "System",
                LastName = "User",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            context.Users.Add(systemUser);
            await context.SaveChangesAsync();

            var workspaceId = Guid.NewGuid();
            var workspace = new Workspace 
            { 
                Id = workspaceId, 
                Name = "TestWorkspace",
                UserId = systemUserId
            };
            context.Workspaces.Add(workspace);

            var projectId = Guid.NewGuid();
            var project = new Project
            {
                Id = projectId,
                WorkspaceId = workspaceId,
                Name = "ChatTestProject",
                SourceLocation = "Local"
            };
            context.Projects.Add(project);

            var session = new ChatSession
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = "Session 1",
                CreatedAt = DateTime.UtcNow
            };
            context.ChatSessions.Add(session);

            for (int i = 0; i < 4; i++)
            {
                context.ChatMessages.Add(new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ChatSessionId = session.Id,
                    Role = i % 2 == 0 ? "user" : "assistant",
                    Content = $"Test Message {i}",
                    TokenCount = 10,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5 + i)
                });
            }
            await context.SaveChangesAsync();

            var retrievalMock = new Mock<ISemanticRetrievalService>();
            retrievalMock.Setup(r => r.RetrieveDetailedContextAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DetailedRetrievalResult {
                    Chunks = new List<CodeChunkDto> {
                        new CodeChunkDto {
                            Id = Guid.NewGuid(),
                            ChunkType = "Class",
                            Content = "class OrderService {}",
                            Metadata = "{\"symbol_name\":\"OrderService\",\"file_path\":\"OrderService.cs\"}",
                            RetrievalExplanation = "Exact SymbolName match"
                        }
                    },
                    FinalChunks = 1,
                    CandidateChunks = 1,
                    FilteredChunks = 0,
                    AverageSimilarity = 0.8
                });

            var promptBuilderMock = new Mock<IPromptBuilder>();
            promptBuilderMock.Setup(p => p.BuildRagPrompt(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RepositoryContextDto>(), It.IsAny<List<ChatMessageDto>>()))
                .Returns(new List<ChatMessageDto>());

            var providerMock = new Mock<IChatProvider>();
            providerMock.Setup(p => p.ProviderName).Returns("Mock");
            providerMock.Setup(p => p.GetResponseAsync(It.IsAny<List<ChatMessageDto>>(), It.IsAny<ChatSettingsDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChatResponseDto { Content = "Summarized key points...", TokenCount = 15 });

            var factoryMock = new Mock<IChatProviderFactory>();
            factoryMock.Setup(f => f.GetProvider("Mock")).Returns(providerMock.Object);

            var inMemorySettings = new Dictionary<string, string> {
                {"RagSettings:SummarizeAfterMessagesCount", "5"},
                {"EmbeddingSettings:Provider", "Mock"},
                {"EmbeddingSettings:Model", "gpt-4"}
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();

            var graphServiceMock = new Mock<IRepositoryGraphService>();
            var contextExpanderMock = new Mock<IRepositoryContextExpander>();
            contextExpanderMock.Setup(e => e.ExpandContextAsync(It.IsAny<Guid>(), It.IsAny<List<CodeChunkDto>>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, List<CodeChunkDto> seeds, List<string> _, CancellationToken _) => new RepositoryContextDto
                {
                    SeedChunks = seeds,
                    ReferencedSymbols = new List<string> { "OrderService" },
                    RelatedFiles = new List<string> { "OrderService.cs" }
                });

            var chatService = new AiChatService(
                context,
                retrievalMock.Object,
                promptBuilderMock.Object,
                factoryMock.Object,
                _mapper,
                config,
                NullLogger<AiChatService>.Instance,
                contextExpanderMock.Object,
                graphServiceMock.Object
            );

            var response = await chatService.SendMessageAsync(session.Id, "Question?", "ExplainCode", null, CancellationToken.None);

            Assert.NotNull(response);
            Assert.Equal("assistant", response.Role);

            var dbMessages = await context.ChatMessages
                .Where(m => m.ChatSessionId == session.Id)
                .ToListAsync();

            Assert.Single(dbMessages);
            Assert.Equal("system", dbMessages[0].Role);
            Assert.Contains("[Summary of earlier discussion]", dbMessages[0].Content);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task GroqChatProvider_ShouldReturnResponse_WhenApiSucceeds()
    {
        var mockResponseJson = @"
        {
            ""choices"": [
                {
                    ""message"": {
                        ""role"": ""assistant"",
                        ""content"": ""Hello from Groq!""
                    }
                }
            ],
            ""usage"": {
                ""total_tokens"": 42
            }
        }";

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(mockResponseJson, System.Text.Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var inMemorySettings = new Dictionary<string, string> {
            {"ChatSettings:ApiKey", "test-key"},
            {"ChatSettings:BaseUrl", "https://api.groq.com/openai/v1"}
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();
        
        var provider = new GroqChatProvider(httpClient, config, NullLogger<GroqChatProvider>.Instance);

        var messages = new List<ChatMessageDto>
        {
            new() { Role = "user", Content = "Hi" }
        };
        var settings = new ChatSettingsDto { Model = "llama-3.3-70b-versatile" };

        var result = await provider.GetResponseAsync(messages, settings, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Hello from Groq!", result.Content);
        Assert.Equal(42, result.TokenCount);
    }

    [Fact]
    public async Task SemanticRetrieval_ShouldPrioritizeSymbolNameMatch()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=DevPilotAI_AiChatTests_SymbolMatch;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        var context = new ApplicationDbContext(options, new DateTimeProvider(), new Mock<ICurrentUserService>().Object);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        try
        {
            // Seed system user
            var systemUserId = "D035B9FE-B7FE-438B-B0D1-1C349C3AF21F";
            var systemUser = new ApplicationUser
            {
                Id = Guid.Parse(systemUserId),
                UserName = "system@devpilot.ai",
                Email = "system@devpilot.ai",
                FirstName = "System",
                LastName = "User"
            };
            context.Users.Add(systemUser);
            await context.SaveChangesAsync();

            var workspaceId = Guid.NewGuid();
            var workspace = new Workspace 
            { 
                Id = workspaceId, 
                Name = "TestWorkspace",
                UserId = Guid.Parse(systemUserId)
            };
            context.Workspaces.Add(workspace);

            var projectId = Guid.NewGuid();
            var project = new Project 
            { 
                Id = projectId, 
                WorkspaceId = workspaceId,
                Name = "Test Project",
                SourceLocation = "Local"
            };
            context.Projects.Add(project);
            await context.SaveChangesAsync();

            var fileId = Guid.NewGuid();
            var parsedFile = new ParsedFile
            {
                Id = fileId,
                ProjectId = projectId,
                RelativePath = "Repositories/ResumeRepository.cs",
                Language = "C#"
            };
            context.ParsedFiles.Add(parsedFile);
            
            var classId = Guid.NewGuid();
            var parsedClass = new ParsedClass
            {
                Id = classId,
                ParsedFileId = fileId,
                Name = "ResumeRepository",
                FullName = "Repositories.ResumeRepository"
            };
            context.ParsedClasses.Add(parsedClass);
            await context.SaveChangesAsync();

            // Chunk1 is unrelated but has high semantic score (sim 0.95, rank 0 in Qdrant)
            var chunk1 = new CodeChunk 
            { 
                Id = Guid.NewGuid(), 
                ProjectId = projectId, 
                ParsedFileId = fileId, 
                Content = "public class OrderService { }", 
                ChunkType = "Class",
                Metadata = "{\"symbol_name\":\"OrderService\",\"class_name\":\"OrderService\",\"file_path\":\"Services/OrderService.cs\"}"
            };
            
            // Chunk2 matches ResumeRepository class name (and has lower semantic score - sim 0.85, rank 2 in Qdrant)
            var chunk2 = new CodeChunk 
            { 
                Id = Guid.NewGuid(), 
                ProjectId = projectId, 
                ParsedFileId = fileId, 
                ParsedClassId = classId,
                Content = "public class ResumeRepository { }", 
                ChunkType = "Class",
                Metadata = "{\"symbol_name\":\"ResumeRepository\",\"class_name\":\"ResumeRepository\",\"file_path\":\"Repositories/ResumeRepository.cs\"}"
            };

            context.CodeChunks.AddRange(chunk1, chunk2);
            await context.SaveChangesAsync();

            var embeddingMock = new Mock<IEmbeddingService>();
            embeddingMock.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[1536]);

            var qdrantMock = new Mock<IQdrantService>();
            // Qdrant returns chunk1 (unrelated) first, and chunk2 (matching) second
            qdrantMock.Setup(q => q.SearchSimilarityAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Guid>
                {
                    chunk1.Id,
                    chunk2.Id
                });

            var inMemorySettings = new Dictionary<string, string> {
                {"RagSettings:TopK", "3"},
                {"RagSettings:SimilarityThreshold", "0.50"},
                {"RagSettings:MaxContextChunks", "5"}
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();
            var symbolGraphResolver = new SymbolGraphResolver(context);

            var retrievalService = new SemanticRetrievalService(qdrantMock.Object, embeddingMock.Object, context, config, NullLogger<SemanticRetrievalService>.Instance, symbolGraphResolver);

            // Act - Query specifically asks for "ResumeRepository"
            var results = await retrievalService.RetrieveRelevantContextAsync(projectId, "Explain ResumeRepository database implementation", CancellationToken.None);

            // Assert - chunk2 (ResumeRepository) should be retrieved first because of the Class Name boost (+1.0) and File Name boost (+1.5)
            Assert.Equal(2, results.Count);
            Assert.Equal(chunk2.Id, results[0].Id); // ResumeRepository is first!
            Assert.Equal(chunk1.Id, results[1].Id); // OrderService is second!
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task SendMessageAsync_ShouldMergeConfigurationDefaultsWithPartialOverrides()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=DevPilotAI_AiChatTests_Merge;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        using var context = new ApplicationDbContext(options, new DateTimeProvider(), new Mock<ICurrentUserService>().Object);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        try
        {
            var systemUserId = Guid.NewGuid();
            var systemUser = new ApplicationUser
            {
                Id = systemUserId,
                UserName = "system@devpilot.ai",
                Email = "system@devpilot.ai",
                FirstName = "System",
                LastName = "User"
            };
            context.Users.Add(systemUser);
            await context.SaveChangesAsync();

            var workspaceId = Guid.NewGuid();
            var workspace = new Workspace 
            { 
                Id = workspaceId, 
                Name = "TestWorkspace",
                UserId = systemUserId
            };
            context.Workspaces.Add(workspace);

            var projectId = Guid.NewGuid();
            var project = new Project
            {
                Id = projectId,
                WorkspaceId = workspaceId,
                Name = "Test Project",
                SourceLocation = "Local"
            };
            context.Projects.Add(project);
            await context.SaveChangesAsync();

            var session = new ChatSession
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = "Session",
                CreatedAt = DateTime.UtcNow
            };
            context.ChatSessions.Add(session);
            await context.SaveChangesAsync();

            var retrievalMock = new Mock<ISemanticRetrievalService>();
            retrievalMock.Setup(r => r.RetrieveDetailedContextAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DetailedRetrievalResult {
                    Chunks = new List<CodeChunkDto> {
                        new CodeChunkDto {
                            Id = Guid.NewGuid(),
                            ChunkType = "Class",
                            Content = "class OrderService {}",
                            Metadata = "{\"symbol_name\":\"OrderService\",\"file_path\":\"OrderService.cs\"}",
                            RetrievalExplanation = "Exact SymbolName match"
                        }
                    },
                    FinalChunks = 1,
                    CandidateChunks = 1,
                    FilteredChunks = 0,
                    AverageSimilarity = 0.8
                });

            var promptBuilderMock = new Mock<IPromptBuilder>();
            promptBuilderMock.Setup(p => p.BuildRagPrompt(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RepositoryContextDto>(), It.IsAny<List<ChatMessageDto>>()))
                .Returns(new List<ChatMessageDto>());

            var chatProviderMock = new Mock<IChatProvider>();
            chatProviderMock.Setup(c => c.ProviderName).Returns("Groq");

            ChatSettingsDto? capturedSettings = null;
            chatProviderMock.Setup(c => c.GetResponseAsync(It.IsAny<List<ChatMessageDto>>(), It.IsAny<ChatSettingsDto>(), It.IsAny<CancellationToken>()))
                .Callback<List<ChatMessageDto>, ChatSettingsDto, CancellationToken>((_, s, _) => capturedSettings = s)
                .ReturnsAsync(new ChatResponseDto { Content = "Response", TokenCount = 10 });

            var factoryMock = new Mock<IChatProviderFactory>();
            factoryMock.Setup(f => f.GetProvider("Groq")).Returns(chatProviderMock.Object);

            var inMemorySettings = new Dictionary<string, string> {
                {"ChatSettings:Provider", "Groq"},
                {"ChatSettings:Model", "llama-3.3-70b-versatile"},
                {"ChatSettings:Temperature", "0.2"},
                {"ChatSettings:MaxTokens", "2048"}
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();

            var graphServiceMock = new Mock<IRepositoryGraphService>();
            var contextExpanderMock = new Mock<IRepositoryContextExpander>();
            contextExpanderMock.Setup(e => e.ExpandContextAsync(It.IsAny<Guid>(), It.IsAny<List<CodeChunkDto>>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, List<CodeChunkDto> seeds, List<string> _, CancellationToken _) => new RepositoryContextDto
                {
                    SeedChunks = seeds,
                    ReferencedSymbols = new List<string> { "OrderService" },
                    RelatedFiles = new List<string> { "OrderService.cs" }
                });

            var chatService = new AiChatService(
                context,
                retrievalMock.Object,
                promptBuilderMock.Object,
                factoryMock.Object,
                _mapper,
                config,
                NullLogger<AiChatService>.Instance,
                contextExpanderMock.Object,
                graphServiceMock.Object
            );

            var settingsOverride = new ChatSettingsOverrideDto { Provider = "Groq" };

            // Act
            await chatService.SendMessageAsync(session.Id, "Question?", "ExplainCode", settingsOverride, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedSettings);
            Assert.Equal("Groq", capturedSettings.Provider);
            Assert.Equal("llama-3.3-70b-versatile", capturedSettings.Model);
            Assert.Equal(0.2, capturedSettings.Temperature);
            Assert.Equal(2048, capturedSettings.MaxTokens);
            Assert.Equal(0.9, capturedSettings.TopP); // Fallback to default
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }
}
