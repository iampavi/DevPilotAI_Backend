using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevPilotAI.Application.DTOs.Chat;

namespace DevPilotAI.Application.Common.Interfaces;

public interface IChatHubClient
{
    Task ChatStarted(Guid sessionId, string promptMode);
    Task TokenReceived(Guid sessionId, string token);
    Task SourcesRetrieved(Guid sessionId, List<RetrievalSourceDto> sources);
    Task ChatCompleted(Guid sessionId, ChatDiagnosticsDto diagnostics);
    Task ErrorOccurred(Guid sessionId, string errorMessage);
}
