using System;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Infrastructure.Services;

public class ChunkingScheduler : IChunkingScheduler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IProjectChunkingQueue _chunkingQueue;
    private readonly ILogger<ChunkingScheduler> _logger;

    public ChunkingScheduler(
        IServiceProvider serviceProvider,
        IProjectChunkingQueue chunkingQueue,
        ILogger<ChunkingScheduler> logger)
    {
        _serviceProvider = serviceProvider;
        _chunkingQueue = chunkingQueue;
        _logger = logger;
    }

    public async Task QueueChunkingJobAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("ChunkingScheduler: Parse complete hook triggered for project {ProjectId}. Scheduling chunking run.", projectId);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var jobId = Guid.NewGuid();
        var job = new ProjectChunkingJob
        {
            Id = jobId,
            ProjectId = projectId,
            Status = JobStatus.Pending,
            Progress = 0,
            StartedAt = DateTime.UtcNow
        };

        context.ProjectChunkingJobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);

        await _chunkingQueue.QueueChunkingJobAsync(new ChunkingJobItem(jobId, projectId));
    }
}
