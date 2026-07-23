using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace DevPilotAI.Infrastructure.Services;

public class LocalStorageService : IFileStorageService
{
    private readonly string _baseDirectory;

    public LocalStorageService(IConfiguration configuration)
    {
        var configuredPath = configuration["StorageSettings:BaseDirectory"];
        _baseDirectory = !string.IsNullOrEmpty(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Storage");
    }

    public async Task SaveFileAsync(string folder, string relativePath, Stream stream, CancellationToken cancellationToken = default)
    {
        var targetDir = Path.Combine(_baseDirectory, folder);
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var filePath = Path.Combine(targetDir, relativePath);
        
        // Ensure parent directories exist
        var parentDir = Path.GetDirectoryName(filePath);
        if (parentDir != null && !Directory.Exists(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await stream.CopyToAsync(fileStream, cancellationToken);
    }

    public Task DeleteDirectoryAsync(string folder, string relativePath, CancellationToken cancellationToken = default)
    {
        var targetDir = Path.Combine(_baseDirectory, folder, relativePath);
        if (Directory.Exists(targetDir))
        {
            Directory.Delete(targetDir, recursive: true);
        }
        return Task.CompletedTask;
    }

    public Task<bool> DirectoryExistsAsync(string folder, string relativePath, CancellationToken cancellationToken = default)
    {
        var targetDir = Path.Combine(_baseDirectory, folder, relativePath);
        return Task.FromResult(Directory.Exists(targetDir));
    }

    public string GetAbsoluteLocation(string folder, string relativePath)
    {
        return Path.GetFullPath(Path.Combine(_baseDirectory, folder, relativePath));
    }
}
