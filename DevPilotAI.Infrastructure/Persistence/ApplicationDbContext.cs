using System.Reflection;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Common;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using DevPilotAI.Domain.Entities.Identity;

namespace DevPilotAI.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICurrentUserService _currentUserService;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IDateTimeProvider dateTimeProvider,
        ICurrentUserService currentUserService) : base(options)
    {
        _dateTimeProvider = dateTimeProvider;
        _currentUserService = currentUserService;
    }

    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectSettings> ProjectSettings => Set<ProjectSettings>();
    public DbSet<ProjectStatistics> ProjectStatistics => Set<ProjectStatistics>();
    public DbSet<ProjectIndex> ProjectIndexes => Set<ProjectIndex>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ProjectImportJob> ProjectImportJobs => Set<ProjectImportJob>();
    public DbSet<ParsedFile> ParsedFiles => Set<ParsedFile>();
    public DbSet<ParsedClass> ParsedClasses => Set<ParsedClass>();
    public DbSet<ParsedMethod> ParsedMethods => Set<ParsedMethod>();
    public DbSet<ParsedProperty> ParsedProperties => Set<ParsedProperty>();
    public DbSet<ParsedField> ParsedFields => Set<ParsedField>();
    public DbSet<ProjectParseJob> ProjectParseJobs => Set<ProjectParseJob>();
    public DbSet<CodeChunk> CodeChunks => Set<CodeChunk>();
    public DbSet<ProjectChunkingJob> ProjectChunkingJobs => Set<ProjectChunkingJob>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply EntityTypeConfigurations
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Apply global ISoftDelete query filters
        modelBuilder.Entity<Workspace>().HasQueryFilter(w => !w.IsDeleted);
        modelBuilder.Entity<Project>().HasQueryFilter(p => !p.IsDeleted);
        modelBuilder.Entity<ProjectSettings>().HasQueryFilter(ps => !ps.IsDeleted);
        modelBuilder.Entity<ProjectIndex>().HasQueryFilter(idx => !idx.IsDeleted);
        modelBuilder.Entity<ChatSession>().HasQueryFilter(s => !s.IsDeleted);
    }

    public override int SaveChanges()
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditAndSoftDelete()
    {
        var timestamp = _dateTimeProvider.UtcNow;
        var userId = _currentUserService.UserId ?? "System";

        foreach (var entry in ChangeTracker.Entries())
        {
            // Intercept delete operations for soft-delete conversion
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete softDelete)
            {
                entry.State = EntityState.Modified;
                softDelete.IsDeleted = true;
                softDelete.DeletedAt = timestamp;
            }

            // Apply auditing fields
            if (entry.State == EntityState.Added && entry.Entity is AuditableEntity auditableAdded)
            {
                auditableAdded.CreatedAt = timestamp;
                auditableAdded.CreatedBy = userId;
            }
            else if (entry.State == EntityState.Modified && entry.Entity is AuditableEntity auditableModified)
            {
                auditableModified.LastModifiedAt = timestamp;
                auditableModified.LastModifiedBy = userId;
            }
        }
    }
}
