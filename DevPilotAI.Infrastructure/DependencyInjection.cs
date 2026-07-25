using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Infrastructure.Services;
using DevPilotAI.Infrastructure.Services.EmbeddingProviders;
using DevPilotAI.Infrastructure.Services.ChatProviders;
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
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
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
        services.AddScoped<IChunkingScheduler, ChunkingScheduler>();
        services.AddSingleton<IProjectParseQueue, ProjectParseQueue>();
        services.AddHostedService<ProjectParseBackgroundWorker>();

        // Embedding Providers (Isolated via Typed HttpClients)
        services.AddHttpClient<IEmbeddingProvider, OpenAIEmbeddingProvider>();
        services.AddHttpClient<IEmbeddingProvider, AzureOpenAIEmbeddingProvider>();
        services.AddHttpClient<IEmbeddingProvider, OllamaEmbeddingProvider>();
        services.AddScoped<IEmbeddingProvider, MockEmbeddingProvider>();

        // Embedding, Qdrant, Semantic Search services and Background Worker
        services.AddScoped<IEmbeddingService, EmbeddingService>();
        services.AddSingleton<IQdrantService, QdrantService>();
        services.AddScoped<ISemanticSearchService, SemanticSearchService>();
        services.AddSingleton<IProjectChunkingQueue, ProjectChunkingQueue>();
        services.AddHostedService<ProjectChunkingBackgroundWorker>();

        // Chat Providers (Isolated via Typed HttpClients)
        services.AddHttpClient<IChatProvider, OpenAIChatProvider>();
        services.AddHttpClient<IChatProvider, AzureOpenAIChatProvider>();
        services.AddHttpClient<IChatProvider, OllamaChatProvider>();
        services.AddScoped<IChatProvider, MockChatProvider>();

        // RAG Search & AI Chat services
        services.AddScoped<IChatProviderFactory, ChatProviderFactory>();
        services.AddScoped<ISemanticRetrievalService, SemanticRetrievalService>();
        services.AddScoped<IPromptBuilder, PromptBuilder>();
        services.AddScoped<IAiChatService, AiChatService>();

        return services;
    }
}
