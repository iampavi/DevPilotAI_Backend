using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevPilotAI.Infrastructure.Persistence.Configurations;

public class CodeChunkConfiguration : IEntityTypeConfiguration<CodeChunk>
{
    public void Configure(EntityTypeBuilder<CodeChunk> builder)
    {
        builder.ToTable("CodeChunks");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ChunkType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.Content)
            .IsRequired();

        builder.Property(c => c.Hash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(c => c.EmbeddingModel)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(c => c.ProjectId);
        builder.HasIndex(c => c.ParsedFileId);
        builder.HasIndex(c => c.Hash);

        // Relation: Project -> CodeChunks
        builder.HasOne(c => c.Project)
            .WithMany()
            .HasForeignKey(c => c.ProjectId)
            .OnDelete(DeleteBehavior.NoAction);

        // Relation: ParsedFile -> CodeChunks
        builder.HasOne(c => c.ParsedFile)
            .WithMany()
            .HasForeignKey(c => c.ParsedFileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
