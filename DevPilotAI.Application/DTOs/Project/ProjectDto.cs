namespace DevPilotAI.Application.DTOs.Project;

public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SourceLocation { get; set; }
    public string ProjectType { get; set; } = string.Empty;
    public Guid WorkspaceId { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    
    public ProjectSettingsDto Settings { get; set; } = null!;
    public ProjectStatisticsDto Statistics { get; set; } = null!;
    public ProjectIndexDto Index { get; set; } = null!;
}
