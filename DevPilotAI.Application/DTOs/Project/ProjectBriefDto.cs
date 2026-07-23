namespace DevPilotAI.Application.DTOs.Project;

public class ProjectBriefDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ProjectType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string IndexStatus { get; set; } = string.Empty;
}
