using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
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
        RepositoryContextDto repositoryContext,
        List<ChatMessageDto> history)
    {
        var templatesSection = _configuration.GetSection("RagSettings:PromptTemplates");
        var template = templatesSection[templateMode];

        if (string.IsNullOrEmpty(template))
        {
            template = "You are DevPilot AI, a professional coding assistant. Use the following code context to answer the user's question.\n\nContext:\n{context}\n\nQuestion: {question}";
        }

        var contextBuilder = new StringBuilder();

        // 1. Append Relationships Summary if any exist
        if (repositoryContext.Relationships != null && repositoryContext.Relationships.Any())
        {
            contextBuilder.AppendLine("=========================================");
            contextBuilder.AppendLine("REPOSITORY RELATIONSHIPS");
            contextBuilder.AppendLine("=========================================");
            foreach (var rel in repositoryContext.Relationships)
            {
                contextBuilder.AppendLine($"- {rel.FromSymbol} {rel.RelationshipType} {rel.ToSymbol}");
            }
            contextBuilder.AppendLine();
        }

        // 2. Combine and format all chunks grouped by Namespace ➔ File ➔ Symbol
        var allChunks = repositoryContext.SeedChunks.Concat(repositoryContext.ExpandedChunks).ToList();
        var groupedString = FormatGroupedContext(allChunks);
        contextBuilder.Append(groupedString);

        var contextString = contextBuilder.ToString();

        var systemPrefix = "Repository Rules:\n" +
                           "- Repository context is the only source of truth.\n" +
                           "- Never fabricate missing information.\n" +
                           "- Never assume code exists.\n" +
                           "- Never create endpoints that were not retrieved.\n" +
                           "- Never generate DTOs not present in context.\n" +
                           "- If context is insufficient, say so.\n" +
                           "- Never use your general C# knowledge when the repository does not provide evidence.\n" +
                           "- Always cite retrieved symbols and source files/line numbers whenever possible.\n\n";

        var systemContent = systemPrefix + template
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

    private string FormatGroupedContext(List<CodeChunkDto> chunks)
    {
        if (chunks == null || !chunks.Any())
            return string.Empty;

        var contextBuilder = new StringBuilder();

        var grouped = chunks.Select(c => {
            string filePath = "Unknown File";
            string ns = "Global";
            string symbol = c.ChunkType;

            try
            {
                var metaDict = JsonSerializer.Deserialize<Dictionary<string, string>>(c.Metadata);
                if (metaDict != null)
                {
                    if (metaDict.TryGetValue("file_path", out var path) && !string.IsNullOrEmpty(path))
                        filePath = path;
                    if (metaDict.TryGetValue("namespace", out var nsp) && !string.IsNullOrEmpty(nsp))
                        ns = nsp;
                    if (metaDict.TryGetValue("class_name", out var cls) && !string.IsNullOrEmpty(cls))
                        symbol = cls;
                    else if (metaDict.TryGetValue("symbol_name", out var sym) && !string.IsNullOrEmpty(sym))
                        symbol = sym;
                }
            }
            catch { }

            return new { Chunk = c, FilePath = filePath, Namespace = ns, Symbol = symbol };
        })
        .GroupBy(x => x.Namespace)
        .OrderBy(g => g.Key);

        foreach (var nsGroup in grouped)
        {
            contextBuilder.AppendLine("=========================================");
            contextBuilder.AppendLine($"NAMESPACE: {nsGroup.Key}");
            contextBuilder.AppendLine("=========================================");
            contextBuilder.AppendLine();

            var fileGroups = nsGroup.GroupBy(x => x.FilePath).OrderBy(g => g.Key);
            foreach (var fileGroup in fileGroups)
            {
                contextBuilder.AppendLine($"--- FILE: {fileGroup.Key} ---");
                contextBuilder.AppendLine();

                var symbolGroups = fileGroup.GroupBy(x => x.Symbol).OrderBy(g => g.Key);
                foreach (var symbolGroup in symbolGroups)
                {
                    contextBuilder.AppendLine($"## SYMBOL: {symbolGroup.Key}");
                    foreach (var item in symbolGroup)
                    {
                        contextBuilder.AppendLine("```csharp");
                        contextBuilder.AppendLine(item.Chunk.Content);
                        contextBuilder.AppendLine("```");
                        contextBuilder.AppendLine();
                    }
                }
                contextBuilder.AppendLine("-----------------------------------------");
                contextBuilder.AppendLine();
            }
        }

        return contextBuilder.ToString();
    }

    private int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}
