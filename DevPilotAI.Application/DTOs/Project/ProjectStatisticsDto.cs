namespace DevPilotAI.Application.DTOs.Project;

public class ProjectStatisticsDto
{
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
