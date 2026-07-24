using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;

namespace DevPilotAI.Infrastructure.Services;

public class ProjectChunkingQueue : IProjectChunkingQueue
{
    private readonly Channel<ChunkingJobItem> _channel;

    public ProjectChunkingQueue()
    {
        var options = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateUnbounded<ChunkingJobItem>(options);
    }

    public async ValueTask QueueChunkingJobAsync(ChunkingJobItem job)
    {
        await _channel.Writer.WriteAsync(job);
    }

    public async ValueTask<ChunkingJobItem> DequeueChunkingJobAsync(CancellationToken cancellationToken)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }
}
