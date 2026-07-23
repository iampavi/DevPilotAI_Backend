using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Services;

namespace DevPilotAI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddAutoMapper(cfg => cfg.AddMaps(assembly));
        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<IProjectService, ProjectService>();

        return services;
    }
}
