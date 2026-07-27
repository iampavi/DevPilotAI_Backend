using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using DevPilotAI.Infrastructure.Persistence;
using DevPilotAI.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DevPilotAI.Domain.Entities.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevPilotAI.UnitTests.Services;

public class ProjectChunkingTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IEmbeddingService> _embeddingMock;
    private readonly Mock<IQdrantService> _qdrantMock;
    private readonly Mock<IProjectChunkingQueue> _queueMock;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly ProjectChunkingBackgroundWorker _worker;

    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _jobId = Guid.NewGuid();

    public ProjectChunkingTests()
    {
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(u => u.UserId).Returns("System");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=DevPilotAI_ProjectChunkingTests;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        _context = new ApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
        _context.Database.EnsureDeleted();
        _context.Database.EnsureCreated();

        // Seed system user to satisfy foreign key constraints
        var systemUser = new ApplicationUser
        {
            Id = Guid.Parse("D035B9FE-B7FE-438B-B0D1-1C349C3AF21F"),
            UserName = "system@devpilot.ai",
            Email = "system@devpilot.ai",
            FirstName = "System",
            LastName = "User"
        };
        _context.Users.Add(systemUser);
        _context.SaveChanges();

        _embeddingMock = new Mock<IEmbeddingService>();
        _embeddingMock.Setup(e => e.Dimensions).Returns(1536);
        _embeddingMock.Setup(e => e.ConfiguredModel).Returns("text-embedding-3-small");

        _qdrantMock = new Mock<IQdrantService>();
        _queueMock = new Mock<IProjectChunkingQueue>();

        // Build ServiceProvider for worker scope
        var services = new ServiceCollection();
        services.AddSingleton<IApplicationDbContext>(_context);
        services.AddSingleton(_embeddingMock.Object);
        services.AddSingleton(_qdrantMock.Object);
        services.AddSingleton<IChunkingScheduler>(new Mock<IChunkingScheduler>().Object);
        services.AddSingleton(new Mock<IProjectIndexSynchronizationService>().Object);

        var serviceProvider = services.BuildServiceProvider();

        var inMemorySettings = new Dictionary<string, string> {
            {"EmbeddingSettings:BatchSize", "2"}
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _worker = new ProjectChunkingBackgroundWorker(
            _queueMock.Object,
            serviceProvider,
            configuration,
            NullLogger<ProjectChunkingBackgroundWorker>.Instance);
    }

    [Fact]
    public async Task ProcessJobAsync_ShouldChunkAndEmbedCorrectly_WithHashIncrementalUpdates()
    {
        // Arrange
        // Seed default Project, ParseJob, and ParsedFiles
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace
        {
            Id = workspaceId,
            Name = "Test Workspace",
            UserId = Guid.Parse("D035B9FE-B7FE-438B-B0D1-1C349C3AF21F")
        };
        _context.Workspaces.Add(workspace);

        var project = new Project
        {
            Id = _projectId,
            Name = "Test Chunking Project",
            SourceLocation = AppDomain.CurrentDomain.BaseDirectory,
            WorkspaceId = workspaceId
        };
        _context.Projects.Add(project);

        var job = new ProjectChunkingJob
        {
            Id = _jobId,
            ProjectId = _projectId,
            Status = JobStatus.Pending,
            Progress = 0
        };
        _context.ProjectChunkingJobs.Add(job);

        // Write a test C# file on disk to simulate parsing source code
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestOrderService.cs");
        var csharpContent = @"
namespace Test;
public class TestOrderService
{
    private readonly int _id;
    public string Name { get; set; }
    public void Run() { }
}";
        await File.WriteAllTextAsync(filePath, csharpContent);

        var parsedFile = new ParsedFile
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            RelativePath = "TestOrderService.cs",
            Language = "C#",
            SizeInBytes = csharpContent.Length
        };

        var parsedClass = new ParsedClass
        {
            Id = Guid.NewGuid(),
            Name = "TestOrderService",
            FullName = "Test.TestOrderService",
            Namespace = "Test",
            SymbolType = SymbolType.Class,
            StartLine = 3,
            EndLine = 9
        };

        parsedClass.Fields.Add(new ParsedField { Name = "_id", Type = "int", AccessModifier = "private" });
        parsedClass.Properties.Add(new ParsedProperty { Name = "Name", Type = "string", AccessModifier = "public" });
        parsedClass.Methods.Add(new ParsedMethod { Name = "Run", ReturnType = "void", AccessModifier = "public", StartLine = 7, EndLine = 7 });

        parsedFile.Classes.Add(parsedClass);
        _context.ParsedFiles.Add(parsedFile);
        await _context.SaveChangesAsync();

        // Mock embedding generations
        _embeddingMock.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken _) =>
            {
                return texts.Select(_ => new float[1536]).ToList();
            });

        var jobItem = new ChunkingJobItem(_jobId, _projectId);

        // Act
        // Invoke internal private method using Reflection to test
        var methodInfo = typeof(ProjectChunkingBackgroundWorker)
            .GetMethod("ProcessJobAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        await (Task)methodInfo.Invoke(_worker, new object[] { jobItem, CancellationToken.None });

        // Assert
        // Verify job state in database
        var dbJob = await _context.ProjectChunkingJobs.FindAsync(_jobId);
        Assert.NotNull(dbJob);
        Assert.Equal(JobStatus.Completed, dbJob.Status);
        Assert.Equal(100, dbJob.Progress);

        // Verify chunks were created
        var chunks = await _context.CodeChunks.Where(c => c.ProjectId == _projectId).ToListAsync();
        Assert.Equal(3, chunks.Count); // Class, Method, Property chunks

        var classChunk = chunks.First(c => c.ChunkType == "Class");
        Assert.Equal(parsedClass.Id, classChunk.ParsedClassId);
        Assert.Contains("class TestOrderService", classChunk.Content);

        // Verify Qdrant upserts were called
        _qdrantMock.Verify(q => q.UpsertVectorsAsync(
            "devpilot-project-chunks",
            It.Is<List<QdrantPointDto>>(pts => pts.Count > 0),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        // Clean up
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
