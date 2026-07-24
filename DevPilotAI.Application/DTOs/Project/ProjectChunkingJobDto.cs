using System;

namespace DevPilotAI.Application.DTOs.Project;

public class ProjectChunkingJobDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
}
