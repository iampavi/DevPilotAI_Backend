using System;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Shared.Common;

namespace DevPilotAI.Application.Common.Interfaces;

public interface IGitRepositoryService
{
    Task<Result> CloneRepositoryAsync(
        string repositoryUrl,
        string branch,
        string? personalAccessToken,
        string destinationPath,
        Action<int> onProgress,
        CancellationToken cancellationToken = default);
}
