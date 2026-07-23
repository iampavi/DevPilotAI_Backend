using DevPilotAI.Application.Common.Interfaces;

namespace DevPilotAI.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
