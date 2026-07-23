namespace DevPilotAI.Application.DTOs.Project;

public class UpdateProjectDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SourceLocation { get; set; }
}
