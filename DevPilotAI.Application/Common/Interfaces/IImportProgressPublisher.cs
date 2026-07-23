using System;
using System.Threading;
using System.Threading.Tasks;

namespace DevPilotAI.Application.Common.Interfaces;

public interface IImportProgressPublisher
{
    Task PublishProgressAsync(Guid jobId, Guid projectId, int progress, string status, string? error = null, CancellationToken cancellationToken = default);
}
