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
using DevPilotAI.Application.DTOs.Project;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Infrastructure.Services;

public class AiChatService : IAiChatService
{
    private readonly IApplicationDbContext _context;
    private readonly ISemanticRetrievalService _retrievalService;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IChatProviderFactory _providerFactory;
    private readonly IMapper _mapper;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiChatService> _logger;
    private readonly IRepositoryContextExpander _contextExpander;
    private readonly IRepositoryGraphService _graphService;
    private readonly int _summarizeAfterCount;
    private readonly string _defaultProviderName;
    private readonly string _defaultModelName;

    public AiChatService(
        IApplicationDbContext context,
        ISemanticRetrievalService retrievalService,
        IPromptBuilder promptBuilder,
        IChatProviderFactory providerFactory,
        IMapper mapper,
        IConfiguration configuration,
        ILogger<AiChatService> logger,
        IRepositoryContextExpander contextExpander,
        IRepositoryGraphService graphService)
    {
        _context = context;
        _retrievalService = retrievalService;
        _promptBuilder = promptBuilder;
        _providerFactory = providerFactory;
        _mapper = mapper;
        _configuration = configuration;
        _logger = logger;
        _contextExpander = contextExpander;
        _graphService = graphService;

        _summarizeAfterCount = int.TryParse(configuration["RagSettings:SummarizeAfterMessagesCount"], out var limit) ? limit : 25;
        _defaultProviderName = configuration["ChatSettings:Provider"] ?? configuration["EmbeddingSettings:Provider"] ?? "Mock";
        _defaultModelName = configuration["ChatSettings:Model"] ?? configuration["EmbeddingSettings:Model"] ?? "gpt-4";
    }

    public async Task<ChatSessionDto> CreateSessionAsync(Guid projectId, string title, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating new chat session for project {ProjectId} with title: {Title}", projectId, title);

        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = title,
            CreatedAt = DateTime.UtcNow
        };

