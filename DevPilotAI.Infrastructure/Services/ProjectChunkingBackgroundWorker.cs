using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Infrastructure.Services;

public class ProjectChunkingBackgroundWorker : BackgroundService
{
    private readonly IProjectChunkingQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProjectChunkingBackgroundWorker> _logger;
    private readonly int _batchSize;

    public ProjectChunkingBackgroundWorker(
        IProjectChunkingQueue queue,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<ProjectChunkingBackgroundWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        if (!int.TryParse(configuration["EmbeddingSettings:BatchSize"], out _batchSize) || _batchSize <= 0)
        {
            _batchSize = 32; // Default fallback
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Project Chunking Background Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobItem = await _queue.DequeueChunkingJobAsync(stoppingToken);
                _logger.LogInformation("Processing chunking job {JobId} for project {ProjectId}.", jobItem.JobId, jobItem.ProjectId);

                await ProcessJobAsync(jobItem, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing project chunking background worker.");
            }
        }
    }

    private async Task ProcessJobAsync(ChunkingJobItem jobItem, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
        var qdrantService = scope.ServiceProvider.GetRequiredService<IQdrantService>();

        var jobEntity = await context.ProjectChunkingJobs.FirstOrDefaultAsync(j => j.Id == jobItem.JobId, cancellationToken);
        if (jobEntity == null)
        {
            _logger.LogError("Chunking job {JobId} not found in database. Aborting.", jobItem.JobId);
            return;
        }

        try
        {
            // 1. Update status to Running
            jobEntity.Status = JobStatus.Running;
            jobEntity.StartedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == jobItem.ProjectId, cancellationToken);
            if (project == null)
            {
                throw new Exception($"Project {jobItem.ProjectId} was not found.");
            }

            var collectionName = "devpilot-project-chunks";
            await qdrantService.EnsureCollectionExistsAsync(collectionName, embeddingService.Dimensions, cancellationToken);

            // 2. Fetch existing chunks in SQL
            var existingChunks = await context.CodeChunks
                .Where(c => c.ProjectId == jobItem.ProjectId)
                .ToListAsync(cancellationToken);

            // 3. Fetch all parsed files for this project
            var parsedFiles = await context.ParsedFiles
                .Where(f => f.ProjectId == jobItem.ProjectId)
                .Include(f => f.Classes)
                    .ThenInclude(c => c.Methods)
                .Include(f => f.Classes)
                    .ThenInclude(c => c.Properties)
                .Include(f => f.Classes)
                    .ThenInclude(c => c.Fields)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Generating chunks for {Count} parsed files.", parsedFiles.Count);

            var generatedChunks = new List<CodeChunk>();

            foreach (var file in parsedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var filePath = Path.Combine(project.SourceLocation, file.RelativePath);
                string fileContent = string.Empty;
                if (File.Exists(filePath))
                {
                    fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
                }

                if (file.Language == "C#")
                {
                     foreach (var cls in file.Classes)
                    {
                        // Class chunk
                        var classContent = GenerateClassChunkContent(cls);
                        generatedChunks.Add(CreateChunkInstance(jobItem.ProjectId, file.Id, cls.Id, null, "Class", classContent, cls.Name, file.RelativePath));

                        // Method chunks
                        foreach (var method in cls.Methods)
                        {
                            var methodContent = GenerateMethodChunkContent(cls, method, fileContent);
                            generatedChunks.Add(CreateChunkInstance(jobItem.ProjectId, file.Id, cls.Id, method.Id, "Method", methodContent, method.Name, file.RelativePath));
                        }

                        // Property chunks
                        foreach (var prop in cls.Properties)
                        {
                            var propContent = GeneratePropertyChunkContent(cls, prop);
                            generatedChunks.Add(CreateChunkInstance(jobItem.ProjectId, file.Id, cls.Id, null, "Property", propContent, prop.Name, file.RelativePath));
                        }
                    }
                }
                else
                {
                    // Non-C# sliding window chunks
                    var fileChunks = GenerateNonCSharpChunkContents(fileContent, file.RelativePath);
                    for (int i = 0; i < fileChunks.Count; i++)
                    {
                        generatedChunks.Add(CreateChunkInstance(jobItem.ProjectId, file.Id, null, null, "File", fileChunks[i], $"{file.RelativePath}_part_{i}", file.RelativePath));
                    }
                }
            }

            // 4. Incremental hashing comparison
            var keptChunkIds = new HashSet<Guid>();
            var chunksToUpsert = new List<CodeChunk>();

            var modelName = embeddingService.ConfiguredModel;
            var currentVersion = 1;

            foreach (var gen in generatedChunks)
            {
                // Find matching existing chunk with same signature and hash
                var match = existingChunks.FirstOrDefault(c =>
                    c.ParsedFileId == gen.ParsedFileId &&
                    c.ChunkType == gen.ChunkType &&
                    c.ParsedClassId == gen.ParsedClassId &&
                    c.ParsedMethodId == gen.ParsedMethodId &&
                    c.Hash == gen.Hash &&
                    c.EmbeddingModel == modelName &&
                    c.EmbeddingVersion == currentVersion);

                if (match != null)
                {
                    // Identical chunk already exists, keep it
                    keptChunkIds.Add(match.Id);
                }
                else
                {
                    // New or modified chunk
                    chunksToUpsert.Add(gen);
                }
            }

            // Chunks that are obsolete and should be deleted
            var chunksToDelete = existingChunks.Where(c => !keptChunkIds.Contains(c.Id)).ToList();

            _logger.LogInformation("Incremental result for project {ProjectId}: Preserved={Preserved}, ToUpsert={ToUpsert}, ToDelete={ToDelete}",
                jobItem.ProjectId, keptChunkIds.Count, chunksToUpsert.Count, chunksToDelete.Count);

            // 5. Delete obsolete chunks
            if (chunksToDelete.Count > 0)
            {
                var deleteIds = chunksToDelete.Select(c => c.Id).ToList();
                context.CodeChunks.RemoveRange(chunksToDelete);
                await context.SaveChangesAsync(cancellationToken);
                
                await qdrantService.DeleteVectorsAsync(collectionName, deleteIds, cancellationToken);
            }

            // 6. Generate embeddings and upsert new/updated chunks in batches
            if (chunksToUpsert.Count > 0)
            {
                int totalUpserts = chunksToUpsert.Count;
                int processed = 0;

                for (int i = 0; i < totalUpserts; i += _batchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var batch = chunksToUpsert.Skip(i).Take(_batchSize).ToList();
                    var batchTexts = batch.Select(c => c.Content).ToList();

                    // Generate embeddings via Polly-wrapped embedding service
                    var embeddings = await embeddingService.GenerateEmbeddingsAsync(batchTexts, cancellationToken);

                    var qdrantPoints = new List<QdrantPointDto>();

                    for (int j = 0; j < batch.Count; j++)
                    {
                        var chunk = batch[j];
                        var vector = embeddings[j];

                        chunk.EmbeddingModel = modelName;
                        chunk.EmbeddingVersion = currentVersion;

                        context.CodeChunks.Add(chunk);

                        // Parse out symbol name and file path from metadata payload if available
                        var metaDict = JsonSerializer.Deserialize<Dictionary<string, string>>(chunk.Metadata) ?? new();
                        var symbolName = metaDict.GetValueOrDefault("symbol_name", string.Empty);
                        var filePath = metaDict.GetValueOrDefault("file_path", string.Empty);

                        qdrantPoints.Add(new QdrantPointDto(
                            ChunkId: chunk.Id,
                            Vector: vector,
                            ProjectId: chunk.ProjectId,
                            FileId: chunk.ParsedFileId,
                            FilePath: filePath,
                            SymbolName: symbolName,
                            ChunkType: chunk.ChunkType
                        ));
                    }

                    // Save to SQL Database
                    await context.SaveChangesAsync(cancellationToken);

                    // Upsert vectors to Qdrant (Polly retry handled in service)
                    await qdrantService.UpsertVectorsAsync(collectionName, qdrantPoints, cancellationToken);

                    processed += batch.Count;
                    var progressPercent = (int)((double)processed / totalUpserts * 100);
                    jobEntity.Progress = progressPercent;
                    await context.SaveChangesAsync(cancellationToken);
                }
            }

            // 7. Complete job
            jobEntity.Status = JobStatus.Completed;
            jobEntity.Progress = 100;
            jobEntity.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Chunking and vector indexing successfully completed for project {ProjectId}.", jobItem.ProjectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chunking job {JobId} failed.", jobItem.JobId);

