using DevPilotAI.Domain.Common;

namespace DevPilotAI.Domain.Entities;

public class ProjectStatistics : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    
    public int FileCount { get; set; }
    public long TotalLinesOfCode { get; set; }
    public long TotalBytes { get; set; }
    public int IndexedFileCount { get; set; }
    
    public int ControllerCount { get; set; }
    public int ServiceCount { get; set; }
    public int RepositoryCount { get; set; }
    public int ApiCount { get; set; }
    public int ClassCount { get; set; }
}
