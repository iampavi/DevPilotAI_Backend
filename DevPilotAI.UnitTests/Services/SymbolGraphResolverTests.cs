using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Entities.Identity;
using DevPilotAI.Infrastructure.Persistence;
using DevPilotAI.Infrastructure.Services;
using DevPilotAI.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace DevPilotAI.UnitTests.Services;

public class SymbolGraphResolverTests
{
    private readonly ApplicationDbContext _context;
    private readonly SymbolGraphResolver _resolver;

    public SymbolGraphResolverTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=DevPilotAI_SymbolGraphResolverTests;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        _context = new ApplicationDbContext(options, new DateTimeProvider(), new Mock<ICurrentUserService>().Object);
        _context.Database.EnsureDeleted();
        _context.Database.EnsureCreated();

        _resolver = new SymbolGraphResolver(_context);
    }

    private async Task SeedBaseProjectAsync(Guid projectId)
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

        var project = new Project
        {
            Id = projectId,
            WorkspaceId = workspaceId,
            Name = "Test Project",
            SourceLocation = "Local"
        };
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetClassDependencies_ShouldResolveConstructorParametersAndBaseTypes()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        await SeedBaseProjectAsync(projectId);

        var fileId = Guid.NewGuid();
        var parsedFile = new ParsedFile
        {
            Id = fileId,
            ProjectId = projectId,
            RelativePath = "Services/AuthService.cs",
            Language = "CSharp",
            SizeInBytes = 200,
            Usings = new List<string>()
        };
        _context.ParsedFiles.Add(parsedFile);
        await _context.SaveChangesAsync();

        var classId = Guid.NewGuid();
        var parsedClass = new ParsedClass
        {
            Id = classId,
            ParsedFileId = fileId,
            Name = "AuthService",
            FullName = "DevPilotAI.Services.AuthService",
            Namespace = "DevPilotAI.Services",
            SymbolType = DevPilotAI.Domain.Enums.SymbolType.Class,
            BaseTypes = new List<string> { "IAuthService" },
            Attributes = new List<string>(),
            StartLine = 10,
            EndLine = 80
        };
        _context.ParsedClasses.Add(parsedClass);
        await _context.SaveChangesAsync();

        var ctor = new ParsedMethod
        {
            Id = Guid.NewGuid(),
            ParsedClassId = classId,
            Name = "AuthService",
            ReturnType = "Void",
            AccessModifier = "public",
            Parameters = new List<string> { "IUserRepository userRepository", "IJwtService jwtService" },
            Attributes = new List<string>(),
            StartLine = 15,
            EndLine = 25
        };
        _context.ParsedMethods.Add(ctor);
        await _context.SaveChangesAsync();

        // Act
        var dependencies = await _resolver.GetClassDependenciesAsync(projectId, "AuthService", CancellationToken.None);

        // Assert
        Assert.NotNull(dependencies);
        Assert.Contains("Inherits/Implements: IAuthService", dependencies);
        Assert.Contains("Constructor Injected: IUserRepository", dependencies);
        Assert.Contains("Constructor Injected: IJwtService", dependencies);
    }

    [Fact]
    public async Task GetClassDependents_ShouldResolveInjectedUsages()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        await SeedBaseProjectAsync(projectId);

        var fileId = Guid.NewGuid();
        var parsedFile = new ParsedFile
        {
            Id = fileId,
            ProjectId = projectId,
            RelativePath = "Controllers/AuthController.cs",
            Language = "CSharp",
            SizeInBytes = 150,
            Usings = new List<string>()
        };
        _context.ParsedFiles.Add(parsedFile);
        await _context.SaveChangesAsync();

        var classId = Guid.NewGuid();
        var parsedClass = new ParsedClass
        {
            Id = classId,
            ParsedFileId = fileId,
            Name = "AuthController",
            FullName = "DevPilotAI.Controllers.AuthController",
            Namespace = "DevPilotAI.Controllers",
            SymbolType = DevPilotAI.Domain.Enums.SymbolType.Class,
            BaseTypes = new List<string>(),
            Attributes = new List<string>(),
            StartLine = 5,
            EndLine = 50
        };
        _context.ParsedClasses.Add(parsedClass);
        await _context.SaveChangesAsync();

        var ctor = new ParsedMethod
        {
            Id = Guid.NewGuid(),
            ParsedClassId = classId,
            Name = "AuthController",
            ReturnType = "Void",
            AccessModifier = "public",
            Parameters = new List<string> { "IAuthService authService" },
            Attributes = new List<string>(),
            StartLine = 8,
            EndLine = 12
        };
        _context.ParsedMethods.Add(ctor);
        await _context.SaveChangesAsync();

        // Act
        var dependents = await _resolver.GetClassDependentsAsync(projectId, "AuthService", CancellationToken.None);

        // Assert
        Assert.NotNull(dependents);
        Assert.Contains("AuthController injects IAuthService via constructor", dependents);
    }
}
