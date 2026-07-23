using AutoMapper;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Common.Mappings;
using DevPilotAI.Application.DTOs.Workspace;
using DevPilotAI.Application.Services;
using DevPilotAI.Shared.Common;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevPilotAI.UnitTests.Services;

public class WorkspaceServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly WorkspaceService _service;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;

    public WorkspaceServiceTests()
    {
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(u => u.UserId).Returns("TestUser");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=DevPilotAI_WorkspaceServiceTests;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        _context = new ApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
        _context.Database.EnsureDeleted();
        _context.Database.EnsureCreated();

        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
        }, NullLoggerFactory.Instance);
        _mapper = configuration.CreateMapper();

        var logger = NullLogger<WorkspaceService>.Instance;
        _service = new WorkspaceService(_context, _mapper, logger);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_ShouldSaveToDb_WhenRequestIsValid()
    {
        // Arrange
        var dto = new CreateWorkspaceDto { Name = "New Workspace", Description = "Testing creation" };

        // Act
        var result = await _service.CreateWorkspaceAsync(dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal("New Workspace", result.Value.Name);

        var dbWorkspace = await _context.Workspaces.FindAsync(result.Value.Id);
        Assert.NotNull(dbWorkspace);
        Assert.Equal("New Workspace", dbWorkspace.Name);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_ShouldFail_WhenNameIsDuplicate()
    {
        // Arrange
        var workspace = new Workspace { Name = "DuplicateName" };
        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync();

        var dto = new CreateWorkspaceDto { Name = "DuplicateName" };

        // Act
        var result = await _service.CreateWorkspaceAsync(dto);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Workspace.DuplicateName", result.Error.Code);
    }

    [Fact]
    public async Task GetWorkspaceByIdAsync_ShouldReturnSuccess_WhenWorkspaceExists()
    {
        // Arrange
        var workspace = new Workspace { Name = "Found Workspace" };
        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetWorkspaceByIdAsync(workspace.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Found Workspace", result.Value.Name);
    }

    [Fact]
    public async Task GetWorkspacesAsync_ShouldSupportSearchingAndPagination()
    {
        // Arrange
        _context.Workspaces.AddRange(new[]
        {
            new Workspace { Name = "Apple Workspace" },
            new Workspace { Name = "Banana Workspace" },
            new Workspace { Name = "Cherry Workspace" }
        });
        await _context.SaveChangesAsync();

        // Act & Assert 1: Search term match
        var searchResult = await _service.GetWorkspacesAsync(
            searchTerm: "Cherry",
            sortBy: "name",
            sortOrder: "asc",
            pagination: new PaginationRequest { PageNumber = 1, PageSize = 10 });

        Assert.True(searchResult.IsSuccess);
        Assert.Single(searchResult.Value.Items);
        Assert.Equal("Cherry Workspace", searchResult.Value.Items[0].Name);

        // Act & Assert 2: Pagination check
        var paginationResult = await _service.GetWorkspacesAsync(
            searchTerm: null,
            sortBy: "name",
            sortOrder: "asc",
            pagination: new PaginationRequest { PageNumber = 1, PageSize = 2 });

        Assert.True(paginationResult.IsSuccess);
        Assert.Equal(2, paginationResult.Value.Items.Count);
        Assert.Equal("Apple Workspace", paginationResult.Value.Items[0].Name);
        Assert.Equal("Banana Workspace", paginationResult.Value.Items[1].Name);
        Assert.Equal(3, paginationResult.Value.TotalCount);
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_ShouldUpdateDb_WhenRequestIsValid()
    {
        // Arrange
        var workspace = new Workspace { Name = "Original Name" };
        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync();

        var dto = new UpdateWorkspaceDto { Name = "Updated Name", Description = "Updated Desc" };

        // Act
        var result = await _service.UpdateWorkspaceAsync(workspace.Id, dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Name", result.Value.Name);

        var dbWorkspace = await _context.Workspaces.FindAsync(workspace.Id);
        Assert.Equal("Updated Name", dbWorkspace!.Name);
        Assert.Equal("Updated Desc", dbWorkspace.Description);
    }

    [Fact]
    public async Task DeleteWorkspaceAsync_ShouldSoftDeleteWorkspaceAndProjects()
    {
        // Arrange
        var workspace = new Workspace
        {
            Name = "Workspace to Delete",
            Projects = new List<Project>
            {
                new Project { Name = "Child Project 1" },
                new Project { Name = "Child Project 2" }
            }
        };
        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.DeleteWorkspaceAsync(workspace.Id);

        // Assert
        Assert.True(result.IsSuccess);

        // Querying direct DB context should filter them out
        var dbWorkspaces = await _context.Workspaces.ToListAsync();
        Assert.DoesNotContain(dbWorkspaces, w => w.Id == workspace.Id);

        var dbProjects = await _context.Projects.ToListAsync();
        Assert.Empty(dbProjects);

        // Bypassing filter should reveal they are soft deleted
        var softDeletedWorkspace = await _context.Workspaces.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Id == workspace.Id);
        Assert.True(softDeletedWorkspace!.IsDeleted);
        Assert.NotNull(softDeletedWorkspace.DeletedAt);

        var softDeletedProjects = await _context.Projects.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(2, softDeletedProjects.Count);
        Assert.All(softDeletedProjects, p => Assert.True(p.IsDeleted));
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
