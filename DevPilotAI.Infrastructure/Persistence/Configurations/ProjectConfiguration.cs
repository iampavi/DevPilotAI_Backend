using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevPilotAI.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Description)
            .HasMaxLength(500);

        builder.Property(p => p.SourceLocation)
            .HasMaxLength(2000);

        // Store ProjectType Enum as string
        builder.Property(p => p.ProjectType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // Concurrency token (RowVersion)
        builder.Property(p => p.RowVersion)
            .IsRowVersion();

        // Mapped Indexes
        builder.HasIndex(p => p.Name);
        builder.HasIndex(p => p.WorkspaceId);
        builder.HasIndex(p => p.ProjectType);
        builder.HasIndex(p => p.CreatedAt);
        builder.HasIndex(p => p.LastModifiedAt);

        // Relationships (One-to-One configurations mapping Guid ProjectId as PK/FK)
        builder.HasOne(p => p.Settings)
            .WithOne(s => s.Project)
            .HasForeignKey<ProjectSettings>(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Statistics)
            .WithOne(s => s.Project)
            .HasForeignKey<ProjectStatistics>(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Index)
            .WithOne(idx => idx.Project)
            .HasForeignKey<ProjectIndex>(idx => idx.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
