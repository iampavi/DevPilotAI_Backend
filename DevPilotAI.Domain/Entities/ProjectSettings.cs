using DevPilotAI.Domain.Common;

namespace DevPilotAI.Domain.Entities;

public class ProjectSettings : AuditableSoftDeleteEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    
    public List<string> ExcludedFolders { get; set; } = new();
    public List<string> ExcludedExtensions { get; set; } = new();
    public long MaxFileSizeInBytes { get; set; } = 5242880; // Default 5MB
}
