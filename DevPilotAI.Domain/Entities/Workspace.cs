using DevPilotAI.Domain.Common;
using DevPilotAI.Domain.Entities.Identity;

namespace DevPilotAI.Domain.Entities;

public class Workspace : AuditableSoftDeleteEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
