using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DevPilotAI.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task SaveFileAsync(string folder, string relativePath, Stream stream, CancellationToken cancellationToken = default);
    Task DeleteDirectoryAsync(string folder, string relativePath, CancellationToken cancellationToken = default);
    Task<bool> DirectoryExistsAsync(string folder, string relativePath, CancellationToken cancellationToken = default);
    string GetAbsoluteLocation(string folder, string relativePath);
}
