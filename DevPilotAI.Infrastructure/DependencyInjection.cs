using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using DevPilotAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddTransient<ApplicationDbContextInitializer>();

        return services;
    }
}
