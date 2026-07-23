using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using DevPilotAI.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Infrastructure.Services;

public class ProjectImportBackgroundWorker : BackgroundService
{
    private readonly IProjectImportQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly IFileStorageService _storageService;
    private readonly IFileScanner _fileScanner;
    private readonly ILogger<ProjectImportBackgroundWorker> _logger;

    public ProjectImportBackgroundWorker(
        IProjectImportQueue queue,
        IServiceProvider serviceProvider,
        IFileStorageService storageService,
        IFileScanner fileScanner,
        ILogger<ProjectImportBackgroundWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _storageService = storageService;
        _fileScanner = fileScanner;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Project Import Background Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobItem = await _queue.DequeueImportJobAsync(stoppingToken);
                _logger.LogInformation("Processing import job {JobId} for project {ProjectId}.", jobItem.JobId, jobItem.ProjectId);

                await ProcessJobAsync(jobItem, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing project import worker.");
            }
        }
    }

    private async Task ProcessJobAsync(ImportJobItem jobItem, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var gitService = scope.ServiceProvider.GetRequiredService<IGitRepositoryService>();
        var progressPublisher = scope.ServiceProvider.GetRequiredService<IImportProgressPublisher>();

        var jobEntity = await context.ProjectImportJobs.FirstOrDefaultAsync(j => j.Id == jobItem.JobId, cancellationToken);
        var projectEntity = await context.Projects.FirstOrDefaultAsync(p => p.Id == jobItem.ProjectId, cancellationToken);

        if (jobEntity == null || projectEntity == null)
        {
            _logger.LogError("Job {JobId} or Project {ProjectId} not found in database. Aborting.", jobItem.JobId, jobItem.ProjectId);
            return;
        }

        try
        {
            // 1. Update status to Running
            jobEntity.Status = JobStatus.Running;
            jobEntity.StartedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            await progressPublisher.PublishProgressAsync(jobItem.JobId, jobItem.ProjectId, 0, "Running", null, cancellationToken);

            string finalLocation = "";

            if (jobItem.ImportType == ImportType.ZipUpload)
            {
                // Extract ZIP Archive
                finalLocation = _storageService.GetAbsoluteLocation("Extracted", jobItem.ProjectId.ToString());
                var uploadedZipPath = _storageService.GetAbsoluteLocation("Uploads", jobItem.FilePath!);

                if (!File.Exists(uploadedZipPath))
                {
                    throw new FileNotFoundException("Uploaded zip file not found.", uploadedZipPath);
                }

                // Scan ZIP before extraction
                var scanResult = await _fileScanner.ScanFileAsync(uploadedZipPath, cancellationToken);
                if (scanResult.IsFailure)
                {
                    throw new InvalidOperationException($"File blocked by security scan: {scanResult.Error.Message}");
                }

                // Safe Zip extraction
                using (var archive = ZipFile.OpenRead(uploadedZipPath))
                {
                    int totalEntries = archive.Entries.Count;
                    int currentEntry = 0;

                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue; // skip folders

                        var destinationPath = Path.GetFullPath(Path.Combine(finalLocation, entry.FullName));
                        if (!destinationPath.StartsWith(finalLocation, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException("Zip Slip directory traversal attempt detected!");
                        }

                        var parentDir = Path.GetDirectoryName(destinationPath);
                        if (parentDir != null && !Directory.Exists(parentDir))
                        {
                            Directory.CreateDirectory(parentDir);
                        }

                        entry.ExtractToFile(destinationPath, overwrite: true);
                        currentEntry++;

                        var progressPercent = (int)((double)currentEntry / totalEntries * 100);
                        
                        // Throttled update to DB and publisher
                        jobEntity.Progress = progressPercent;
                        await progressPublisher.PublishProgressAsync(jobItem.JobId, jobItem.ProjectId, progressPercent, "Extracting", null, cancellationToken);
                    }
                }

                // Cleanup ZIP file after extraction
                try
                {
                    File.Delete(uploadedZipPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary upload ZIP at {Path}", uploadedZipPath);
                }
            }
            else if (jobItem.ImportType == ImportType.GitRepository)
            {
                // Clone Git Repository
                finalLocation = _storageService.GetAbsoluteLocation("Repositories", jobItem.ProjectId.ToString());

                var cloneResult = await gitService.CloneRepositoryAsync(
                    jobItem.GitUrl!,
                    jobItem.GitBranch ?? "main",
                    jobItem.PersonalAccessToken,
                    finalLocation,
                    progressPercent =>
                    {
                        jobEntity.Progress = progressPercent;
                        // Fire progress updates (avoid blocking DB save updates inside progress actions)
                        progressPublisher.PublishProgressAsync(jobItem.JobId, jobItem.ProjectId, progressPercent, "Downloading", null, cancellationToken).GetAwaiter().GetResult();
                    },
                    cancellationToken
                );

                if (cloneResult.IsFailure)
                {
                    throw new InvalidOperationException(cloneResult.Error.Message);
                }
            }
            else
            {
                throw new NotSupportedException($"ImportType {jobItem.ImportType} is not supported by the background processor.");
            }

            // 2. Completed Successfully
            jobEntity.Status = JobStatus.Completed;
            jobEntity.Progress = 100;
            jobEntity.CompletedAt = DateTime.UtcNow;

            projectEntity.SourceLocation = finalLocation;

            await context.SaveChangesAsync(cancellationToken);
            await progressPublisher.PublishProgressAsync(jobItem.JobId, jobItem.ProjectId, 100, "Completed", null, cancellationToken);
            
            _logger.LogInformation("Job {JobId} completed successfully for Project {ProjectId}.", jobItem.JobId, jobItem.ProjectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed for Project {ProjectId}.", jobItem.JobId, jobItem.ProjectId);

            jobEntity.Status = JobStatus.Failed;
            jobEntity.CompletedAt = DateTime.UtcNow;
            jobEntity.Error = ex.Message;

            await context.SaveChangesAsync(CancellationToken.None);
            await progressPublisher.PublishProgressAsync(jobItem.JobId, jobItem.ProjectId, jobEntity.Progress, "Failed", ex.Message, CancellationToken.None);
        }
    }
}
