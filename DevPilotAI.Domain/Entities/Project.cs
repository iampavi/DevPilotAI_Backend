using DevPilotAI.Domain.Common;
using DevPilotAI.Domain.Enums;

namespace DevPilotAI.Domain.Entities;

public class Project : AuditableSoftDeleteEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SourceLocation { get; set; }
    public ProjectType ProjectType { get; set; }
    
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;
    
    public ProjectSettings Settings { get; set; } = null!;
    public ProjectStatistics Statistics { get; set; } = null!;
    public ProjectIndex Index { get; set; } = null!;
    
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
