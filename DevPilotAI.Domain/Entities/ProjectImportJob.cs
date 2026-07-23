using DevPilotAI.Domain.Common;
using DevPilotAI.Domain.Enums;

namespace DevPilotAI.Domain.Entities;

public class ProjectImportJob : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public ImportType ImportType { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public int Progress { get; set; } // 0 to 100

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
}
