using System.Collections.Generic;
using System.Text.Json;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevPilotAI.Infrastructure.Persistence.Configurations;

public class ParsedFileConfiguration : IEntityTypeConfiguration<ParsedFile>
{
    public void Configure(EntityTypeBuilder<ParsedFile> builder)
    {
        builder.ToTable("ParsedFiles");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.RelativePath)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(f => f.Language)
            .HasMaxLength(50)
            .IsRequired();

        // Serialize List<string> Usings as JSON
        builder.Property(f => f.Usings)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>())
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(f => f.ProjectId);
        builder.HasIndex(f => f.Language);

        // Relationship: Project -> ParsedFiles
        builder.HasOne(f => f.Project)
            .WithMany()
            .HasForeignKey(f => f.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
