using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using DevPilotAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using System.Text;
using DevPilotAI.Domain.Entities.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace DevPilotAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddTransient<ApplicationDbContextInitializer>();

        // Register Identity
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>();

        // Register JWT Authentication
        var jwtSettings = configuration.GetSection("JwtSettings");
        var secret = jwtSettings["Secret"] ?? "TemporaryDevelopmentSecretKeyForMigrationsOnly123!";
        var key = Encoding.UTF8.GetBytes(secret);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"] ?? "DevPilotAI",
                ValidAudience = jwtSettings["Audience"] ?? "DevPilotAI_Client",
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero
            };
        });

        // Register Authorization Policies
        services.AddAuthorization(options =>
        {
            options.AddPolicy("CanManageWorkspace", policy => policy.RequireAuthenticatedUser());
            options.AddPolicy("CanManageProject", policy => policy.RequireAuthenticatedUser());
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
        });

        services.AddScoped<IIdentityService, IdentityService>();

        // Ingestion & Storage services
        services.AddSingleton<IFileStorageService, LocalStorageService>();
        services.AddSingleton<IFileScanner, NoOpFileScanner>();
        services.AddScoped<IGitRepositoryService, GitRepositoryService>();
        services.AddScoped<IImportProgressPublisher, ImportProgressPublisher>();
        services.AddSingleton<IProjectImportQueue, ProjectImportQueue>();
        services.AddHostedService<ProjectImportBackgroundWorker>();

        // Parsing & Chunking Hook services
        services.AddSingleton<ICSharpParser, RoslynCSharpParser>();
        services.AddScoped<IChunkingScheduler, NoOpChunkingScheduler>();
        services.AddSingleton<IProjectParseQueue, ProjectParseQueue>();
        services.AddHostedService<ProjectParseBackgroundWorker>();

        return services;
    }
}
