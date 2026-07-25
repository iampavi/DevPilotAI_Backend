using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.DTOs.Chat;

namespace DevPilotAI.Application.Common.Interfaces;

public class ChatResponseDto
{
    public string Content { get; set; } = null!;
    public int TokenCount { get; set; }
}

public interface IChatProvider
{
    string ProviderName { get; }
    IAsyncEnumerable<string> StreamResponseAsync(List<ChatMessageDto> messages, ChatSettingsDto settings, CancellationToken cancellationToken = default);
    Task<ChatResponseDto> GetResponseAsync(List<ChatMessageDto> messages, ChatSettingsDto settings, CancellationToken cancellationToken = default);
}
