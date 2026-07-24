using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;

namespace DevPilotAI.Infrastructure.Services;

public class ProjectParseQueue : IProjectParseQueue
{
    private readonly Channel<ParseJobItem> _channel;

    public ProjectParseQueue()
    {
        var options = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateUnbounded<ParseJobItem>(options);
    }

    public async ValueTask QueueParseJobAsync(ParseJobItem job)
    {
        await _channel.Writer.WriteAsync(job);
    }

    public async ValueTask<ParseJobItem> DequeueParseJobAsync(CancellationToken cancellationToken)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }
}
