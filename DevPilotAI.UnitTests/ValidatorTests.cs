using DevPilotAI.Application.DTOs.Project;
using DevPilotAI.Application.DTOs.Workspace;
using DevPilotAI.Application.Validators.Project;
using DevPilotAI.Application.Validators.Workspace;
using DevPilotAI.Domain.Enums;

namespace DevPilotAI.UnitTests;

public class ValidatorTests
{
    private readonly CreateWorkspaceDtoValidator _workspaceValidator = new();
    private readonly CreateProjectDtoValidator _projectValidator = new();
    private readonly UpdateProjectSettingsDtoValidator _settingsValidator = new();

    [Fact]
    public void WorkspaceValidator_ShouldPass_WhenDtoIsValid()
    {
        // Arrange
        var dto = new CreateWorkspaceDto
        {
            Name = "Valid Workspace",
            Description = "A valid workspace description."
        };

        // Act
        var result = _workspaceValidator.Validate(dto);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void WorkspaceValidator_ShouldFail_WhenNameIsEmpty()
    {
        // Arrange
        var dto = new CreateWorkspaceDto
        {
            Name = "",
            Description = "Desc"
        };

        // Act
        var result = _workspaceValidator.Validate(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(dto.Name));
    }

    [Fact]
    public void ProjectValidator_ShouldFail_WhenProjectTypeIsInvalid()
    {
        // Arrange
        var dto = new CreateProjectDto
        {
            Name = "Valid Name",
            ProjectType = (ProjectType)99 // Invalid enum value
        };

        // Act
        var result = _projectValidator.Validate(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(dto.ProjectType));
    }

    [Fact]
    public void SettingsValidator_ShouldFail_WhenMaxFileSizeIsZeroOrNegative()
    {
        // Arrange
        var dto = new UpdateProjectSettingsDto
        {
            MaxFileSizeInBytes = 0,
            ExcludedFolders = new List<string>(),
            ExcludedExtensions = new List<string>()
        };

        // Act
        var result = _settingsValidator.Validate(dto);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(dto.MaxFileSizeInBytes));
    }
}
