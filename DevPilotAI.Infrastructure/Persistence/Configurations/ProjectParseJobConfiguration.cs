using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevPilotAI.Infrastructure.Persistence.Configurations;

public class ProjectParseJobConfiguration : IEntityTypeConfiguration<ProjectParseJob>
{
    public void Configure(EntityTypeBuilder<ProjectParseJob> builder)
    {
        builder.ToTable("ProjectParseJobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(j => j.Error)
            .HasMaxLength(2000);

        builder.HasIndex(j => j.ProjectId);
        builder.HasIndex(j => j.Status);

        // Relation: Project -> ProjectParseJobs
        builder.HasOne(j => j.Project)
            .WithMany()
            .HasForeignKey(j => j.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
