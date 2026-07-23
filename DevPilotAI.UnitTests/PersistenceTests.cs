using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DevPilotAI.UnitTests;

public class PersistenceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly DateTime _utcNow = new(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);
    private readonly string _testUser = "IntegrationTestUser";

    public PersistenceTests()
    {
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(_utcNow);

        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(u => u.UserId).Returns(_testUser);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=DevPilotAI_IntegrationTests;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        _context = new ApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);

        // Ensure database is clean
        _context.Database.EnsureDeleted();
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldSetAuditFields_WhenAddingEntity()
    {
        // Arrange
        var workspace = new Workspace
        {
            Name = "Test Auditing Workspace",
            Description = "Testing audit field generation"
        };

        // Act
        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync();

        // Assert
        Assert.NotEqual(Guid.Empty, workspace.Id);
        Assert.Equal(_utcNow, workspace.CreatedAt);
        Assert.Equal(_testUser, workspace.CreatedBy);
        Assert.Null(workspace.LastModifiedAt);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldConvertDeleteToSoftDelete_WhenDeletingEntity()
    {
        // Arrange
        var workspace = new Workspace
        {
            Name = "Workspace to Soft Delete"
        };
        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync();

        // Act
        _context.Workspaces.Remove(workspace);
        await _context.SaveChangesAsync();

        // Assert
        // State should be modified and marked as deleted
        var entry = _context.Entry(workspace);
        Assert.Equal(EntityState.Unchanged, entry.State); // Refreshed from DB state after Save
        Assert.True(workspace.IsDeleted);
        Assert.Equal(_utcNow, workspace.DeletedAt);

        // Querying the DB context directly shouldn't return it due to Global Query Filters
        var queryResults = await _context.Workspaces.ToListAsync();
        Assert.DoesNotContain(queryResults, w => w.Id == workspace.Id);

        // Bypassing global filter should reveal it
        var bypassedResults = await _context.Workspaces.IgnoreQueryFilters().ToListAsync();
        Assert.Contains(bypassedResults, w => w.Id == workspace.Id);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldUpdateLastModifiedFields_WhenModifyingEntity()
    {
        // Arrange
        var workspace = new Workspace
        {
            Name = "Workspace to Update"
        };
        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync();

        var updatedTime = _utcNow.AddHours(2);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(updatedTime);

        // Act
        workspace.Description = "Updated Description";
        await _context.SaveChangesAsync();

        // Assert
        Assert.Equal(updatedTime, workspace.LastModifiedAt);
        Assert.Equal(_testUser, workspace.LastModifiedBy);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
