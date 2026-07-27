using System;

namespace DevPilotAI.Application.DTOs.Copilot;

public class RetrievalMetrics
{
    public int CandidateChunks { get; set; }
    public int FilteredChunks { get; set; }
    public int FinalChunks { get; set; }
    public double AverageSimilarity { get; set; }
    public TimeSpan RetrievalTime { get; set; }
    public TimeSpan LlmTime { get; set; }
}
