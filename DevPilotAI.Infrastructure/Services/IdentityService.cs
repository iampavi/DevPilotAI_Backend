using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Identity;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Entities.Identity;
using DevPilotAI.Shared.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace DevPilotAI.Infrastructure.Services;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IConfiguration _configuration;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<IdentityService> _logger;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IApplicationDbContext context,
        IMapper mapper,
        IConfiguration configuration,
        IDateTimeProvider dateTimeProvider,
        ILogger<IdentityService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _mapper = mapper;
        _configuration = configuration;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<AuthResponseDto>> RegisterAsync(RegisterDto dto, string ipAddress, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to register user with email: {Email}", dto.Email);

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            _logger.LogWarning("Registration failed. Email {Email} is already registered.", dto.Email);
            return Result.Failure<AuthResponseDto>(new Error("Identity.DuplicateEmail", "Email address is already registered."));
        }

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName
        };

        var createResult = await _userManager.CreateAsync(user, dto.Password);
        if (!createResult.Succeeded)
        {
            var firstError = createResult.Errors.FirstOrDefault();
            _logger.LogWarning("User registration failed on UserManager creation: {Error}", firstError?.Description);
            return Result.Failure<AuthResponseDto>(new Error("Identity.CreateFailed", firstError?.Description ?? "Failed to create user."));
        }

        // Assign default User role
        if (!await _roleManager.RoleExistsAsync("User"))
        {
            await _roleManager.CreateAsync(new ApplicationRole("User"));
        }
        await _userManager.AddToRoleAsync(user, "User");

        _logger.LogInformation("User created successfully: {UserId}", user.Id);

        // Generate initial tokens
        var roles = new[] { "User" };
        var jwtExpiryMinutes = int.TryParse(_configuration["JwtSettings:ExpiryInMinutes"], out var mins) ? mins : 15;
        var expiry = _dateTimeProvider.UtcNow.AddMinutes(jwtExpiryMinutes);
        var jwt = GenerateJwtToken(user, roles, expiry);

        var refreshToken = CreateRefreshToken(user.Id, ipAddress);
        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync(cancellationToken);

        var userDto = _mapper.Map<UserDto>(user);
        return Result.Success(new AuthResponseDto
        {
            AccessToken = jwt,
            RefreshToken = refreshToken.Token,
            ExpiresAt = expiry,
            User = userDto
        });
    }

    public async Task<Result<AuthResponseDto>> LoginAsync(LoginDto dto, string ipAddress, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting login for email: {Email}", dto.Email);

        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
        {
            _logger.LogWarning("Authentication failed for email: {Email}", dto.Email);
            return Result.Failure<AuthResponseDto>(new Error("Identity.InvalidCredentials", "Invalid email or password."));
        }

        // Update login audit timestamp
        user.LastLoginAt = _dateTimeProvider.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var jwtExpiryMinutes = int.TryParse(_configuration["JwtSettings:ExpiryInMinutes"], out var mins) ? mins : 15;
        var expiry = _dateTimeProvider.UtcNow.AddMinutes(jwtExpiryMinutes);
        var jwt = GenerateJwtToken(user, roles, expiry);

        var refreshToken = CreateRefreshToken(user.Id, ipAddress);
        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User logged in successfully: {UserId}", user.Id);

        var userDto = _mapper.Map<UserDto>(user);
        return Result.Success(new AuthResponseDto
        {
            AccessToken = jwt,
            RefreshToken = refreshToken.Token,
            ExpiresAt = expiry,
            User = userDto
        });
    }

    public async Task<Result<AuthResponseDto>> RefreshTokenAsync(string token, string ipAddress, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting token refresh operation.");

        var refreshToken = await _context.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token, cancellationToken);

        if (refreshToken == null)
        {
            _logger.LogWarning("Refresh token validation failed. Token not found.");
            return Result.Failure<AuthResponseDto>(new Error("Auth.InvalidToken", "Invalid refresh token."));
        }

        // Detect replay attacks (using an already revoked refresh token)
        if (refreshToken.IsRevoked)
        {
            _logger.LogWarning("Revoked refresh token reuse detected for user {UserId}. Revoking all active tokens.", refreshToken.UserId);
            var activeTokens = await _context.RefreshTokens
                .Where(t => t.UserId == refreshToken.UserId && t.Revoked == null && t.Expires > _dateTimeProvider.UtcNow)
                .ToListAsync(cancellationToken);

            foreach (var t in activeTokens)
            {
                t.Revoked = _dateTimeProvider.UtcNow;
                t.RevokedByIp = ipAddress;
                t.ReasonRevoked = $"Compromised sibling token reuse attempt: {refreshToken.Id}";
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthResponseDto>(new Error("Auth.TokenCompromised", "Refresh token was compromised and revoked. All active sessions have been terminated."));
        }

        if (refreshToken.IsExpired)
        {
            _logger.LogWarning("Refresh token validation failed. Token expired.");
            return Result.Failure<AuthResponseDto>(new Error("Auth.ExpiredToken", "Refresh token has expired."));
        }

        // Generate new access and rotated refresh token
        var newRefreshToken = CreateRefreshToken(refreshToken.UserId, ipAddress);

        // Mark old token as rotated
        refreshToken.Revoked = _dateTimeProvider.UtcNow;
        refreshToken.RevokedByIp = ipAddress;
        refreshToken.ReasonRevoked = "Replaced by token rotation";
        refreshToken.ReplacedByToken = newRefreshToken.Token;

        _context.RefreshTokens.Add(newRefreshToken);
        await _context.SaveChangesAsync(cancellationToken);

        var roles = await _userManager.GetRolesAsync(refreshToken.User);
        var jwtExpiryMinutes = int.TryParse(_configuration["JwtSettings:ExpiryInMinutes"], out var mins) ? mins : 15;
        var expiry = _dateTimeProvider.UtcNow.AddMinutes(jwtExpiryMinutes);
        var jwt = GenerateJwtToken(refreshToken.User, roles, expiry);

        _logger.LogInformation("Token refreshed successfully for user: {UserId}", refreshToken.UserId);

        var userDto = _mapper.Map<UserDto>(refreshToken.User);
        return Result.Success(new AuthResponseDto
        {
            AccessToken = jwt,
            RefreshToken = newRefreshToken.Token,
            ExpiresAt = expiry,
            User = userDto
        });
    }

    public async Task<Result> RevokeTokenAsync(string token, string ipAddress, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting token revocation.");

        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == token, cancellationToken);

        if (refreshToken == null || !refreshToken.IsActive)
        {
            _logger.LogWarning("Token revocation failed. Token not found or not active.");
            return Result.Failure(new Error("Auth.InvalidToken", "Token was not found or is already inactive."));
        }

        refreshToken.Revoked = _dateTimeProvider.UtcNow;
        refreshToken.RevokedByIp = ipAddress;
        refreshToken.ReasonRevoked = "Manually revoked by user";

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Token revoked successfully: {TokenId}", refreshToken.Id);

        return Result.Success();
    }

    private string GenerateJwtToken(ApplicationUser user, IList<string> roles, DateTime expiry)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var secret = _configuration["JwtSettings:Secret"];
        if (string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException("JWT Secret key configuration is missing.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["JwtSettings:Issuer"],
            audience: _configuration["JwtSettings:Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private RefreshToken CreateRefreshToken(Guid userId, string ipAddress)
    {
        var secureBytes = new byte[64];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(secureBytes);
        }
        var token = Convert.ToBase64String(secureBytes);

        var expiryDays = int.TryParse(_configuration["JwtSettings:RefreshTokenExpiryInDays"], out var days) ? days : 7;

        return new RefreshToken
        {
            UserId = userId,
            Token = token,
            Expires = _dateTimeProvider.UtcNow.AddDays(expiryDays),
            Created = _dateTimeProvider.UtcNow,
            CreatedByIp = ipAddress
        };
    }
}
