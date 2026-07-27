using System.Collections.Generic;
using DevPilotAI.Application.DTOs.Chat;

namespace DevPilotAI.Application.Common.Interfaces;

public interface IPromptBuilder
{
    List<ChatMessageDto> BuildRagPrompt(
        string templateMode,
        string userQuestion,
        RepositoryContextDto repositoryContext,
        List<ChatMessageDto> history);
}
