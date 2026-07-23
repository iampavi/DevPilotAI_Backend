using DevPilotAI.Domain.Enums;

namespace DevPilotAI.Application.DTOs.Project;

public class CreateProjectDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SourceLocation { get; set; }
    public ProjectType ProjectType { get; set; }
}
