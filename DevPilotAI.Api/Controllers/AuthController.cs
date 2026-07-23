using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Identity;
using DevPilotAI.Shared.Common;
using Microsoft.AspNetCore.Mvc;

namespace DevPilotAI.Api.Controllers;

public class AuthController : ApiControllerBase
{
    private readonly IIdentityService _identityService;

    public AuthController(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Register([FromBody] RegisterDto dto, CancellationToken cancellationToken)
    {
        var ipAddress = GetIpAddress();
        var result = await _identityService.RegisterAsync(dto, ipAddress, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<AuthResponseDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<AuthResponseDto>.Success(result.Value, "User registered successfully."));
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login([FromBody] LoginDto dto, CancellationToken cancellationToken)
    {
        var ipAddress = GetIpAddress();
        var result = await _identityService.LoginAsync(dto, ipAddress, cancellationToken);

        if (result.IsFailure)
        {
            return Unauthorized(ApiResponse<AuthResponseDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<AuthResponseDto>.Success(result.Value, "Login successful."));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Refresh([FromBody] RefreshTokenDto dto, CancellationToken cancellationToken)
    {
        var ipAddress = GetIpAddress();
        var result = await _identityService.RefreshTokenAsync(dto.RefreshToken, ipAddress, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Auth.TokenCompromised")
            {
                return Conflict(ApiResponse<AuthResponseDto>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<AuthResponseDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<AuthResponseDto>.Success(result.Value, "Tokens refreshed successfully."));
    }

    [HttpPost("revoke")]
    public async Task<ActionResult<ApiResponse>> Revoke([FromBody] RefreshTokenDto dto, CancellationToken cancellationToken)
    {
        var ipAddress = GetIpAddress();
        var result = await _identityService.RevokeTokenAsync(dto.RefreshToken, ipAddress, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(ApiResponse.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse.Success("Token revoked successfully."));
    }

    private string GetIpAddress()
    {
        if (Request.Headers.ContainsKey("X-Forwarded-For"))
        {
            return Request.Headers["X-Forwarded-For"]!;
        }
        return HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1";
    }
}
