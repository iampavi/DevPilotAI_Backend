using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Workspace> Workspaces { get; }
    DbSet<Project> Projects { get; }
    DbSet<ProjectSettings> ProjectSettings { get; }
    DbSet<ProjectStatistics> ProjectStatistics { get; }
    DbSet<ProjectIndex> ProjectIndexes { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    int SaveChanges();
}
