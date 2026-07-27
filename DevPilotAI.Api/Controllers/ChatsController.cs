using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Chat;
using DevPilotAI.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevPilotAI.Api.Controllers;

public class CreateChatSessionRequest
{
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = null!;
}

public class SendChatMessageRequest
{
    public string UserQuestion { get; set; } = null!;
    public string PromptMode { get; set; } = "ExplainCode";
    public ChatSettingsOverrideDto? SettingsOverride { get; set; }
}

[Authorize]
public class ChatsController : ApiControllerBase
{
    private readonly IAiChatService _chatService;

    public ChatsController(IAiChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost("sessions")]
    public async Task<ActionResult<ApiResponse<ChatSessionDto>>> CreateSession([FromBody] CreateChatSessionRequest request, CancellationToken cancellationToken)
    {
        var session = await _chatService.CreateSessionAsync(request.ProjectId, request.Title, cancellationToken);
        return Ok(ApiResponse<ChatSessionDto>.Success(session, "Chat session created successfully."));
    }

    [HttpPost("sessions/{sessionId}/messages")]
    public async Task<ActionResult<ApiResponse<ChatMessageDto>>> SendMessage(
        Guid sessionId,
        [FromBody] SendChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        var message = await _chatService.SendMessageAsync(
            sessionId,
            request.UserQuestion,
            request.PromptMode,
            request.SettingsOverride,
            cancellationToken);

        return Ok(ApiResponse<ChatMessageDto>.Success(message, "Message processed successfully."));
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<ApiResponse<List<ChatSessionDto>>>> GetSessions([FromQuery] Guid projectId, CancellationToken cancellationToken)
    {
        var sessions = await _chatService.GetSessionsByProjectAsync(projectId, cancellationToken);
        return Ok(ApiResponse<List<ChatSessionDto>>.Success(sessions, "Chat sessions retrieved successfully."));
    }

    [HttpGet("sessions/{sessionId}/messages")]
    public async Task<ActionResult<ApiResponse<List<ChatMessageDto>>>> GetMessageHistory(Guid sessionId, CancellationToken cancellationToken)
    {
        var messages = await _chatService.GetMessageHistoryAsync(sessionId, cancellationToken);
        return Ok(ApiResponse<List<ChatMessageDto>>.Success(messages, "Message history retrieved successfully."));
    }

    [HttpDelete("sessions/{sessionId}")]
    public async Task<ActionResult<ApiResponse>> DeleteSession(Guid sessionId, CancellationToken cancellationToken)
    {
        await _chatService.DeleteSessionAsync(sessionId, cancellationToken);
        return Ok(ApiResponse.Success("Chat session deleted successfully."));
    }
}
