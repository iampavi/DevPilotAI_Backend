using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Chat;
using DevPilotAI.Application.DTOs.Project;
using Microsoft.Extensions.Configuration;

namespace DevPilotAI.Infrastructure.Services;

public class PromptBuilder : IPromptBuilder
{
    private readonly IConfiguration _configuration;
    private readonly int _maxPromptTokens;

    public PromptBuilder(IConfiguration configuration)
    {
        _configuration = configuration;
        _maxPromptTokens = int.TryParse(configuration["RagSettings:MaxPromptTokens"], out var tokens) ? tokens : 8000;
    }

    public List<ChatMessageDto> BuildRagPrompt(
        string templateMode,
        string userQuestion,
        List<CodeChunkDto> contextChunks,
        List<ChatMessageDto> history)
    {
        var templatesSection = _configuration.GetSection("RagSettings:PromptTemplates");
        var template = templatesSection[templateMode];

        if (string.IsNullOrEmpty(template))
        {
            template = "You are DevPilot AI, a professional coding assistant. Use the following code context to answer the user's question.\n\nContext:\n{context}\n\nQuestion: {question}";
        }

        var contextBuilder = new StringBuilder();
        foreach (var chunk in contextChunks)
        {
            contextBuilder.AppendLine($"File: {chunk.Metadata ?? "Unknown File"}");
            contextBuilder.AppendLine("```");
            contextBuilder.AppendLine(chunk.Content);
            contextBuilder.AppendLine("```");
            contextBuilder.AppendLine("---");
        }

        var contextString = contextBuilder.ToString();

        var systemContent = template
            .Replace("{context}", contextString)
            .Replace("{question}", userQuestion);

        var systemMessage = new ChatMessageDto
        {
            Id = Guid.NewGuid(),
            Role = "system",
            Content = systemContent,
            TokenCount = EstimateTokens(systemContent),
            CreatedAt = DateTime.UtcNow
        };

        var userMessage = new ChatMessageDto
        {
            Id = Guid.NewGuid(),
            Role = "user",
            Content = userQuestion,
            TokenCount = EstimateTokens(userQuestion),
            CreatedAt = DateTime.UtcNow
        };

        var systemTokenCount = systemMessage.TokenCount;
        var userTokenCount = userMessage.TokenCount;
        var availableHistoryTokens = _maxPromptTokens - (systemTokenCount + userTokenCount);

        var budgetedHistory = new List<ChatMessageDto>();
        var currentHistoryTokens = 0;

        for (int i = history.Count - 1; i >= 0; i--)
        {
            var msg = history[i];
            var msgTokens = msg.TokenCount > 0 ? msg.TokenCount : EstimateTokens(msg.Content);

            if (currentHistoryTokens + msgTokens > availableHistoryTokens)
            {
                break;
            }

            budgetedHistory.Insert(0, msg);
            currentHistoryTokens += msgTokens;
        }

        var promptList = new List<ChatMessageDto> { systemMessage };
        promptList.AddRange(budgetedHistory);
        promptList.Add(userMessage);

        return promptList;
    }

    private int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}
