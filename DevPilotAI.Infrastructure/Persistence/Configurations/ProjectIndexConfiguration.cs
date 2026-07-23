using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevPilotAI.Infrastructure.Persistence.Configurations;

public class ProjectIndexConfiguration : IEntityTypeConfiguration<ProjectIndex>
{
    public void Configure(EntityTypeBuilder<ProjectIndex> builder)
    {
        builder.ToTable("ProjectIndexes");

        builder.HasKey(idx => idx.ProjectId);

        builder.Property(idx => idx.IndexVersion)
            .IsRequired()
            .HasMaxLength(50);

        // Store IndexStatus Enum as string
        builder.Property(idx => idx.IndexStatus)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(idx => idx.EmbeddingModel)
            .HasMaxLength(100);

        builder.Property(idx => idx.ParserVersion)
            .HasMaxLength(50);

        // Mapped Indexes
        builder.HasIndex(idx => idx.IndexStatus);
        builder.HasIndex(idx => idx.CreatedAt);
        builder.HasIndex(idx => idx.LastModifiedAt);
    }
}
