using System;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Domain.Enums;

namespace DevPilotAI.Application.Common.Interfaces;

public record ImportJobItem(
    Guid JobId,
    Guid ProjectId,
    ImportType ImportType,
    string? FilePath = null,            // For ZIP uploads
    string? GitUrl = null,              // For Git repos
    string? GitBranch = null,           // For Git repos
    string? PersonalAccessToken = null  // For Git repos (discarded immediately after clone)
);

public interface IProjectImportQueue
{
    ValueTask QueueImportJobAsync(ImportJobItem job);
    ValueTask<ImportJobItem> DequeueImportJobAsync(CancellationToken cancellationToken);
}
