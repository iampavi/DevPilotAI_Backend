using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Shared.Common;

namespace DevPilotAI.Application.Common.Interfaces;

public interface IFileScanner
{
    Task<Result> ScanFileAsync(string filePath, CancellationToken cancellationToken = default);
}
