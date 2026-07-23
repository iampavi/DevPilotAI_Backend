using DevPilotAI.Application.Common.Interfaces;

namespace DevPilotAI.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    public string? UserId => "System";
}
