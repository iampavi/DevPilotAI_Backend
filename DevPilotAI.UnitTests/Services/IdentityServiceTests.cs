using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Common.Mappings;
using DevPilotAI.Application.DTOs.Identity;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Entities.Identity;
using DevPilotAI.Infrastructure.Persistence;
using DevPilotAI.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevPilotAI.UnitTests.Services;

public class IdentityServiceTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IIdentityService _service;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly DateTime _utcNow = new(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);

    public IdentityServiceTests()
    {
        var services = new ServiceCollection();
        
        services.AddLogging();
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "JwtSettings:Secret", "DevelopmentSuperSecretKeyForLocalTestingDevPilotAI123!" },
                { "JwtSettings:Issuer", "DevPilotAI" },
                { "JwtSettings:Audience", "DevPilotAI_Client" },
                { "JwtSettings:ExpiryInMinutes", "15" },
                { "JwtSettings:RefreshTokenExpiryInDays", "7" }
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(_utcNow);
        services.AddSingleton<IDateTimeProvider>(_dateTimeProviderMock.Object);

        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(u => u.UserId).Returns(Guid.NewGuid().ToString());
        services.AddSingleton<ICurrentUserService>(_currentUserServiceMock.Object);

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer("Server=localhost;Database=DevPilotAI_IdentityServiceTests;Trusted_Connection=True;TrustServerCertificate=True;"));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequiredLength = 6;
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddAutoMapper(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
        });

        services.AddScoped<IIdentityService, IdentityService>();

        _provider = services.BuildServiceProvider();

        _context = _provider.GetRequiredService<ApplicationDbContext>();
        _userManager = _provider.GetRequiredService<UserManager<ApplicationUser>>();
        _roleManager = _provider.GetRequiredService<RoleManager<ApplicationRole>>();
        _service = _provider.GetRequiredService<IIdentityService>();

        _context.Database.EnsureDeleted();
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task RegisterAsync_ShouldCreateUserAndAssignUserRole_WhenRequestIsValid()
    {
        // Arrange
        var dto = new RegisterDto
        {
            Email = "test@devpilot.ai",
            Password = "Password123!",
            FirstName = "Test",
            LastName = "User"
        };

        // Act
        var result = await _service.RegisterAsync(dto, "127.0.0.1");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.AccessToken);
        Assert.NotNull(result.Value.RefreshToken);
        Assert.Equal("test@devpilot.ai", result.Value.User.Email);

        var dbUser = await _userManager.FindByEmailAsync("test@devpilot.ai");
        Assert.NotNull(dbUser);
        Assert.Equal("Test", dbUser.FirstName);

        var roles = await _userManager.GetRolesAsync(dbUser);
        Assert.Contains("User", roles);
    }

    [Fact]
    public async Task LoginAsync_ShouldVerifyCredentialsAndAuditLogin_WhenValid()
    {
        // Arrange
        var user = new ApplicationUser { UserName = "login@devpilot.ai", Email = "login@devpilot.ai", FirstName = "Login", LastName = "User" };
        await _userManager.CreateAsync(user, "Password123!");
        
        var dto = new LoginDto { Email = "login@devpilot.ai", Password = "Password123!" };

        // Act
        var result = await _service.LoginAsync(dto, "127.0.0.1");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.AccessToken);
        
        var dbUser = await _userManager.FindByEmailAsync("login@devpilot.ai");
        Assert.Equal(_utcNow, dbUser!.LastLoginAt);
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldRotateTokens_WhenTokenIsActive()
    {
        // Arrange
        var user = new ApplicationUser { UserName = "refresh@devpilot.ai", Email = "refresh@devpilot.ai", FirstName = "Refresh", LastName = "User" };
        await _userManager.CreateAsync(user, "Password123!");

        var activeToken = new RefreshToken
        {
            UserId = user.Id,
            Token = "ActiveTokenString",
            Created = _utcNow,
            CreatedByIp = "127.0.0.1",
            Expires = _utcNow.AddDays(7)
        };
        _context.RefreshTokens.Add(activeToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RefreshTokenAsync("ActiveTokenString", "127.0.0.2");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual("ActiveTokenString", result.Value.RefreshToken);

        var oldToken = await _context.RefreshTokens.IgnoreQueryFilters().FirstAsync(t => t.Token == "ActiveTokenString");
        Assert.True(oldToken.IsRevoked);
        Assert.Equal("Replaced by token rotation", oldToken.ReasonRevoked);
        Assert.Equal(result.Value.RefreshToken, oldToken.ReplacedByToken);

        var newToken = await _context.RefreshTokens.FirstAsync(t => t.Token == result.Value.RefreshToken);
        Assert.True(newToken.IsActive);
        Assert.Equal("127.0.0.2", newToken.CreatedByIp);
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldRevokeAllSessions_WhenTokenIsAlreadyRevoked()
    {
        // Arrange
        var user = new ApplicationUser { UserName = "compromise@devpilot.ai", Email = "compromise@devpilot.ai", FirstName = "Compromise", LastName = "User" };
        await _userManager.CreateAsync(user, "Password123!");

        var revokedToken = new RefreshToken
        {
            UserId = user.Id,
            Token = "RevokedTokenString",
            Created = _utcNow,
            CreatedByIp = "127.0.0.1",
            Expires = _utcNow.AddDays(7),
            Revoked = _utcNow.AddHours(-1),
            ReasonRevoked = "Rotated"
        };
        
        var siblingToken = new RefreshToken
        {
            UserId = user.Id,
            Token = "ActiveSiblingToken",
            Created = _utcNow,
            CreatedByIp = "127.0.0.1",
            Expires = _utcNow.AddDays(7)
        };

        _context.RefreshTokens.AddRange(revokedToken, siblingToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RefreshTokenAsync("RevokedTokenString", "127.0.0.2");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Auth.TokenCompromised", result.Error.Code);

        // Verify sibling token was automatically revoked
        var dbSiblingToken = await _context.RefreshTokens.IgnoreQueryFilters().FirstAsync(t => t.Token == "ActiveSiblingToken");
        Assert.True(dbSiblingToken.IsRevoked);
        Assert.Equal("Compromised sibling token reuse attempt: " + revokedToken.Id, dbSiblingToken.ReasonRevoked);
    }

    [Fact]
    public async Task RevokeTokenAsync_ShouldMarkTokenAsRevoked()
    {
        // Arrange
        var user = new ApplicationUser { UserName = "revoke@devpilot.ai", Email = "revoke@devpilot.ai", FirstName = "Revoke", LastName = "User" };
        await _userManager.CreateAsync(user, "Password123!");

        var activeToken = new RefreshToken
        {
            UserId = user.Id,
            Token = "TokenToRevoke",
            Created = _utcNow,
            Expires = _utcNow.AddDays(7)
        };
        _context.RefreshTokens.Add(activeToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RevokeTokenAsync("TokenToRevoke", "127.0.0.1");

        // Assert
        Assert.True(result.IsSuccess);
        
        var dbToken = await _context.RefreshTokens.IgnoreQueryFilters().FirstAsync(t => t.Token == "TokenToRevoke");
        Assert.True(dbToken.IsRevoked);
        Assert.Equal("Manually revoked by user", dbToken.ReasonRevoked);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _provider.Dispose();
    }
}
