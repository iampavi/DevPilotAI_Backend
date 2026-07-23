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

public class ProjectServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ProjectService _service;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly DbContextOptions<ApplicationDbContext> _dbOptions;

    public ProjectServiceTests()
    {
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(u => u.UserId).Returns("TestUser");

        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=DevPilotAI_ProjectServiceTests;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        _context = new ApplicationDbContext(_dbOptions, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
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

        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
        }, NullLoggerFactory.Instance);
        _mapper = configuration.CreateMapper();

        var logger = NullLogger<ProjectService>.Instance;
        _service = new ProjectService(_context, _mapper, logger);
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldInstantiateDefaults_WhenRequestIsValid()
    {
        // Arrange
        var workspace = new Workspace
        {
            Name = "Workspace to Add Project",
            UserId = Guid.Parse("D035B9FE-B7FE-438B-B0D1-1C349C3AF21F")
        };
        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync();

        var dto = new CreateProjectDto
        {
            Name = "New Project",
            Description = "Testing defaults creation",
            SourceLocation = "C:\\local\\folder",
            ProjectType = ProjectType.LocalFolder
        };

        // Act
        var result = await _service.CreateProjectAsync(workspace.Id, dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal("New Project", result.Value.Name);

        // Assert cascading configuration mappings loaded correctly
        var dbProject = await _context.Projects
            .Include(p => p.Settings)
            .Include(p => p.Statistics)
            .Include(p => p.Index)
            .FirstOrDefaultAsync(p => p.Id == result.Value.Id);

        Assert.NotNull(dbProject);
        Assert.NotNull(dbProject.Settings);
        Assert.Contains("bin", dbProject.Settings.ExcludedFolders);
        Assert.Contains(".pdb", dbProject.Settings.ExcludedExtensions);
        Assert.Equal(5242880, dbProject.Settings.MaxFileSizeInBytes);

        Assert.NotNull(dbProject.Statistics);
        Assert.Equal(0, dbProject.Statistics.FileCount);

        Assert.NotNull(dbProject.Index);
        Assert.Equal("v1.0", dbProject.Index.IndexVersion);
        Assert.Equal(IndexStatus.Unindexed, dbProject.Index.IndexStatus);
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldFail_WhenWorkspaceNotFound()
    {
        // Arrange
        var dto = new CreateProjectDto
        {
            Name = "Project with Invalid Workspace",
            ProjectType = ProjectType.LocalFolder
        };

        // Act
        var result = await _service.CreateProjectAsync(Guid.NewGuid(), dto);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Workspace.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldFail_WhenNameIsDuplicateInWorkspace()
    {
        // Arrange
        var workspace = new Workspace
        {
            Name = "Workspace",
            UserId = Guid.Parse("D035B9FE-B7FE-438B-B0D1-1C349C3AF21F")
        };
        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync();

        var project = new Project { Name = "UniqueProject", WorkspaceId = workspace.Id, ProjectType = ProjectType.LocalFolder };
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        var dto = new CreateProjectDto { Name = "UniqueProject", ProjectType = ProjectType.LocalFolder };

        // Act
        var result = await _service.CreateProjectAsync(workspace.Id, dto);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Project.DuplicateName", result.Error.Code);
    }

    [Fact]
    public async Task UpdateProjectAsync_ShouldFail_WhenConcurrencyExceptionOccurs()
    {
        // Arrange
        var workspace = new Workspace
        {
            Name = "Workspace",
            UserId = Guid.Parse("D035B9FE-B7FE-438B-B0D1-1C349C3AF21F")
        };
        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync();

        var project = new Project { Name = "ConcurrencyProject", WorkspaceId = workspace.Id, ProjectType = ProjectType.LocalFolder };
        project.Settings = new ProjectSettings();
        project.Statistics = new ProjectStatistics();
        project.Index = new ProjectIndex();
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        // Instantiate another DbContext context to update the entity concurrently in database
        using var secondaryContext = new ApplicationDbContext(_dbOptions, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
        var concurrentProject = await secondaryContext.Projects.FindAsync(project.Id);
        concurrentProject!.Name = "Concurrently Modified Name";
        await secondaryContext.SaveChangesAsync();

        var dto = new UpdateProjectDto { Name = "Stale Edit Name" };

        // Act
        var result = await _service.UpdateProjectAsync(project.Id, dto);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Concurrency.Conflict", result.Error.Code);
    }

    [Fact]
    public async Task UpdateProjectSettingsAsync_ShouldUpdateSettingsCorrectly()
    {
        // Arrange
        var workspace = new Workspace
        {
            Name = "Workspace",
            UserId = Guid.Parse("D035B9FE-B7FE-438B-B0D1-1C349C3AF21F")
        };
        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync();

        var project = new Project { Name = "Project Settings Test", WorkspaceId = workspace.Id, ProjectType = ProjectType.LocalFolder };
        project.Settings = new ProjectSettings
        {
            ExcludedFolders = new List<string> { "old" },
            ExcludedExtensions = new List<string> { ".old" }
        };
        project.Statistics = new ProjectStatistics();
        project.Index = new ProjectIndex();
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        var updateDto = new UpdateProjectSettingsDto
        {
            ExcludedFolders = new List<string> { "new", "test" },
            ExcludedExtensions = new List<string> { ".new", ".json" },
            MaxFileSizeInBytes = 1000
        };

        // Act
        var result = await _service.UpdateProjectSettingsAsync(project.Id, updateDto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1000, result.Value.MaxFileSizeInBytes);
        Assert.Contains("new", result.Value.ExcludedFolders);
        Assert.Contains(".json", result.Value.ExcludedExtensions);

        var dbSettings = await _context.ProjectSettings.FindAsync(project.Id);
        Assert.Equal(1000, dbSettings!.MaxFileSizeInBytes);
        Assert.Contains("new", dbSettings.ExcludedFolders);
    }

    [Fact]
    public async Task DeleteProjectAsync_ShouldSoftDeleteCascade()
    {
        // Arrange
        var workspace = new Workspace
        {
            Name = "Workspace",
            UserId = Guid.Parse("D035B9FE-B7FE-438B-B0D1-1C349C3AF21F")
        };
        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync();

        var project = new Project { Name = "Project to Delete", WorkspaceId = workspace.Id, ProjectType = ProjectType.LocalFolder };
        project.Settings = new ProjectSettings();
        project.Statistics = new ProjectStatistics();
        project.Index = new ProjectIndex();
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.DeleteProjectAsync(project.Id);

        // Assert
        Assert.True(result.IsSuccess);

        // Filtered out from queries
        var dbProjects = await _context.Projects.ToListAsync();
        Assert.Empty(dbProjects);

        // Soft deleted properties are populated
        var softDeletedProject = await _context.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == project.Id);
        Assert.True(softDeletedProject!.IsDeleted);
        Assert.NotNull(softDeletedProject.DeletedAt);

        var softDeletedSettings = await _context.ProjectSettings.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.ProjectId == project.Id);
        Assert.True(softDeletedSettings!.IsDeleted);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