        _context.ChatSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ChatSessionDto>(session);
    }

    public async Task<ChatMessageDto> SendMessageAsync(
        Guid sessionId,
        string userQuestion,
        string promptMode,
        ChatSettingsOverrideDto? settingsOverride = null,
        CancellationToken cancellationToken = default)
    {
        var settings = new ChatSettingsDto
        {
            Provider = settingsOverride?.Provider ?? _configuration["ChatSettings:Provider"] ?? _configuration["EmbeddingSettings:Provider"] ?? "Mock",
            Model = settingsOverride?.Model ?? _configuration["ChatSettings:Model"] ?? _configuration["EmbeddingSettings:Model"] ?? "gpt-4",
            Temperature = settingsOverride?.Temperature ?? (double.TryParse(_configuration["ChatSettings:Temperature"], out var tempVal) ? tempVal : 0.7),
            MaxTokens = settingsOverride?.MaxTokens ?? (int.TryParse(_configuration["ChatSettings:MaxTokens"], out var maxVal) ? maxVal : 2048),
            TopP = settingsOverride?.TopP ?? (double.TryParse(_configuration["ChatSettings:TopP"], out var topPVal) ? topPVal : 0.9),
            FrequencyPenalty = settingsOverride?.FrequencyPenalty ?? (double.TryParse(_configuration["ChatSettings:FrequencyPenalty"], out var fpVal) ? fpVal : 0.0),
            PresencePenalty = settingsOverride?.PresencePenalty ?? (double.TryParse(_configuration["ChatSettings:PresencePenalty"], out var ppVal) ? ppVal : 0.0)
        };

        _logger.LogInformation("Processing message for session {SessionId} using provider {Provider}", sessionId, settings.Provider);

        var session = await _context.ChatSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
        {
            throw new ArgumentException($"Chat session '{sessionId}' not found.", nameof(sessionId));
        }

        var totalStopwatch = Stopwatch.StartNew();

        // 1. Retrieve Context
        var retrievalStopwatch = Stopwatch.StartNew();
        var retrievalResult = await _retrievalService.RetrieveDetailedContextAsync(session.ProjectId, userQuestion, cancellationToken);
        retrievalStopwatch.Stop();

        var contextChunks = retrievalResult.Chunks;

        // Intent Strategy: If Architecture or query mentions startup/auth, load Program.cs / DbContext chunks
        var isArch = promptMode.Equals("ExplainArchitecture", StringComparison.OrdinalIgnoreCase) ||
                     promptMode.Equals("ExplainDependencies", StringComparison.OrdinalIgnoreCase) ||
                     IsArchitectureQuery(userQuestion);

        if (isArch)
        {
            var archChunks = await _graphService.GetArchitectureChunksAsync(session.ProjectId, cancellationToken);
            var mappedArch = _mapper.Map<List<CodeChunkDto>>(archChunks);
            contextChunks = contextChunks.Concat(mappedArch).GroupBy(ch => ch.Id).Select(g => g.First()).ToList();
        }

        // 2. Perform Multi-Hop BFS Expansion
        var repoContext = await _contextExpander.ExpandContextAsync(session.ProjectId, contextChunks, new List<string>(), cancellationToken);

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

        // 3. Fetch Message History DTOs
        var messageHistoryDtos = session.Messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => _mapper.Map<ChatMessageDto>(m))
            .ToList();

        // 4. Build Prompt List
        var prompts = _promptBuilder.BuildRagPrompt(promptMode, userQuestion, repoContext, messageHistoryDtos);

        // 5. Compute Weighted Confidence Score
        double symbolMatchScore = 0.0;
        double relationshipScore = 0.0;
        double similarityScore = 0.0;
        double contextQualityScore = 0.0;
        double coverageScore = 0.0;

        // Symbol Match (40%)
        bool hasExactMatch = contextChunks.Any(c => 
            !string.IsNullOrEmpty(c.RetrievalExplanation) && (
            c.RetrievalExplanation.Contains("Exact SymbolName") || 
            c.RetrievalExplanation.Contains("Exact File Name") ||
            c.RetrievalExplanation.Contains("Exact Class Name")));
        if (hasExactMatch)
        {
            symbolMatchScore = 40.0;
        }

        // Relationship Match (25%)
        if (repoContext.Relationships != null && repoContext.Relationships.Any())
        {
            relationshipScore = 25.0;
        }

        // Semantic Similarity (20%)
        similarityScore = Math.Min(20.0, retrievalResult.AverageSimilarity * 20.0);

        // Context Quality (10%)
        if (retrievalResult.FinalChunks >= 3)
        {
            contextQualityScore = 10.0;
        }

        // Retrieval Coverage (5%)
        if (retrievalResult.FilteredChunks < 5)
        {
            coverageScore = 5.0;
        }

        double confidenceScore = symbolMatchScore + relationshipScore + similarityScore + contextQualityScore + coverageScore;
        confidenceScore = Math.Min(100.0, Math.Max(0.0, confidenceScore));

        var retrievedSymbols = repoContext.ReferencedSymbols;
        var sourceFiles = repoContext.RelatedFiles;

        double similarityThreshold = double.TryParse(_configuration["RetrievalSettings:SimilarityThreshold"] ?? _configuration["RagSettings:SimilarityThreshold"], out var stVal) ? stVal : 0.7;

        string chatResponseContent;
        int tokenCount = 0;
        double responseTimeMs = 0.0;

        if (confidenceScore < 50.0)
        {
            chatResponseContent = "Insufficient repository context.";
        }
        else
        {
            // 6. Retrieve Chat Provider
            var provider = _providerFactory.GetProvider(settings.Provider);

            // 7. Generate AI Completion
            var providerStopwatch = Stopwatch.StartNew();
            var chatResponse = await provider.GetResponseAsync(prompts, settings, cancellationToken);
            providerStopwatch.Stop();

            chatResponseContent = chatResponse.Content;
            tokenCount = chatResponse.TokenCount;
            responseTimeMs = providerStopwatch.Elapsed.TotalMilliseconds;
        }

        var fullMetadata = new ChatMessageMetadataDto
        {
            Sources = sources,
            ConfidenceScore = confidenceScore,
            RetrievedSymbols = retrievedSymbols,
            SourceFiles = sourceFiles,
            RetrievedChunksCount = retrievalResult.CandidateChunks,
            SimilarityThreshold = similarityThreshold,
            ChunkCount = retrievalResult.FinalChunks,
            ModelUsed = settings.Model,
            Provider = settings.Provider,
            ResponseTimeMs = responseTimeMs,
            PromptMode = promptMode,
            Relationships = repoContext.Relationships
        };

        var sourcesMetadataJson = JsonSerializer.Serialize(fullMetadata);

        // 6. Save messages to DB
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
            Content = chatResponseContent,
            TokenCount = tokenCount,
            Metadata = sourcesMetadataJson,
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
            LatencyMilliseconds = totalStopwatch.ElapsedMilliseconds,
            CreatedAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(userMessage);
        _context.ChatMessages.Add(assistantMessage);
        _context.AiUsageLogs.Add(usageLog);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("RAG Complete. Telemetry Details: Session={SessionId}, Chunks={ChunksCount}, LatencyMs={Latency}, PromptTokens={PromptTokens}, CompletionTokens={CompletionTokens}",
            sessionId, contextChunks.Count, totalStopwatch.ElapsedMilliseconds, userMessage.TokenCount, assistantMessage.TokenCount);

        foreach (var chunk in contextChunks)
        {
            _logger.LogInformation("Chunk {ChunkId} explainability: {Explanation}", chunk.Id, chunk.RetrievalExplanation);
        }

        var totalMessagesCount = session.Messages.Count + 2;
        if (totalMessagesCount >= _summarizeAfterCount)
        {
            await RunSummarizationAsync(session, settings, cancellationToken);
        }

        return _mapper.Map<ChatMessageDto>(assistantMessage);
    }

    public async Task<List<ChatSessionDto>> GetSessionsByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var sessions = await _context.ChatSessions
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<ChatSessionDto>>(sessions);
    }

    public async Task<List<ChatMessageDto>> GetMessageHistoryAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var messages = await _context.ChatMessages
            .Where(m => m.ChatSessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<ChatMessageDto>>(messages);
    }

    public async Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _context.ChatSessions.FindAsync(new object[] { sessionId }, cancellationToken);
        if (session != null)
        {
            session.IsDeleted = true;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task RunSummarizationAsync(ChatSession session, ChatSettingsDto settings, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Triggering conversation summarization for session {SessionId}", session.Id);

        var messagesToSummarize = await _context.ChatMessages
            .Where(m => m.ChatSessionId == session.Id)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        if (messagesToSummarize.Count < 4) return;

        var provider = _providerFactory.GetProvider(settings.Provider);
        
        var summaryPrompt = new List<ChatMessageDto>
        {
            new ChatMessageDto 
            { 
                Role = "system", 
                Content = "Summarize the key discussion points, code elements discussed, and decisions from the following chat history in less than 3 sentences." 
            },
            new ChatMessageDto 
            { 
                Role = "user", 
                Content = string.Join("\n", messagesToSummarize.Select(m => $"{m.Role}: {m.Content}")) 
            }
        };

        try
        {
            var summaryResponse = await provider.GetResponseAsync(summaryPrompt, settings, cancellationToken);
            
            _context.ChatMessages.RemoveRange(messagesToSummarize);

            var summaryAnchorMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ChatSessionId = session.Id,
                Role = "system",
                Content = $"[Summary of earlier discussion]: {summaryResponse.Content}",
                TokenCount = summaryResponse.TokenCount,
                CreatedAt = DateTime.UtcNow.AddSeconds(-1)
            };

            _context.ChatMessages.Add(summaryAnchorMessage);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Session {SessionId} summarized and archived successfully.", session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate session summary.");
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

    private bool IsArchitectureQuery(string question)
    {
        if (string.IsNullOrEmpty(question)) return false;
        var keywords = new[] { "architecture", "startup", "middleware", "auth", "authentication", "jwt", "login", "dependency injection", "configure" };
        return keywords.Any(k => question.Contains(k, StringComparison.OrdinalIgnoreCase));
    }
}
