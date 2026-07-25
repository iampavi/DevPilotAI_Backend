using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Chat;
using DevPilotAI.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Infrastructure.Hubs;

[Authorize]
public class ChatHub : Hub<IChatHubClient>
{
    private readonly IApplicationDbContext _context;
    private readonly ISemanticRetrievalService _retrievalService;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IChatProviderFactory _providerFactory;
    private readonly IMapper _mapper;
    private readonly ILogger<ChatHub> _logger;
    private readonly string _defaultProviderName;
    private readonly string _defaultModelName;

    public ChatHub(
        IApplicationDbContext context,
        ISemanticRetrievalService retrievalService,
        IPromptBuilder promptBuilder,
        IChatProviderFactory providerFactory,
        IMapper mapper,
        IConfiguration configuration,
        ILogger<ChatHub> logger)
    {
        _context = context;
        _retrievalService = retrievalService;
        _promptBuilder = promptBuilder;
        _providerFactory = providerFactory;
        _mapper = mapper;
        _logger = logger;

        _defaultProviderName = configuration["EmbeddingSettings:Provider"] ?? "Mock";
        _defaultModelName = configuration["EmbeddingSettings:Model"] ?? "gpt-4";
    }

    public async Task SendMessageStream(Guid sessionId, string userQuestion, string promptMode, ChatSettingsDto? settingsOverride)
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("Connection {ConnectionId} started chat streaming for session {SessionId}", connectionId, sessionId);

        var settings = settingsOverride ?? new ChatSettingsDto
        {
            Provider = _defaultProviderName,
            Model = _defaultModelName
        };

        try
        {
            await Clients.Caller.ChatStarted(sessionId, promptMode);

            var session = await _context.ChatSessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
            {
                await Clients.Caller.ErrorOccurred(sessionId, $"Chat session '{sessionId}' not found.");
                return;
            }

            var contextChunks = await _retrievalService.RetrieveRelevantContextAsync(session.ProjectId, userQuestion);

            var sources = contextChunks.Select(c => {
                string symbolName = c.ChunkType;
                string filePath = "Unknown File";
                try
                {
                    var metaDict = JsonSerializer.Deserialize<Dictionary<string, string>>(c.Metadata);
                    if (metaDict != null)
                    {
                        if (metaDict.TryGetValue("symbol_name", out var sym) && !string.IsNullOrEmpty(sym))
                            symbolName = sym;
                        if (metaDict.TryGetValue("file_path", out var path) && !string.IsNullOrEmpty(path))
                            filePath = path;
                    }
                }
                catch { }

                return new RetrievalSourceDto
                {
                    ChunkId = c.Id,
                    FilePath = filePath,
                    SymbolName = symbolName,
                    SimilarityScore = 0.9,
                    StartLine = c.StartLine,
                    EndLine = c.EndLine
                };
            }).ToList();

            await Clients.Caller.SourcesRetrieved(sessionId, sources);

            var messageHistoryDtos = session.Messages
                .OrderBy(m => m.CreatedAt)
                .Select(m => _mapper.Map<ChatMessageDto>(m))
                .ToList();

            var prompts = _promptBuilder.BuildRagPrompt(promptMode, userQuestion, contextChunks, messageHistoryDtos);

            var provider = _providerFactory.GetProvider(settings.Provider);

            var stopwatch = Stopwatch.StartNew();
            var fullResponseBuilder = new System.Text.StringBuilder();

            await foreach (var token in provider.StreamResponseAsync(prompts, settings, Context.ConnectionAborted))
            {
                fullResponseBuilder.Append(token);
                await Clients.Caller.TokenReceived(sessionId, token);
            }
            stopwatch.Stop();

            var finalContent = fullResponseBuilder.ToString();

            var userMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ChatSessionId = sessionId,
                Role = "user",
                Content = userQuestion,
                TokenCount = (int)Math.Ceiling(userQuestion.Length / 4.0),
                CreatedAt = DateTime.UtcNow
            };

            var assistantMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ChatSessionId = sessionId,
                Role = "assistant",
                Content = finalContent,
                TokenCount = (int)Math.Ceiling(finalContent.Length / 4.0),
                Metadata = JsonSerializer.Serialize(sources),
                CreatedAt = DateTime.UtcNow
            };

            var usageLog = new AiUsageLog
            {
                Id = Guid.NewGuid(),
                Provider = settings.Provider,
                Model = settings.Model,
                PromptTokens = userMessage.TokenCount,
                CompletionTokens = assistantMessage.TokenCount,
                TotalTokens = userMessage.TokenCount + assistantMessage.TokenCount,
                Cost = CalculateCost(settings.Provider, settings.Model, userMessage.TokenCount, assistantMessage.TokenCount),
                LatencyMilliseconds = stopwatch.ElapsedMilliseconds,
                CreatedAt = DateTime.UtcNow
            };

            _context.ChatMessages.Add(userMessage);
            _context.ChatMessages.Add(assistantMessage);
            _context.AiUsageLogs.Add(usageLog);
            await _context.SaveChangesAsync(Context.ConnectionAborted);

            var diagnostics = new ChatDiagnosticsDto
            {
                RetrievedChunksCount = contextChunks.Count,
                PromptTokens = userMessage.TokenCount,
                CompletionTokens = assistantMessage.TokenCount,
                LatencyMilliseconds = stopwatch.ElapsedMilliseconds,
                SimilarityScores = contextChunks.Select(_ => 0.9).ToList(),
                RetrievalScoresExplanation = contextChunks.Select(c => c.RetrievalExplanation).ToList()
            };

            await Clients.Caller.ChatCompleted(sessionId, diagnostics);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Connection {ConnectionId} cancelled the chat streaming request.", connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during chat streaming on connection {ConnectionId}", connectionId);
            await Clients.Caller.ErrorOccurred(sessionId, ex.Message);
        }
    }

    private decimal? CalculateCost(string provider, string model, int promptTokens, int completionTokens)
    {
        if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            if (model.Contains("gpt-4"))
            {
                return (promptTokens * 0.000030m) + (completionTokens * 0.000060m);
            }
            return (promptTokens * 0.000005m) + (completionTokens * 0.000015m);
        }
        return null;
    }
}
