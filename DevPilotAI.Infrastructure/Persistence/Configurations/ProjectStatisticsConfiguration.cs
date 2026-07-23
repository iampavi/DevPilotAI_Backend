using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevPilotAI.Infrastructure.Persistence.Configurations;

public class ProjectStatisticsConfiguration : IEntityTypeConfiguration<ProjectStatistics>
{
    public void Configure(EntityTypeBuilder<ProjectStatistics> builder)
    {
        builder.ToTable("ProjectStatistics");

        builder.HasKey(s => s.ProjectId);

        builder.Property(s => s.FileCount).IsRequired();
        builder.Property(s => s.TotalLinesOfCode).IsRequired();
        builder.Property(s => s.TotalBytes).IsRequired();
        builder.Property(s => s.IndexedFileCount).IsRequired();

        builder.Property(s => s.ControllerCount).IsRequired();
        builder.Property(s => s.ServiceCount).IsRequired();
        builder.Property(s => s.RepositoryCount).IsRequired();
        builder.Property(s => s.ApiCount).IsRequired();
        builder.Property(s => s.ClassCount).IsRequired();
    }
}
