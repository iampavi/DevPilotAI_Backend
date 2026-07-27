using System;
using System.Collections.Generic;
using DevPilotAI.Application.DTOs.Chat;

namespace DevPilotAI.Application.DTOs.Copilot;

public class CopilotResponseDto
{
    public string Content { get; set; } = string.Empty;
    public List<RetrievalSourceDto> Sources { get; set; } = [];
    public int RetrievedChunks { get; set; }
    public int RelevantChunks { get; set; }
    public int IgnoredChunks { get; set; }
    public List<string> IgnoredReasons { get; set; } = [];
    public double ConfidenceScore { get; set; }
    public string ConfidenceRating { get; set; } = string.Empty;
    public RetrievalMetrics Metrics { get; set; } = new();
    public string PromptMode { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public TimeSpan Duration { get; set; }
}
