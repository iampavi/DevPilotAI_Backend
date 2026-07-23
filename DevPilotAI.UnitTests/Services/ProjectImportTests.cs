using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Common.Mappings;
using DevPilotAI.Application.DTOs.Project;
using DevPilotAI.Application.Services;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Entities.Identity;
using DevPilotAI.Domain.Enums;
using DevPilotAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevPilotAI.UnitTests.Services;

public class ProjectImportTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ProjectService _service;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<IFileStorageService> _fileStorageServiceMock;
    private readonly Mock<IProjectImportQueue> _projectImportQueueMock;
    private readonly Guid _workspaceId = Guid.Parse("D035B9FE-B7FE-438B-B0D1-1C349C3AF21E");
    private readonly Guid _systemUserId = Guid.Parse("D035B9FE-B7FE-438B-B0D1-1C349C3AF21F");

    public ProjectImportTests()
    {
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(u => u.UserId).Returns(_systemUserId.ToString());

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=DevPilotAI_ProjectImportTests;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        _context = new ApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
        _context.Database.EnsureDeleted();
        _context.Database.EnsureCreated();

        // Seed System User and Default Workspace
        var systemUser = new ApplicationUser
        {
            Id = _systemUserId,
            UserName = "system@devpilot.ai",
            Email = "system@devpilot.ai",
            FirstName = "System",
            LastName = "User"
        };
        _context.Users.Add(systemUser);

        var workspace = new Workspace
        {
            Id = _workspaceId,
            Name = "Default Workspace",
            UserId = _systemUserId
        };
        _context.Workspaces.Add(workspace);
        _context.SaveChanges();

        _fileStorageServiceMock = new Mock<IFileStorageService>();
        _projectImportQueueMock = new Mock<IProjectImportQueue>();

        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
        }, NullLoggerFactory.Instance);
        _mapper = configuration.CreateMapper();

        _service = new ProjectService(
            _context,
            _mapper,
            _fileStorageServiceMock.Object,
            _projectImportQueueMock.Object,
            NullLogger<ProjectService>.Instance);
    }

    [Fact]
    public async Task RegisterLocalProjectAsync_ShouldSaveProject_WhenFolderExists()
    {
        // Arrange
        // Use current executing directory as a valid path
        var validPath = AppDomain.CurrentDomain.BaseDirectory;
        var dto = new RegisterLocalDto { Name = "Local Project", SourceLocation = validPath };

        // Act
        var result = await _service.RegisterLocalProjectAsync(_workspaceId, dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectType.LocalFolder.ToString(), result.Value.ProjectType);
        
        var dbProject = await _context.Projects.FindAsync(result.Value.Id);
        Assert.NotNull(dbProject);
        Assert.Equal(validPath, dbProject.SourceLocation);

        var dbJob = await _context.ProjectImportJobs.FirstOrDefaultAsync(j => j.ProjectId == dbProject.Id);
        Assert.NotNull(dbJob);
        Assert.Equal(JobStatus.Completed, dbJob.Status);
        Assert.Equal(100, dbJob.Progress);
    }

    [Fact]
    public async Task RegisterLocalProjectAsync_ShouldFail_WhenFolderDoesNotExist()
    {
        // Arrange
        var invalidPath = @"C:\NonExistentFolderForDevPilotAIIntegrationTests";
        var dto = new RegisterLocalDto { Name = "Local Project", SourceLocation = invalidPath };

        // Act
        var result = await _service.RegisterLocalProjectAsync(_workspaceId, dto);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Project.InvalidLocation", result.Error.Code);
    }

    [Fact]
    public async Task ImportZipProjectAsync_ShouldQueueJob_WhenFileIsZip()
    {
        // Arrange
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        
        // Act
        var result = await _service.ImportZipProjectAsync(_workspaceId, "Zip Project", stream, "project.zip");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("ZipUpload", result.Value.ImportType);
        Assert.Equal("Pending", result.Value.Status);

        _fileStorageServiceMock.Verify(s => s.SaveFileAsync("Uploads", It.Is<string>(f => f.EndsWith(".zip")), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        _projectImportQueueMock.Verify(q => q.QueueImportJobAsync(It.Is<ImportJobItem>(j => j.ImportType == ImportType.ZipUpload)), Times.Once);
    }

    [Fact]
    public async Task ImportZipProjectAsync_ShouldFail_WhenFileIsNotZip()
    {
        // Arrange
        var stream = new MemoryStream();

        // Act
        var result = await _service.ImportZipProjectAsync(_workspaceId, "Bad Project", stream, "project.txt");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Project.InvalidExtension", result.Error.Code);
    }

    [Fact]
    public async Task ImportGitProjectAsync_ShouldMapProviderTypeCorrectly_BasedOnUrl()
    {
        // Arrange
        var githubDto = new ImportGitDto { Name = "Github Project", RepositoryUrl = "https://github.com/user/repo.git", Branch = "master" };
        var gitlabDto = new ImportGitDto { Name = "Gitlab Project", RepositoryUrl = "https://gitlab.com/user/repo.git", Branch = "main" };

        // Act
        var githubResult = await _service.ImportGitProjectAsync(_workspaceId, githubDto);
        var gitlabResult = await _service.ImportGitProjectAsync(_workspaceId, gitlabDto);

        // Assert
        Assert.True(githubResult.IsSuccess);
        Assert.True(gitlabResult.IsSuccess);

        var githubProj = await _context.Projects.FindAsync(githubResult.Value.ProjectId);
        var gitlabProj = await _context.Projects.FindAsync(gitlabResult.Value.ProjectId);

        Assert.Equal(ProjectType.GitHub, githubProj!.ProjectType);
        Assert.Equal(ProjectType.GitLab, gitlabProj!.ProjectType);

        _projectImportQueueMock.Verify(q => q.QueueImportJobAsync(It.Is<ImportJobItem>(j => j.ImportType == ImportType.GitRepository)), Times.Exactly(2));
    }

    [Fact]
    public void ZipSlipDetection_ShouldFlagTraversalPaths()
    {
        // Arrange
        var targetExtractionDir = Path.GetFullPath(@"C:\DevPilotAI\Storage\Extracted\Project1");
        
        var safeEntryName = @"src/Program.cs";
        var safePath = Path.GetFullPath(Path.Combine(targetExtractionDir, safeEntryName));
        
        var maliciousEntryName = @"../../outside.txt";
        var maliciousPath = Path.GetFullPath(Path.Combine(targetExtractionDir, maliciousEntryName));

        // Act
        var isSafeEntryLegitimate = safePath.StartsWith(targetExtractionDir, StringComparison.OrdinalIgnoreCase);
        var isMaliciousEntryLegitimate = maliciousPath.StartsWith(targetExtractionDir, StringComparison.OrdinalIgnoreCase);

        // Assert
        Assert.True(isSafeEntryLegitimate);
        Assert.False(isMaliciousEntryLegitimate);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
