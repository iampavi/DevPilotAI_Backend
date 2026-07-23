using AutoMapper;
using DevPilotAI.Application.Common.Mappings;
using DevPilotAI.Application.DTOs.Project;
using DevPilotAI.Application.DTOs.Workspace;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevPilotAI.UnitTests;

public class MappingTests
{
    private readonly IMapper _mapper;

    public MappingTests()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
        }, NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();
    }

    [Fact]
    public void Configuration_ShouldBeValid()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
        }, NullLoggerFactory.Instance);
        
        // Assert
        config.AssertConfigurationIsValid();
    }

    [Fact]
    public void Map_WorkspaceToWorkspaceDto_ShouldMapProperties()
    {
        // Arrange
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "BeatBox",
            Description = "Music player project",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var dto = _mapper.Map<WorkspaceDto>(workspace);

        // Assert
        Assert.Equal(workspace.Id, dto.Id);
        Assert.Equal(workspace.Name, dto.Name);
        Assert.Equal(workspace.Description, dto.Description);
        Assert.Equal(workspace.CreatedAt, dto.CreatedAt);
    }

    [Fact]
    public void Map_ProjectToProjectDto_ShouldConvertEnumsToStrings()
    {
        // Arrange
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "BeatBox.Api",
            Description = "ASP.NET core API",
            SourceLocation = "C:\\Projects\\BeatBox",
            ProjectType = ProjectType.GitHub,
            WorkspaceId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Settings = new ProjectSettings
            {
                ExcludedFolders = new List<string> { "bin", "obj" },
                ExcludedExtensions = new List<string> { ".pdb" },
                MaxFileSizeInBytes = 10000
            },
            Statistics = new ProjectStatistics
            {
                FileCount = 10,
                TotalLinesOfCode = 1200
            },
            Index = new ProjectIndex
            {
                IndexVersion = "v1.0",
                IndexStatus = IndexStatus.Indexing
            }
        };

        // Act
        var dto = _mapper.Map<ProjectDto>(project);

        // Assert
        Assert.Equal(project.Id, dto.Id);
        Assert.Equal("GitHub", dto.ProjectType);
        Assert.Equal("Indexing", dto.Index.IndexStatus);
        Assert.Equal(10, dto.Statistics.FileCount);
        Assert.Contains("bin", dto.Settings.ExcludedFolders);
    }
}
