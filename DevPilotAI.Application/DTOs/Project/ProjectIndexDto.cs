namespace DevPilotAI.Application.DTOs.Project;

public class ProjectIndexDto
{
    public string IndexVersion { get; set; } = string.Empty;
    public string IndexStatus { get; set; } = string.Empty;
    public DateTime? LastIndexedAt { get; set; }
    public string? EmbeddingModel { get; set; }
    public int ChunkCount { get; set; }
    public int EmbeddingCount { get; set; }
    public string? ParserVersion { get; set; }
}
