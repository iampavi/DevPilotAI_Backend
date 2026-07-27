using System.Text.Json.Serialization;

namespace DevPilotAI.Application.DTOs.Copilot;

public class CopilotRequest
{
    public string Target { get; set; } = string.Empty;
    public string? AdditionalInstructions { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CopilotMode Mode { get; set; }
}
