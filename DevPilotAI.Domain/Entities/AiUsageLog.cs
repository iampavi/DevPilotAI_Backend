using System;
using DevPilotAI.Domain.Common;

namespace DevPilotAI.Domain.Entities;

public class AiUsageLog : BaseEntity
{
    public string Provider { get; set; } = null!;
    public string Model { get; set; } = null!;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal? Cost { get; set; }
    public long LatencyMilliseconds { get; set; }
    public DateTime CreatedAt { get; set; }
}
