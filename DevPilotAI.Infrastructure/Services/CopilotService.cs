using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Chat;
using DevPilotAI.Application.DTOs.Copilot;
using DevPilotAI.Application.DTOs.Project;
using DevPilotAI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Infrastructure.Services;

public class CopilotService : ICopilotService
{
    private readonly ISemanticRetrievalService _retrievalService;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IChatProviderFactory _providerFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CopilotService> _logger;
    private readonly IRepositoryContextExpander _contextExpander;
    private readonly IRepositoryGraphService _graphService;

    public CopilotService(
        ISemanticRetrievalService retrievalService,
        IPromptBuilder promptBuilder,
        IChatProviderFactory providerFactory,
        IConfiguration configuration,
        ILogger<CopilotService> logger,
        IRepositoryContextExpander contextExpander,
        IRepositoryGraphService graphService)
    {
        _retrievalService = retrievalService;
        _promptBuilder = promptBuilder;
        _providerFactory = providerFactory;
        _configuration = configuration;
        _logger = logger;
        _contextExpander = contextExpander;
        _graphService = graphService;
    }

    public async Task<CopilotResponseDto> ExecuteAsync(
        Guid projectId,
        CopilotRequest request,
        CancellationToken cancellationToken)
    {
        var totalStopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Executing Copilot Action: {Mode} on Target: {Target}", request.Mode, request.Target);

        var promptModeName = MapModeToTemplateName(request.Mode);
        
        var settings = new ChatSettingsDto
        {
            Provider = _configuration["ChatSettings:Provider"] ?? _configuration["EmbeddingSettings:Provider"] ?? "Mock",
            Model = _configuration["ChatSettings:Model"] ?? _configuration["EmbeddingSettings:Model"] ?? "gpt-4"
        };

        // Pre-query adaptions for better context
        var targetQuery = request.Target;
        if (request.Mode == CopilotMode.Architecture && 
            (targetQuery.Contains("auth", StringComparison.OrdinalIgnoreCase) || targetQuery.Contains("login", StringComparison.OrdinalIgnoreCase)))
        {
            // Architecture mode: pre-fetch key startup/identity files
            targetQuery += " Program.cs DependencyInjection AuthController JwtService IdentityService RefreshToken";
        }

        // 1. Retrieve code context
        var retrievalStopwatch = Stopwatch.StartNew();
        var retrievalResult = await _retrievalService.RetrieveDetailedContextAsync(projectId, targetQuery, cancellationToken);
        retrievalStopwatch.Stop();

        var contextChunks = retrievalResult.Chunks;

        // Intent Strategy: If Architecture, fetch Program.cs / DbContext chunks
        if (request.Mode == CopilotMode.Architecture)
        {
            var archChunks = await _graphService.GetArchitectureChunksAsync(projectId, cancellationToken);
            var mappedArch = archChunks.Select(ch => new CodeChunkDto
            {
                Id = ch.Id,
                ProjectId = ch.ProjectId,
                ParsedFileId = ch.ParsedFileId,
                ParsedClassId = ch.ParsedClassId,
                ParsedMethodId = ch.ParsedMethodId,
                ChunkType = ch.ChunkType,
                Content = ch.Content,
                TokenCount = ch.TokenCount,
                Hash = ch.Hash,
                Metadata = ch.Metadata,
                StartLine = int.TryParse(GetMetaValue(ch.Metadata, "start_line"), out var sLine) ? sLine : 0,
                EndLine = int.TryParse(GetMetaValue(ch.Metadata, "end_line"), out var eLine) ? eLine : 0,
                RetrievalExplanation = "Exact File Name match (Architecture query strategy)"
            }).ToList();
            contextChunks = contextChunks.Concat(mappedArch).GroupBy(ch => ch.Id).Select(g => g.First()).ToList();
        }

        // 2. Perform Multi-Hop BFS Expansion
        var additionalSymbols = new List<string>();
        if (!string.IsNullOrEmpty(request.Target))
        {
            additionalSymbols.Add(request.Target);
        }
        var repoContext = await _contextExpander.ExpandContextAsync(projectId, contextChunks, additionalSymbols, cancellationToken);

        // Sources mapping
        var sources = contextChunks.Select(c =>
        {
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

        // 3. Compute Weighted Confidence Score
        double symbolMatchScore = 0.0;
        double relationshipScore = 0.0;
        double similarityScore = 0.0;
        double contextQualityScore = 0.0;
        double coverageScore = 0.0;

        // Symbol Match (40%)
        bool hasExactMatch = contextChunks.Any(c => 
            c.RetrievalExplanation.Contains("Exact SymbolName") || 
            c.RetrievalExplanation.Contains("Exact File Name") ||
            c.RetrievalExplanation.Contains("Exact Class Name"));
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

        string confidenceRating = "Limited Context";
        if (confidenceScore >= 80.0) confidenceRating = "Very Reliable";
        else if (confidenceScore >= 50.0) confidenceRating = "Reliable";

        // Check Confidence Bands: less than 50 returns immediate message without calling LLM
        if (confidenceScore < 50.0)
        {
            totalStopwatch.Stop();
            return new CopilotResponseDto
            {
                Content = "I couldn't confidently locate this symbol.",
                Sources = sources,
                RetrievedChunks = retrievalResult.CandidateChunks,
                RelevantChunks = retrievalResult.FinalChunks,
                IgnoredChunks = retrievalResult.FilteredChunks,
                IgnoredReasons = retrievalResult.IgnoredReasons,
                ConfidenceScore = confidenceScore,
                ConfidenceRating = confidenceRating,
                PromptMode = promptModeName,
                Provider = settings.Provider,
                TokenCount = 0,
                Duration = totalStopwatch.Elapsed,
                Metrics = new RetrievalMetrics
                {
                    CandidateChunks = retrievalResult.CandidateChunks,
                    FilteredChunks = retrievalResult.FilteredChunks,
                    FinalChunks = retrievalResult.FinalChunks,
                    AverageSimilarity = retrievalResult.AverageSimilarity,
                    RetrievalTime = retrievalStopwatch.Elapsed,
                    LlmTime = TimeSpan.Zero
                }
            };
        }

        // 4. Ground prompt details dynamically using Roslyn metadata to prevent LLM hallucinations
        var instructions = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(request.AdditionalInstructions))
        {
            instructions.AppendLine($"Additional Instructions: {request.AdditionalInstructions}");
        }

        if (request.Mode == CopilotMode.Navigation && repoContext.Relationships.Any())
        {
            instructions.AppendLine("\nFactual execution navigation path (ground response in this trace directly):");
            foreach (var rel in repoContext.Relationships)
            {
                instructions.AppendLine($"  - {rel.FromSymbol} ➔ {rel.RelationshipType} ➔ {rel.ToSymbol}");
            }
        }
        else if (request.Mode == CopilotMode.DependencyGraph)
        {
            instructions.AppendLine("\nFactual database-resolved class relationships (ground response in this structure directly):");
            foreach (var rel in repoContext.Relationships)
            {
                instructions.AppendLine($"  - {rel.FromSymbol} {rel.RelationshipType} {rel.ToSymbol}");
            }
        }
        else if (request.Mode == CopilotMode.ImpactAnalysis)
        {
            instructions.AppendLine("\nFactual class usage references (ground response in this analysis directly):");
            instructions.AppendLine("Direct dependents/usages that will break or require refactoring:");
            if (repoContext.Relationships.Any(r => r.RelationshipType == "injects" || r.RelationshipType == "uses"))
            {
                foreach (var rel in repoContext.Relationships.Where(r => r.RelationshipType == "injects" || r.RelationshipType == "uses"))
                {
                    instructions.AppendLine($"  - {rel.FromSymbol} consumes {rel.ToSymbol}");
                }
            }
            else
            {
                instructions.AppendLine("  - No usages found in workspace.");
            }
        }

        // 5. Construct prompt question
        var question = request.Target;
        if (instructions.Length > 0)
        {
            question += $"\n\n{instructions}";
        }

        // 6. Build message prompts list
        var prompts = _promptBuilder.BuildRagPrompt(promptModeName, question, repoContext, new List<ChatMessageDto>());

        // 8. Resolve provider & call LLM
        var provider = _providerFactory.GetProvider(settings.Provider);
        var llmStopwatch = Stopwatch.StartNew();
        var chatResponse = await provider.GetResponseAsync(prompts, settings, cancellationToken);
        llmStopwatch.Stop();

        var finalContent = chatResponse.Content;
        // Band 50-79: Prepend limited context notice
        if (confidenceScore >= 50.0 && confidenceScore < 80.0)
        {
            finalContent = "> [!NOTE]\n> Limited project context was found; the response may be incomplete.\n\n" + finalContent;
        }

        totalStopwatch.Stop();

        return new CopilotResponseDto
        {
            Content = finalContent,
            Sources = sources,
            RetrievedChunks = retrievalResult.CandidateChunks,
            RelevantChunks = retrievalResult.FinalChunks,
            IgnoredChunks = retrievalResult.FilteredChunks,
            IgnoredReasons = retrievalResult.IgnoredReasons,
            ConfidenceScore = confidenceScore,
            ConfidenceRating = confidenceRating,
            PromptMode = promptModeName,
            Provider = settings.Provider,
            TokenCount = chatResponse.TokenCount,
            Duration = totalStopwatch.Elapsed,
            Metrics = new RetrievalMetrics
            {
                CandidateChunks = retrievalResult.CandidateChunks,
                FilteredChunks = retrievalResult.FilteredChunks,
                FinalChunks = retrievalResult.FinalChunks,
                AverageSimilarity = retrievalResult.AverageSimilarity,
                RetrievalTime = retrievalStopwatch.Elapsed,
                LlmTime = llmStopwatch.Elapsed
            }
        };
    }

    private List<CodeChunkDto> GroupChunksByCategory(List<CodeChunkDto> chunks)
    {
        var controllers = new List<CodeChunkDto>();
        var services = new List<CodeChunkDto>();
        var repositories = new List<CodeChunkDto>();
        var interfaces = new List<CodeChunkDto>();
        var entities = new List<CodeChunkDto>();
        var config = new List<CodeChunkDto>();
        var other = new List<CodeChunkDto>();

        foreach (var c in chunks)
        {
            var filePath = GetMetaValue(c.Metadata, "file_path").ToLowerInvariant();
            var symbolName = GetMetaValue(c.Metadata, "symbol_name").ToLowerInvariant();

            if (filePath.Contains("controller") || symbolName.Contains("controller")) controllers.Add(c);
            else if (filePath.Contains("service") || symbolName.Contains("service")) services.Add(c);
            else if (filePath.Contains("repository") || symbolName.Contains("repository") || filePath.Contains("persistence")) repositories.Add(c);
            else if (symbolName.StartsWith("i") && symbolName.Length > 1 && char.IsUpper(symbolName[1])) interfaces.Add(c);
            else if (filePath.Contains("entities") || filePath.Contains("models") || filePath.Contains("dtos") || symbolName.EndsWith("dto")) entities.Add(c);
            else if (filePath.EndsWith("program.cs") || filePath.EndsWith("startup.cs") || filePath.Contains("config") || filePath.Contains("appsettings")) config.Add(c);
            else other.Add(c);
        }

        var result = new List<CodeChunkDto>();
        if (controllers.Any()) { result.AddRange(controllers); }
        if (services.Any()) { result.AddRange(services); }
        if (repositories.Any()) { result.AddRange(repositories); }
        if (interfaces.Any()) { result.AddRange(interfaces); }
        if (entities.Any()) { result.AddRange(entities); }
        if (config.Any()) { result.AddRange(config); }
        if (other.Any()) { result.AddRange(other); }

        return result;
    }

    private string GetMetaValue(string? metadata, string key)
    {
        if (string.IsNullOrEmpty(metadata)) return string.Empty;
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(metadata);
            if (dict != null && dict.TryGetValue(key, out var val)) return val;
        }
        catch {}
        return string.Empty;
    }

    private string MapModeToTemplateName(CopilotMode mode)
    {
        return mode switch
        {
            CopilotMode.Review => "ReviewCode",
            CopilotMode.BugAnalysis => "FindBugs",
            CopilotMode.Refactor => "RefactorCode",
            CopilotMode.UnitTests => "GenerateTests",
            CopilotMode.ApiDocumentation => "ExplainApi",
            CopilotMode.Architecture => "ExplainArchitecture",
            CopilotMode.Navigation => "NavigateCode",
            CopilotMode.ImpactAnalysis => "AnalyzeImpact",
            CopilotMode.DependencyGraph => "ExplainDependencies",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), $"Unsupported CopilotMode '{mode}'")
        };
    }
}
