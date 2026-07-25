using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Project;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Infrastructure.Services;

public class SemanticRetrievalService : ISemanticRetrievalService
{
    private readonly IQdrantService _qdrantService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<SemanticRetrievalService> _logger;
    private readonly IConfiguration _configuration;

    private readonly int _topK;
    private readonly double _similarityThreshold;
    private readonly int _maxContextChunks;

    public SemanticRetrievalService(
        IQdrantService qdrantService,
        IEmbeddingService embeddingService,
        IApplicationDbContext context,
        IConfiguration configuration,
        ILogger<SemanticRetrievalService> logger)
    {
        _qdrantService = qdrantService;
        _embeddingService = embeddingService;
        _context = context;
        _logger = logger;
        _configuration = configuration;

        // Try reading configuration from RetrievalSettings section first, fallback to RagSettings
        _topK = int.TryParse(configuration["RetrievalSettings:TopK"] ?? configuration["RagSettings:TopK"], out var k) ? k : 10;
        _similarityThreshold = double.TryParse(configuration["RetrievalSettings:SimilarityThreshold"] ?? configuration["RagSettings:SimilarityThreshold"], out var threshold) ? threshold : 0.72;
        _maxContextChunks = int.TryParse(configuration["RagSettings:MaxContextChunks"], out var max) ? max : 10;
    }

