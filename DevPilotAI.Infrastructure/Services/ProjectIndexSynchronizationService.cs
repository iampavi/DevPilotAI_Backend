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
using Microsoft.Extensions.Configuration;

namespace DevPilotAI.Infrastructure.Services;

public class ProjectIndexSynchronizationService : IProjectIndexSynchronizationService
{
    private readonly IApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public ProjectIndexSynchronizationService(IApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task SynchronizeProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (_context is DbContext dbContext)
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await RunSyncInternalAsync(projectId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        else
        {
            await RunSyncInternalAsync(projectId, cancellationToken);
        }
    }

    private async Task RunSyncInternalAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .Include(p => p.Statistics)
            .Include(p => p.Index)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project == null) return;

        // Initialize structures if null
        if (project.Statistics == null)
        {
            project.Statistics = new ProjectStatistics { ProjectId = projectId };
            _context.ProjectStatistics.Add(project.Statistics);
        }

        if (project.Index == null)
        {
            project.Index = new ProjectIndex { ProjectId = projectId };
            _context.ProjectIndexes.Add(project.Index);
        }

        // 1. Gather files and classes from database metadata
        var files = await _context.ParsedFiles
            .Where(f => f.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        var classes = await _context.ParsedClasses
            .Include(c => c.ParsedFile)
            .Where(c => c.ParsedFile.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        int fileCount = files.Count;
        int classCount = classes.Count;

        // 2. Count distinct categories
        int controllerCount = classes.Count(c => c.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase));
        int serviceCount = classes.Count(c => c.Name.EndsWith("Service", StringComparison.OrdinalIgnoreCase));
        int repositoryCount = classes.Count(c => c.Name.EndsWith("Repository", StringComparison.OrdinalIgnoreCase));

        // 3. Count APIs (public methods inside controllers excluding constructors/ctor)
        int apiCount = 0;
        var controllerIds = classes
            .Where(c => c.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Id)
            .ToList();

        if (controllerIds.Any())
        {
            var apiMethods = await _context.ParsedMethods
                .Include(m => m.ParsedClass)
                .Where(m => controllerIds.Contains(m.ParsedClassId) && m.AccessModifier == "public" && m.Name != ".ctor")
                .ToListAsync(cancellationToken);

            apiCount = apiMethods.Count(m => m.Name != m.ParsedClass.Name);
        }

        // 4. Calculate total lines of code from disk (as lines count is not preserved in SQL tables)
        long totalLinesOfCode = 0;
        if (!string.IsNullOrEmpty(project.SourceLocation))
        {
            foreach (var file in files)
            {
                var fullPath = Path.Combine(project.SourceLocation, file.RelativePath);
                if (File.Exists(fullPath))
                {
                    try
                    {
                        var fileLines = await File.ReadAllLinesAsync(fullPath, cancellationToken);
                        totalLinesOfCode += fileLines.Length;
                    }
                    catch {}
                }
            }
        }

        // 5. Gather chunks count from database
        int chunkCount = await _context.CodeChunks
            .CountAsync(c => c.ProjectId == projectId, cancellationToken);

        // 6. Set stats
        project.Statistics.FileCount = fileCount;
        project.Statistics.ClassCount = classCount;
        project.Statistics.ControllerCount = controllerCount;
        project.Statistics.ServiceCount = serviceCount;
        project.Statistics.RepositoryCount = repositoryCount;
        project.Statistics.ApiCount = apiCount;
        project.Statistics.IndexedFileCount = fileCount;
        project.Statistics.TotalBytes = files.Sum(f => f.SizeInBytes);
        project.Statistics.TotalLinesOfCode = totalLinesOfCode;

        // 7. Set index details
        var syncTime = DateTime.UtcNow;
        project.Index.IndexStatus = IndexStatus.Indexed;
        project.Index.LastIndexedAt = syncTime;
        project.Index.ChunkCount = chunkCount;
        project.Index.EmbeddingCount = chunkCount;

        // Map max parser version to formatted string representation
        var maxParserVersion = files.Any() ? files.Max(f => f.ParserVersion).ToString("F1") : "1.0";
        project.Index.ParserVersion = maxParserVersion;
        project.Index.EmbeddingModel = _configuration["EmbeddingSettings:Model"] ?? "text-embedding-3-small";

        // 8. Update modification timestamps together
        project.LastModifiedAt = syncTime;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
