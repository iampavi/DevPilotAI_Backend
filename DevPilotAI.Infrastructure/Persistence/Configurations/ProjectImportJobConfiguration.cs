using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevPilotAI.Infrastructure.Persistence.Configurations;

public class ProjectImportJobConfiguration : IEntityTypeConfiguration<ProjectImportJob>
{
    public void Configure(EntityTypeBuilder<ProjectImportJob> builder)
    {
        builder.ToTable("ProjectImportJobs");

        builder.HasKey(j => j.Id);

        // Store enums as string
        builder.Property(j => j.ImportType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(j => j.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(j => j.Error)
            .HasMaxLength(2000);

        // Configure indexes
        builder.HasIndex(j => j.ProjectId);
        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.ImportType);

        // One-to-Many Relationship with Project
        builder.HasOne(j => j.Project)
            .WithMany()
            .HasForeignKey(j => j.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
