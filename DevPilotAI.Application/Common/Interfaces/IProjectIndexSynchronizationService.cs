using System;
using System.Threading;
using System.Threading.Tasks;

namespace DevPilotAI.Application.Common.Interfaces;

public interface IProjectIndexSynchronizationService
{
    Task SynchronizeProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
