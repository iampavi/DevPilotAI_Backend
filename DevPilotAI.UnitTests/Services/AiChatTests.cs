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
            {"RagSettings:MaxPromptTokens", "50"},
            {"RagSettings:PromptTemplates:ExplainCode", "Explain context: {context}\nQuery: {question}"}
        };
        var tightConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(tightInMemorySettings!)
            .Build();
        var tightPromptBuilder = new PromptBuilder(tightConfig);

        var result = tightPromptBuilder.BuildRagPrompt("ExplainCode", userQuestion, contextChunks, history);

        Assert.Equal(3, result.Count);
        Assert.Equal("system", result[0].Role);
        Assert.Equal("assistant", result[1].Role);
        Assert.Equal("Message 6", result[1].Content);
        Assert.Equal("user", result[2].Role);
        Assert.Equal(userQuestion, result[2].Content);
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

            var retrievalService = new SemanticRetrievalService(qdrantMock.Object, embeddingMock.Object, context, config, NullLogger<SemanticRetrievalService>.Instance);

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
            retrievalMock.Setup(r => r.RetrieveRelevantContextAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CodeChunkDto>());

            var promptBuilderMock = new Mock<IPromptBuilder>();
            promptBuilderMock.Setup(p => p.BuildRagPrompt(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<CodeChunkDto>>(), It.IsAny<List<ChatMessageDto>>()))
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

            var chatService = new AiChatService(
                context,
                retrievalMock.Object,
                promptBuilderMock.Object,
                factoryMock.Object,
                _mapper,
                config,
                NullLogger<AiChatService>.Instance
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
}
