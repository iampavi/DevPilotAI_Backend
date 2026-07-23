using DevPilotAI.Application.DTOs.Identity;
using DevPilotAI.Shared.Common;

namespace DevPilotAI.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<Result<AuthResponseDto>> RegisterAsync(RegisterDto dto, string ipAddress, CancellationToken cancellationToken = default);
    Task<Result<AuthResponseDto>> LoginAsync(LoginDto dto, string ipAddress, CancellationToken cancellationToken = default);
    Task<Result<AuthResponseDto>> RefreshTokenAsync(string token, string ipAddress, CancellationToken cancellationToken = default);
    Task<Result> RevokeTokenAsync(string token, string ipAddress, CancellationToken cancellationToken = default);
}
