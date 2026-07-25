using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.DTOs.Chat;

namespace DevPilotAI.Application.Common.Interfaces;

public interface IAiChatService
{
    Task<ChatSessionDto> CreateSessionAsync(Guid projectId, string title, CancellationToken cancellationToken = default);
    Task<ChatMessageDto> SendMessageAsync(Guid sessionId, string userQuestion, string promptMode, ChatSettingsDto? settingsOverride = null, CancellationToken cancellationToken = default);
    Task<List<ChatSessionDto>> GetSessionsByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<List<ChatMessageDto>> GetMessageHistoryAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
