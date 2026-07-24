using System;
using System.Threading;
using System.Threading.Tasks;

namespace DevPilotAI.Application.Common.Interfaces;

public record ChunkingJobItem(
    Guid JobId,
    Guid ProjectId
);

public interface IProjectChunkingQueue
{
    ValueTask QueueChunkingJobAsync(ChunkingJobItem job);
    ValueTask<ChunkingJobItem> DequeueChunkingJobAsync(CancellationToken cancellationToken);
}
