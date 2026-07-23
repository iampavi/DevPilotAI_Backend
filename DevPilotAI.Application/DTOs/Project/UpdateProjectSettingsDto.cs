namespace DevPilotAI.Application.DTOs.Project;

public class UpdateProjectSettingsDto
{
    public List<string> ExcludedFolders { get; set; } = new();
    public List<string> ExcludedExtensions { get; set; } = new();
    public long MaxFileSizeInBytes { get; set; }
}