            jobEntity.Status = JobStatus.Failed;
            jobEntity.CompletedAt = DateTime.UtcNow;
            jobEntity.Error = ex.Message;

            await context.SaveChangesAsync(CancellationToken.None);
        }
    }

    private string GenerateClassChunkContent(ParsedClass cls)
    {
        var sb = new StringBuilder();
        foreach (var attr in cls.Attributes)
        {
            sb.AppendLine($"[{attr}]");
        }
        var baseTypeStr = cls.BaseTypes.Any() ? " : " + string.Join(", ", cls.BaseTypes) : "";
        sb.AppendLine($"{cls.SymbolType.ToString().ToLower()} {cls.Name}{baseTypeStr}");
        sb.AppendLine("{");
        foreach (var field in cls.Fields)
        {
            sb.AppendLine($"    {field.AccessModifier} {field.Type} {field.Name};");
        }
        foreach (var prop in cls.Properties)
        {
            sb.AppendLine($"    {prop.AccessModifier} {prop.Type} {prop.Name} {{ get; set; }}");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private string GenerateMethodChunkContent(ParsedClass cls, ParsedMethod method, string fileContent)
    {
        var body = string.Empty;
        if (!string.IsNullOrEmpty(fileContent))
        {
            var lines = fileContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int startIdx = Math.Max(0, method.StartLine - 1);
            int endIdx = Math.Min(lines.Length - 1, method.EndLine - 1);
            if (startIdx < lines.Length)
            {
                body = string.Join(Environment.NewLine, lines.Skip(startIdx).Take(endIdx - startIdx + 1));
            }
        }

        if (string.IsNullOrEmpty(body))
        {
            // Signature fallback if file lines are not resolved
            var paramStr = string.Join(", ", method.Parameters);
            body = $"{method.AccessModifier} {method.ReturnType ?? "void"} {method.Name}({paramStr}) {{ }}";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"// Namespace: {cls.Namespace}");
        sb.AppendLine($"// Class: {cls.FullName}");
        sb.AppendLine(body);
        return sb.ToString();
    }

    private string GeneratePropertyChunkContent(ParsedClass cls, ParsedProperty prop)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// Class: {cls.FullName}");
        foreach (var attr in prop.Attributes)
        {
            sb.AppendLine($"[{attr}]");
        }
        sb.AppendLine($"{prop.AccessModifier} {prop.Type} {prop.Name} {{ get; set; }}");
        return sb.ToString();
    }

    private List<string> GenerateNonCSharpChunkContents(string content, string relativePath)
    {
        var chunks = new List<string>();
        if (string.IsNullOrEmpty(content)) return chunks;

        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        
        if (lines.Length <= 50)
        {
            chunks.Add($"// File: {relativePath}{Environment.NewLine}{content}");
        }
        else
        {
            int totalLines = lines.Length;
            int windowSize = 50;
            int overlap = 10;
            int step = windowSize - overlap;
            
            for (int start = 0; start < totalLines; start += step)
            {
                var chunkLines = lines.Skip(start).Take(windowSize).ToList();
                var chunkText = $"// File: {relativePath} (Lines {start + 1}-{start + chunkLines.Count}){Environment.NewLine}" 
                                + string.Join(Environment.NewLine, chunkLines);
                chunks.Add(chunkText);
                if (start + windowSize >= totalLines) break;
            }
        }
        return chunks;
    }

    private CodeChunk CreateChunkInstance(Guid projectId, Guid fileId, Guid? classId, Guid? methodId, string chunkType, string content, string symbolName, string relativePath)
    {
        var hash = ComputeSha256Hash(content);
        var metaDict = new Dictionary<string, string>
        {
            { "symbol_name", symbolName },
            { "file_path", relativePath }
        };
        var meta = JsonSerializer.Serialize(metaDict);

        return new CodeChunk
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ParsedFileId = fileId,
            ParsedClassId = classId,
            ParsedMethodId = methodId,
            ChunkType = chunkType,
            Content = content,
            TokenCount = content.Length / 4, // Simple character heuristic
            Hash = hash,
            Metadata = meta
        };
    }

    private string ComputeSha256Hash(string text)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(text);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
