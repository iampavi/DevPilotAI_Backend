using System;
using System.Threading;
using System.Threading.Tasks;

namespace DevPilotAI.Application.Common.Interfaces;

public interface IChunkingScheduler
{
    Task QueueChunkingJobAsync(Guid projectId, CancellationToken cancellationToken = default);
}
