using System.Collections.Generic;

namespace DevPilotAI.Application.DTOs.Chat;

public class ChatDiagnosticsDto
{
    public int RetrievedChunksCount { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public long LatencyMilliseconds { get; set; }
    public List<double> SimilarityScores { get; set; } = new();
}
