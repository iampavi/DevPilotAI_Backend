using System;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Infrastructure.Services;

public class NoOpChunkingScheduler : IChunkingScheduler
{
    private readonly ILogger<NoOpChunkingScheduler> _logger;

    public NoOpChunkingScheduler(ILogger<NoOpChunkingScheduler> logger)
    {
        _logger = logger;
    }

    public Task QueueChunkingJobAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("NoOpChunkingScheduler: Complete hook triggered for project {ProjectId}.", projectId);
        return Task.CompletedTask;
    }
}