    public async Task<List<CodeChunkDto>> RetrieveRelevantContextAsync(Guid projectId, string query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting semantic retrieval for project {ProjectId} with query: {Query}", projectId, query);

        // 0. Extract potential identifiers from query
        var words = System.Text.RegularExpressions.Regex.Matches(query, @"[A-Za-z_][A-Za-z0-9_]*")
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(m => m.Value)
            .Where(w => w.Length > 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ignoredKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "class", "interface", "struct", "record", "enum", "public", "private", "protected", "internal",
            "void", "async", "await", "task", "string", "int", "bool", "double", "float", "decimal",
            "explain", "what", "how", "does", "with", "this", "that", "from", "here", "there", "where",
            "code", "file", "method", "function", "variable", "property", "object", "type", "name",
            "project", "workspace", "return", "using", "namespace", "get", "set"
        };
        var identifiers = words.Where(w => !ignoredKeywords.Contains(w)).ToList();

        var symbolChunks = new List<CodeChunk>();
        if (identifiers.Any())
        {
            // Exact SymbolName Match (Class Name or Method Name)
            var directChunks = await _context.CodeChunks
                .Include(c => c.ParsedMethod)
                .Include(c => c.ParsedClass)
                .Include(c => c.ParsedFile)
                .Where(c => c.ProjectId == projectId && (
                    (c.ParsedMethod != null && identifiers.Contains(c.ParsedMethod.Name)) ||
                    (c.ParsedClass != null && identifiers.Contains(c.ParsedClass.Name))
                ))
                .ToListAsync(cancellationToken);

            // File Name Match
            var allFiles = await _context.ParsedFiles
                .Where(f => f.ProjectId == projectId)
                .ToListAsync(cancellationToken);

            var matchingFileIds = allFiles
                .Where(f => identifiers.Any(ident => 
                    f.RelativePath.Contains(ident, StringComparison.OrdinalIgnoreCase)))
                .Select(f => f.Id)
                .ToList();

            var fileChunks = await _context.CodeChunks
                .Include(c => c.ParsedMethod)
                .Include(c => c.ParsedClass)
                .Include(c => c.ParsedFile)
                .Where(c => c.ProjectId == projectId && matchingFileIds.Contains(c.ParsedFileId))
                .ToListAsync(cancellationToken);

            symbolChunks.AddRange(directChunks);
            symbolChunks.AddRange(fileChunks);
            symbolChunks = symbolChunks.GroupBy(c => c.Id).Select(g => g.First()).ToList();
        }

        // 1. Generate Query Embedding
        var queryVector = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
        var qdrantResults = new List<Guid>();
        if (queryVector != null && queryVector.Length > 0)
        {
            var collectionName = "devpilot-project-chunks";
            try
            {
                qdrantResults = await _qdrantService.SearchSimilarityAsync(collectionName, queryVector, projectId, _topK, cancellationToken)
                    ?? new List<Guid>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Qdrant similarity search failed.");
            }
        }

        // 3. Resolve combined SQL candidates
        var allChunkIds = qdrantResults.Union(symbolChunks.Select(c => c.Id)).ToList();
        if (allChunkIds.Count == 0)
        {
            return new List<CodeChunkDto>();
        }

        var chunks = await _context.CodeChunks
            .Include(c => c.ParsedMethod)
            .Include(c => c.ParsedClass)
            .Include(c => c.ParsedFile)
            .Where(c => c.ProjectId == projectId && allChunkIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        // Map mock similarity score based on order/rank: Rank 0 gets 0.95, Rank 1 gets 0.90, etc.
        var scoresMap = new Dictionary<Guid, double>();
        for (int i = 0; i < qdrantResults.Count; i++)
        {
            scoresMap[qdrantResults[i]] = Math.Max(0.5, 0.95 - (i * 0.05));
        }

        // Load ignored directories and files from configurations
        var ignoredDirs = _configuration.GetSection("RetrievalSettings:IgnoredDirectories").Get<List<string>>() 
            ?? new List<string> { "bin", "obj", "node_modules", ".git", "vendor", "dist", "build", "coverage", ".vs", ".idea", ".vscode" };
        var ignoredFiles = _configuration.GetSection("RetrievalSettings:IgnoredFiles").Get<List<string>>() 
            ?? new List<string> { "package-lock.json", "yarn.lock", "pnpm-lock.yaml", "composer.lock", "packages.lock.json" };

        var cleanChunks = new List<CodeChunk>();
        var seenHashes = new HashSet<string>();
        var seenContents = new HashSet<string>();

        foreach (var chunk in chunks)
        {
            var filePath = GetMetaValue(chunk.Metadata, "file_path").ToLowerInvariant();
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = chunk.ChunkType.ToLowerInvariant();
            }

            // Ignored Directories
            bool inIgnoredDir = ignoredDirs.Any(d => filePath.Contains("/" + d.ToLowerInvariant() + "/") || 
                                                     filePath.Contains("\\" + d.ToLowerInvariant() + "\\") || 
                                                     filePath.StartsWith(d.ToLowerInvariant() + "/") || 
                                                     filePath.StartsWith(d.ToLowerInvariant() + "\\"));
            if (inIgnoredDir) continue;

            // Ignored Files
            var fileName = Path.GetFileName(filePath);
            if (ignoredFiles.Any(f => f.Equals(fileName, StringComparison.OrdinalIgnoreCase))) continue;

            // Ignored Extensions (dll, exe, pdb, cache, log, min.js, min.css, map)
            if (fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".cache", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".min.css", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".map", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Duplicate filter
            var hash = !string.IsNullOrEmpty(chunk.Hash) ? chunk.Hash : chunk.Content;
            if (seenHashes.Contains(hash) || seenContents.Contains(chunk.Content))
            {
                continue;
            }
            seenHashes.Add(hash);
            seenContents.Add(chunk.Content);

            cleanChunks.Add(chunk);
        }

        // Apply similarity threshold filter
        var thresholdFiltered = cleanChunks
            .Where(c => {
                if (symbolChunks.Any(sc => sc.Id == c.Id)) return true;
                return scoresMap.TryGetValue(c.Id, out var score) && score >= _similarityThreshold;
            })
            .ToList();

        _logger.LogInformation("Found {Count} matches above threshold {Threshold}", thresholdFiltered.Count, _similarityThreshold);

        if (thresholdFiltered.Count == 0)
        {
            return new List<CodeChunkDto>();
        }

        // Merge adjacent chunks from the same method
        var mergedChunks = new List<CodeChunk>();
        var methodGroups = thresholdFiltered
            .Where(c => c.ParsedMethodId.HasValue)
            .GroupBy(c => c.ParsedMethodId!.Value)
            .ToList();

        var nonMethodChunks = thresholdFiltered.Where(c => !c.ParsedMethodId.HasValue).ToList();

        foreach (var group in methodGroups)
        {
            if (group.Count() == 1)
            {
                mergedChunks.Add(group.First());
            }
            else
            {
                var sorted = group.OrderBy(c => {
                    if (int.TryParse(GetMetaValue(c.Metadata, "start_line"), out var line)) return line;
                    return 0;
                }).ToList();

                var primary = sorted.First();
                // Merge contents with a separator
                var combinedContent = string.Join("\n\n// [Merged Adjacent Chunk]\n", sorted.Select(c => c.Content));
                
                primary.Content = combinedContent;
                primary.TokenCount = sorted.Sum(c => c.TokenCount);

                mergedChunks.Add(primary);
            }
        }
        mergedChunks.AddRange(nonMethodChunks);

        // 4. TF-IDF/Keyword Lexical Re-ranking and Structural Boosting
        var queryKeywords = query
            .ToLowerInvariant()
            .Split(new[] { ' ', '.', '_', ':', '(', ')', '{', '}', '[', ']', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .Distinct()
            .ToList();

        var rankedList = new List<(CodeChunk Chunk, double Score, string Explanation)>();

        foreach (var chunk in mergedChunks)
        {
            var similarityScore = scoresMap.TryGetValue(chunk.Id, out var s) ? s : 0.0;
            var contentLower = chunk.Content.ToLowerInvariant();
            var filePath = GetMetaValue(chunk.Metadata, "file_path").ToLowerInvariant();
            var symbolName = GetMetaValue(chunk.Metadata, "symbol_name").ToLowerInvariant();
            var chunkType = chunk.ChunkType.ToLowerInvariant();

            // Check exact/partial symbol boosts
            double symbolBoostValue = 0.0;
            string symbolExpl = "";

            if (identifiers.Any())
            {
                // Exact SymbolName Match
                bool isExactSymbol = false;
                if (!string.IsNullOrEmpty(symbolName))
                {
                    isExactSymbol = identifiers.Any(ident => string.Equals(ident, symbolName, StringComparison.OrdinalIgnoreCase));
                }

                // Exact File Name Match
                bool isExactFile = false;
                if (!string.IsNullOrEmpty(filePath))
                {
                    var fileNameOnly = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
                    var fileNameWithExt = Path.GetFileName(filePath).ToLowerInvariant();
                    isExactFile = identifiers.Any(ident => 
                        string.Equals(ident, fileNameOnly, StringComparison.OrdinalIgnoreCase) || 
                        string.Equals(ident, fileNameWithExt, StringComparison.OrdinalIgnoreCase));
                }

                // Exact Class Name Match
                bool isExactClass = false;
                var chunkClassName = GetMetaValue(chunk.Metadata, "class_name");
                if (string.IsNullOrEmpty(chunkClassName) && chunk.ParsedClass != null)
                {
                    chunkClassName = chunk.ParsedClass.Name;
                }
                if (!string.IsNullOrEmpty(chunkClassName))
                {
                    isExactClass = identifiers.Any(ident => string.Equals(ident, chunkClassName, StringComparison.OrdinalIgnoreCase));
                }

                // Partial Match
                bool isPartial = false;
                if (!isExactSymbol && !isExactFile && !isExactClass)
                {
                    isPartial = identifiers.Any(ident => 
                        (!string.IsNullOrEmpty(symbolName) && symbolName.Contains(ident, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(filePath) && filePath.Contains(ident, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(chunkClassName) && chunkClassName.Contains(ident, StringComparison.OrdinalIgnoreCase)));
                }

                if (isExactSymbol)
                {
                    symbolBoostValue = 2.0;
                    symbolExpl = "Exact SymbolName (+2.0)";
                }
                else if (isExactFile)
                {
                    symbolBoostValue = 1.5;
                    symbolExpl = "Exact File Name (+1.5)";
                }
                else if (isExactClass)
                {
                    symbolBoostValue = 1.0;
                    symbolExpl = "Exact Class Name (+1.0)";
                }
                else if (isPartial)
                {
                    symbolBoostValue = 0.5;
                    symbolExpl = "Partial Match (+0.5)";
                }
            }

            double finalScore = symbolBoostValue;
            var expl = symbolBoostValue > 0 ? $"Base (Symbol): {symbolBoostValue:F2} [{symbolExpl}]" : "Base: 0.00";

            finalScore += similarityScore;
            expl += $", Semantic: {similarityScore:F2}";

            // 1. Lexical Keyword match score
            double lexicalScore = 0.0;
            if (queryKeywords.Count > 0)
            {
                var matches = queryKeywords.Count(kw => contentLower.Contains(kw) || filePath.Contains(kw) || symbolName.Contains(kw));
                lexicalScore = (double)matches / queryKeywords.Count;
            }
            double lexicalWeight = 0.20 * lexicalScore;
            finalScore += lexicalWeight;
            if (lexicalWeight > 0)
            {
                expl += $", Lexical: +{lexicalWeight:F2}";
            }

            // 2. Folder Weights & Symbol boosting
            double boost = 0.0;

            // Authentication & Authorization core classes boost
            if (filePath.Contains("auth") || symbolName.Contains("auth") || 
                filePath.Contains("jwt") || symbolName.Contains("jwt") || 
                filePath.Contains("token") || symbolName.Contains("token") || 
                filePath.Contains("identity") || symbolName.Contains("identity") ||
                filePath.Contains("login") || symbolName.Contains("login") ||
                filePath.Contains("register") || symbolName.Contains("register") ||
                filePath.Contains("user") || symbolName.Contains("user"))
            {
                boost += 0.30;
            }

            // Program / Startup configuration boost
            if (filePath.EndsWith("program.cs") || filePath.EndsWith("startup.cs") || filePath.Contains("dependencyinjection"))
            {
                boost += 0.25;
            }

            // Controllers, Services, Repositories, Interfaces boost
            if (symbolName.EndsWith("controller") || filePath.Contains("controller") || filePath.Contains("/controllers/"))
            {
                boost += 0.25;
            }
            if (symbolName.EndsWith("service") || filePath.Contains("service") || filePath.Contains("/services/"))
            {
                boost += 0.20;
            }
            if (symbolName.EndsWith("repository") || filePath.Contains("repository") || filePath.Contains("/repositories/"))
            {
                boost += 0.20;
            }
            if (symbolName.StartsWith("i") && symbolName.Length > 1 && char.IsUpper(symbolName[1]))
            {
                boost += 0.15;
            }

            // Folder Weights: Application, Infrastructure, Domain, Persistence
            if (filePath.Contains("/application/") || filePath.Contains("/infrastructure/") || 
                filePath.Contains("/domain/") || filePath.Contains("/persistence/"))
            {
                boost += 0.10;
            }

            // DTOs & Entities
            if (filePath.Contains("/entities/") || filePath.Contains("/dtos/") || symbolName.EndsWith("dto"))
            {
                boost += 0.10;
            }

            if (boost > 0)
            {
                finalScore += boost;
                expl += $", Boost: +{boost:F2}";
            }

            // 3. Penalize Noise
            double penalty = 0.0;
            bool isNoiseFile = filePath.EndsWith("package.json") || 
                               filePath.EndsWith("readme.md") || 
                               filePath.EndsWith("appsettings.json") || 
                               filePath.EndsWith("appsettings.development.json") || 
                               filePath.EndsWith(".csproj") || 
                               filePath.EndsWith(".xml") ||
                               chunkType == "file" && filePath.Contains("config");

            if (isNoiseFile)
            {
                bool queryAsksForConfig = queryKeywords.Any(kw => 
                    kw.Contains("config") || kw.Contains("setting") || 
                    kw.Contains("package") || kw.Contains("dependency") || 
                    kw.Contains("readme") || kw.Contains("project") || kw.Contains("csproj"));

                if (!queryAsksForConfig)
                {
                    penalty = -0.40;
                    finalScore += penalty;
                    expl += $", Penalty: {penalty:F2}";
                }
            }

            // 4. Prefer Code over Configuration/Text
            if (chunk.ChunkType == "Class" || chunk.ChunkType == "Method" || chunk.ChunkType == "Property")
            {
                finalScore += 0.05;
                expl += $", Structure: +0.05";
            }

            expl += $", Final Score: {finalScore:F2}";
            rankedList.Add((chunk, finalScore, expl));
        }

        var classIds = mergedChunks.Where(c => c.ParsedClassId.HasValue).Select(c => c.ParsedClassId!.Value).Distinct().ToList();
        var methodIds = mergedChunks.Where(c => c.ParsedMethodId.HasValue).Select(c => c.ParsedMethodId!.Value).Distinct().ToList();

        var classes = classIds.Any()
            ? await _context.ParsedClasses.Where(c => classIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken)
            : new Dictionary<Guid, ParsedClass>();

        var methods = methodIds.Any()
            ? await _context.ParsedMethods.Where(m => methodIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, cancellationToken)
            : new Dictionary<Guid, ParsedMethod>();

        var finalOrderedChunks = new List<CodeChunkDto>();
        foreach (var r in rankedList.OrderByDescending(x => x.Score).Take(_maxContextChunks))
        {
            var chunk = r.Chunk;
            int startLine = 1;
            int endLine = 1;

            if (chunk.ParsedMethodId.HasValue && methods.TryGetValue(chunk.ParsedMethodId.Value, out var method))
            {
                startLine = method.StartLine;
                endLine = method.EndLine;
            }
            else if (chunk.ParsedClassId.HasValue && classes.TryGetValue(chunk.ParsedClassId.Value, out var cls))
            {
                startLine = cls.StartLine;
                endLine = cls.EndLine;
            }
            else
            {
                var lineMatch = System.Text.RegularExpressions.Regex.Match(chunk.Content, @"Lines (\d+)-(\d+)");
                if (lineMatch.Success)
                {
                    startLine = int.Parse(lineMatch.Groups[1].Value);
                    endLine = int.Parse(lineMatch.Groups[2].Value);
                }
                else
                {
                    var linesCount = chunk.Content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Length;
                    startLine = 1;
                    endLine = Math.Max(1, linesCount);
                }
            }

            finalOrderedChunks.Add(new CodeChunkDto
            {
                Id = chunk.Id,
                ProjectId = chunk.ProjectId,
                ParsedFileId = chunk.ParsedFileId,
                ParsedClassId = chunk.ParsedClassId,
                ParsedMethodId = chunk.ParsedMethodId,
                ChunkType = chunk.ChunkType,
                Content = chunk.Content,
                TokenCount = chunk.TokenCount,
                Hash = chunk.Hash,
                EmbeddingModel = chunk.EmbeddingModel,
                EmbeddingVersion = chunk.EmbeddingVersion,
                Metadata = chunk.Metadata,
                StartLine = startLine,
                EndLine = endLine,
                RetrievalExplanation = r.Explanation
            });
        }

        _logger.LogInformation("Retrieved and ranked {Count} context chunks.", finalOrderedChunks.Count);
        return finalOrderedChunks;
    }

    private string GetMetaValue(string? metadata, string key)
    {
        if (string.IsNullOrEmpty(metadata)) return string.Empty;
        try
        {
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(metadata);
            if (dict != null && dict.TryGetValue(key, out var val)) return val;
        }
        catch {}
        return string.Empty;
    }
}
