using System;
using System.Threading;
using System.Threading.Tasks;

namespace DevPilotAI.Application.Common.Interfaces;

public record ParseJobItem(
    Guid JobId,
    Guid ProjectId,
    string SourceLocation
);

public interface IProjectParseQueue
{
    ValueTask QueueParseJobAsync(ParseJobItem job);
    ValueTask<ParseJobItem> DequeueParseJobAsync(CancellationToken cancellationToken);
}
