using System;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Infrastructure.Services;

public class ImportProgressPublisher : IImportProgressPublisher
{
    private readonly ILogger<ImportProgressPublisher> _logger;

    public ImportProgressPublisher(ILogger<ImportProgressPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishProgressAsync(Guid jobId, Guid projectId, int progress, string status, string? error = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Job {JobId} Progress: Project {ProjectId} is {Status} ({Progress}%). Error: {Error}",
            jobId, projectId, status, progress, error ?? "None");
        
        return Task.CompletedTask;
    }
}
