using System.Collections.Generic;
using DevPilotAI.Application.DTOs.Chat;
using DevPilotAI.Application.DTOs.Project;

namespace DevPilotAI.Application.Common.Interfaces;

public interface IPromptBuilder
{
    List<ChatMessageDto> BuildRagPrompt(string templateMode, string userQuestion, List<CodeChunkDto> contextChunks, List<ChatMessageDto> history);
}
