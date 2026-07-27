using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Entities.Identity;
using DevPilotAI.Domain.Enums;
using DevPilotAI.Infrastructure.Persistence;
using DevPilotAI.Infrastructure.Services;
using DevPilotAI.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace DevPilotAI.UnitTests.Services;

public class ProjectIndexSynchronizationTests
{
    private readonly ApplicationDbContext _context;
    private readonly ProjectIndexSynchronizationService _syncService;

    public ProjectIndexSynchronizationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=DevPilotAI_ProjectIndexSynchronizationTests;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        _context = new ApplicationDbContext(options, new DateTimeProvider(), new Mock<ICurrentUserService>().Object);
        _context.Database.EnsureDeleted();
        _context.Database.EnsureCreated();

        var inMemorySettings = new Dictionary<string, string> {
            {"EmbeddingSettings:Model", "text-embedding-3-small"}
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();

        _syncService = new ProjectIndexSynchronizationService(_context, config);
    }

    [Fact]
    public async Task SynchronizeProjectAsync_ShouldCalculateAllStatsAndMarkAsIndexed()
    {
        // Arrange
        var systemUserId = Guid.NewGuid();
        var systemUser = new ApplicationUser
        {
            Id = systemUserId,
            UserName = "system@devpilot.ai",
            Email = "system@devpilot.ai",
            FirstName = "System",
            LastName = "User"
        };
        _context.Users.Add(systemUser);
        await _context.SaveChangesAsync();

        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace 
        { 
            Id = workspaceId, 
            Name = "TestWorkspace",
            UserId = systemUserId
        };
        _context.Workspaces.Add(workspace);

        var projectId = Guid.NewGuid();
        var project = new Project
        {
            Id = projectId,
            WorkspaceId = workspaceId,
            Name = "Sync Test Project",
            SourceLocation = "Local"
        };
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        var fileId = Guid.NewGuid();
        var parsedFile = new ParsedFile
        {
            Id = fileId,
            ProjectId = projectId,
            RelativePath = "Controllers/TestController.cs",
            Language = "CSharp",
            SizeInBytes = 500,
            ParserVersion = 1,
            Usings = new List<string>()
        };
        _context.ParsedFiles.Add(parsedFile);
        await _context.SaveChangesAsync();

        var classId = Guid.NewGuid();
        var parsedClass = new ParsedClass
        {
            Id = classId,
            ParsedFileId = fileId,
            Name = "TestController",
            FullName = "DevPilotAI.Controllers.TestController",
            Namespace = "DevPilotAI.Controllers",
            SymbolType = SymbolType.Class,
            BaseTypes = new List<string> { "ControllerBase" },
            Attributes = new List<string>(),
            StartLine = 1,
            EndLine = 30
        };
        _context.ParsedClasses.Add(parsedClass);
        await _context.SaveChangesAsync();

        // 1 Constructor + 1 Public API Action Method
        var ctor = new ParsedMethod
        {
            Id = Guid.NewGuid(),
            ParsedClassId = classId,
            Name = "TestController",
            ReturnType = "Void",
            AccessModifier = "public",
            Parameters = new List<string>(),
            Attributes = new List<string>(),
            StartLine = 5,
            EndLine = 8
        };
        var action = new ParsedMethod
        {
            Id = Guid.NewGuid(),
            ParsedClassId = classId,
            Name = "GetEndpoints",
            ReturnType = "Task<IActionResult>",
            AccessModifier = "public",
            Parameters = new List<string>(),
            Attributes = new List<string>(),
            StartLine = 10,
            EndLine = 15
        };
        _context.ParsedMethods.Add(ctor);
        _context.ParsedMethods.Add(action);

        // Add 2 code chunks
        var chunk1 = new CodeChunk
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ParsedFileId = fileId,
            ParsedClassId = classId,
            ChunkType = "Class",
            Content = "public class TestController { }",
            Hash = "hash1",
            Metadata = "{}"
        };
        var chunk2 = new CodeChunk
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ParsedFileId = fileId,
            ParsedClassId = classId,
            ChunkType = "Method",
            Content = "public Task<IActionResult> GetEndpoints() { }",
            Hash = "hash2",
            Metadata = "{}"
        };
        _context.CodeChunks.Add(chunk1);
        _context.CodeChunks.Add(chunk2);
        await _context.SaveChangesAsync();

        // Act
        await _syncService.SynchronizeProjectAsync(projectId, CancellationToken.None);

        // Assert
        var updatedProject = await _context.Projects
            .Include(p => p.Statistics)
            .Include(p => p.Index)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        Assert.NotNull(updatedProject);
        Assert.NotNull(updatedProject.Statistics);
        Assert.NotNull(updatedProject.Index);

        // Statistics validations
        Assert.Equal(1, updatedProject.Statistics.FileCount);
        Assert.Equal(1, updatedProject.Statistics.ClassCount);
        Assert.Equal(1, updatedProject.Statistics.ControllerCount);
        Assert.Equal(0, updatedProject.Statistics.ServiceCount);
        Assert.Equal(0, updatedProject.Statistics.RepositoryCount);
        Assert.Equal(1, updatedProject.Statistics.ApiCount);
        Assert.Equal(1, updatedProject.Statistics.IndexedFileCount);
        Assert.Equal(500, updatedProject.Statistics.TotalBytes);

        // Index validations
        Assert.Equal(IndexStatus.Indexed, updatedProject.Index.IndexStatus);
        Assert.Equal(2, updatedProject.Index.ChunkCount);
        Assert.Equal(2, updatedProject.Index.EmbeddingCount);
        Assert.Equal("1.0", updatedProject.Index.ParserVersion);
        Assert.Equal("text-embedding-3-small", updatedProject.Index.EmbeddingModel);

        // Timing validation
        Assert.True((updatedProject.LastModifiedAt.Value - updatedProject.Index.LastIndexedAt.Value).Duration() < TimeSpan.FromSeconds(1));
    }
}
