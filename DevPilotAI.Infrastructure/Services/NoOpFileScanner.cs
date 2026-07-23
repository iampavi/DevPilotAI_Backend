using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Shared.Common;

namespace DevPilotAI.Infrastructure.Services;

public class NoOpFileScanner : IFileScanner
{
    public Task<Result> ScanFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // Default NoOp file scanner always returns Success
        return Task.FromResult(Result.Success());
    }
}
