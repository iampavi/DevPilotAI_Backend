namespace DevPilotAI.Application.DTOs.Chat;

public class ChatSettingsDto
{
    public string Provider { get; set; } = "Mock";
    public string Model { get; set; } = "gpt-4";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2048;
    public double TopP { get; set; } = 0.9;
    public double FrequencyPenalty { get; set; } = 0.0;
    public double PresencePenalty { get; set; } = 0.0;
}
