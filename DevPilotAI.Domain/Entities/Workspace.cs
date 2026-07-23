using DevPilotAI.Domain.Common;

namespace DevPilotAI.Domain.Entities;

public class Workspace : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
