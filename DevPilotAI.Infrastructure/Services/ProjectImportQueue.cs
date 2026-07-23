using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;

namespace DevPilotAI.Infrastructure.Services;

public class ProjectImportQueue : IProjectImportQueue
{
    private readonly Channel<ImportJobItem> _channel;

    public ProjectImportQueue()
    {
        var options = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateUnbounded<ImportJobItem>(options);
    }

    public async ValueTask QueueImportJobAsync(ImportJobItem job)
    {
        await _channel.Writer.WriteAsync(job);
    }

    public async ValueTask<ImportJobItem> DequeueImportJobAsync(CancellationToken cancellationToken)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }
}
