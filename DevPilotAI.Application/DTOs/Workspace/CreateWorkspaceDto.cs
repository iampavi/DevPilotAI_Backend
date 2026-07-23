namespace DevPilotAI.Application.DTOs.Workspace;

public class CreateWorkspaceDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
