using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace DevPilotAI.Infrastructure.Services;

public class ProjectParseBackgroundWorker : BackgroundService
{
    private readonly IProjectParseQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProjectParseBackgroundWorker> _logger;

    public ProjectParseBackgroundWorker(
        IProjectParseQueue queue,
        IServiceProvider serviceProvider,
        ILogger<ProjectParseBackgroundWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Project Parse Background Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobItem = await _queue.DequeueParseJobAsync(stoppingToken);
                _logger.LogInformation("Processing parse job {JobId} for project {ProjectId}.", jobItem.JobId, jobItem.ProjectId);

                await ProcessJobAsync(jobItem, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing project parse background worker.");
            }
        }
    }

    private async Task ProcessJobAsync(ParseJobItem jobItem, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var parser = scope.ServiceProvider.GetRequiredService<ICSharpParser>();
        var chunkingScheduler = scope.ServiceProvider.GetRequiredService<IChunkingScheduler>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var jobEntity = await context.ProjectParseJobs.FirstOrDefaultAsync(j => j.Id == jobItem.JobId, cancellationToken);
        if (jobEntity == null)
        {
            _logger.LogError("Parse job {JobId} not found in database. Aborting.", jobItem.JobId);
            return;
        }

        try
        {
            // 1. Update status to Running
            jobEntity.Status = JobStatus.Running;
            jobEntity.StartedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            // 2. Clear old parsed metadata for this project (cascades down to classes, methods, properties, and fields)
            var oldFiles = await context.ParsedFiles
                .Where(f => f.ProjectId == jobItem.ProjectId)
                .ToListAsync(cancellationToken);
            context.ParsedFiles.RemoveRange(oldFiles);
            await context.SaveChangesAsync(cancellationToken);

            // 3. Scan codebase directory
            if (!Directory.Exists(jobItem.SourceLocation))
            {
                throw new DirectoryNotFoundException($"Project source directory '{jobItem.SourceLocation}' was not found.");
            }

            var ignoredDirs = configuration.GetSection("RetrievalSettings:IgnoredDirectories").Get<List<string>>() 
                ?? new List<string> { "bin", "obj", "node_modules", ".git", "vendor", "dist", "build", "coverage", ".vs", ".idea", ".vscode" };
            var ignoredFiles = configuration.GetSection("RetrievalSettings:IgnoredFiles").Get<List<string>>() 
                ?? new List<string> { "package-lock.json", "yarn.lock", "pnpm-lock.yaml", "composer.lock", "packages.lock.json" };

            var matchedFiles = GetProjectFiles(jobItem.SourceLocation, ignoredDirs, ignoredFiles);
            int totalFiles = matchedFiles.Count;

            _logger.LogInformation("Scanned project {ProjectId} directory. Found {Count} matching files to parse.", jobItem.ProjectId, totalFiles);

            if (totalFiles == 0)
            {
                // Edge case: no files to parse
                jobEntity.Status = JobStatus.Completed;
                jobEntity.Progress = 100;
                jobEntity.CompletedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
                
                await chunkingScheduler.QueueChunkingJobAsync(jobItem.ProjectId, cancellationToken);
                return;
            }

            // 4. Process each file
            int currentIndex = 0;
            foreach (var filePath in matchedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(jobItem.SourceLocation, filePath);
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                var language = MapExtensionToLanguage(ext);
                var size = new FileInfo(filePath).Length;

                var parsedFile = new ParsedFile
                {
                    ProjectId = jobItem.ProjectId,
                    RelativePath = relativePath,
                    Language = language,
                    SizeInBytes = size,
                    ParserVersion = 1
                };

                // Deep parse syntax tree if file is C#
                if (language == "C#")
                {
                    var sourceCode = await File.ReadAllTextAsync(filePath, cancellationToken);
                    var parseResult = parser.ParseContent(sourceCode);
                    if (parseResult.IsSuccess)
                    {
                        parsedFile.Usings = parseResult.Value.Usings;

                        foreach (var classData in parseResult.Value.Classes)
                        {
                            var parsedClass = new ParsedClass
                            {
                                Name = classData.Name,
                                FullName = classData.FullName,
                                Namespace = classData.Namespace,
                                SymbolType = MapSymbolType(classData.SymbolType),
                                BaseTypes = classData.BaseTypes,
                                Attributes = classData.Attributes,
                                StartLine = classData.StartLine,
                                EndLine = classData.EndLine
                            };

                            // Add Methods
                            foreach (var m in classData.Methods)
                            {
                                parsedClass.Methods.Add(new ParsedMethod
                                {
                                    Name = m.Name,
                                    ReturnType = m.ReturnType,
                                    AccessModifier = m.AccessModifier,
                                    Parameters = m.Parameters,
                                    Attributes = m.Attributes,
                                    StartLine = m.StartLine,
                                    EndLine = m.EndLine
                                });
                            }

                            // Add Properties
                            foreach (var p in classData.Properties)
                            {
                                parsedClass.Properties.Add(new ParsedProperty
                                {
                                    Name = p.Name,
                                    Type = p.Type,
                                    AccessModifier = p.AccessModifier,
                                    Attributes = p.Attributes,
                                    StartLine = p.StartLine,
                                    EndLine = p.EndLine
                                });
                            }

                            // Add Fields
                            foreach (var f in classData.Fields)
                            {
                                parsedClass.Fields.Add(new ParsedField
                                {
                                    Name = f.Name,
                                    Type = f.Type,
                                    AccessModifier = f.AccessModifier,
                                    Attributes = f.Attributes,
                                    StartLine = f.StartLine,
                                    EndLine = f.EndLine
                                });
                            }

                            parsedFile.Classes.Add(parsedClass);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Failed to parse syntax tree for file {Path}: {Error}", relativePath, parseResult.Error.Message);
                    }
                }

                context.ParsedFiles.Add(parsedFile);
                currentIndex++;

                // Throttled progress reporting
                var progressPercent = (int)((double)currentIndex / totalFiles * 100);
                jobEntity.Progress = progressPercent;
                await context.SaveChangesAsync(cancellationToken);
            }

            // 5. Save and trigger next step (Chunking Scheduler)
            jobEntity.Status = JobStatus.Completed;
            jobEntity.Progress = 100;
            jobEntity.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Parse job {JobId} completed successfully. Invoking downstream chunking hook.", jobItem.JobId);
            await chunkingScheduler.QueueChunkingJobAsync(jobItem.ProjectId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parse job {JobId} failed.", jobItem.JobId);

            jobEntity.Status = JobStatus.Failed;
            jobEntity.CompletedAt = DateTime.UtcNow;
            jobEntity.Error = ex.Message;

            await context.SaveChangesAsync(CancellationToken.None);
        }
    }

    private List<string> GetProjectFiles(string basePath, List<string> ignoredDirs, List<string> ignoredFiles)
    {
        var result = new List<string>();
        var ignoreDirsSet = new HashSet<string>(ignoredDirs, StringComparer.OrdinalIgnoreCase);
        var ignoreFilesSet = new HashSet<string>(ignoredFiles, StringComparer.OrdinalIgnoreCase);
        ScanDirectory(basePath, ignoreDirsSet, ignoreFilesSet, result);
        return result;
    }

    private void ScanDirectory(string currentDir, HashSet<string> ignoreDirs, HashSet<string> ignoreFiles, List<string> result)
    {
        var dirInfo = new DirectoryInfo(currentDir);
        if (ignoreDirs.Contains(dirInfo.Name))
        {
            return;
        }

        try
        {
            foreach (var file in Directory.GetFiles(currentDir))
            {
                var fileName = Path.GetFileName(file);
                var fileNameLower = fileName.ToLowerInvariant();
                if (ignoreFiles.Contains(fileName) || IsIgnoredFile(fileNameLower))
                {
                    continue;
                }

                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (IsSupportedExtension(ext))
                {
                    result.Add(file);
                }
            }

            foreach (var subDir in Directory.GetDirectories(currentDir))
            {
                ScanDirectory(subDir, ignoreDirs, ignoreFiles, result);
            }
        }
        catch (Exception)
        {
            // Skip inaccessible paths
        }
    }

    private bool IsIgnoredFile(string fileNameLower)
    {
        return fileNameLower.EndsWith(".min.js") ||
               fileNameLower.EndsWith(".min.css") ||
               fileNameLower.EndsWith(".map") ||
               fileNameLower.EndsWith(".dll") ||
               fileNameLower.EndsWith(".exe") ||
               fileNameLower.EndsWith(".pdb") ||
               fileNameLower.EndsWith(".cache") ||
               fileNameLower.EndsWith(".log");
    }

    private bool IsSupportedExtension(string ext)
    {
        return ext == ".cs" || ext == ".js" || ext == ".jsx" || ext == ".ts" || ext == ".tsx" 
            || ext == ".json" || ext == ".xml" || ext == ".md" || ext == ".css" || ext == ".html" || ext == ".sql";
    }

    private string MapExtensionToLanguage(string ext)
    {
        return ext switch
        {
            ".cs" => "C#",
            ".js" or ".jsx" => "JavaScript",
            ".ts" or ".tsx" => "TypeScript",
            ".json" => "JSON",
            ".xml" => "XML",
            ".md" => "Markdown",
            ".css" => "CSS",
            ".html" => "HTML",
            ".sql" => "SQL",
            _ => "Unknown"
        };
    }

    private SymbolType MapSymbolType(string rawType)
    {
        return rawType switch
        {
            "Class" => SymbolType.Class,
            "Interface" => SymbolType.Interface,
            "Record" => SymbolType.Record,
            "Struct" => SymbolType.Struct,
            "Enum" => SymbolType.Enum,
            _ => SymbolType.Class
        };
    }
}
