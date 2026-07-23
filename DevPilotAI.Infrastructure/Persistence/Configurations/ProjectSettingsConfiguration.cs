using System.Text.Json;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevPilotAI.Infrastructure.Persistence.Configurations;

public class ProjectSettingsConfiguration : IEntityTypeConfiguration<ProjectSettings>
{
    public void Configure(EntityTypeBuilder<ProjectSettings> builder)
    {
        builder.ToTable("ProjectSettings");

        builder.HasKey(s => s.ProjectId);

        builder.Property(s => s.MaxFileSizeInBytes)
            .IsRequired();

        // Convert List<string> properties to JSON string in SQL Server
        builder.Property(s => s.ExcludedFolders)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>()
            )
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(s => s.ExcludedExtensions)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>()
            )
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        // Auditing indexes
        builder.HasIndex(s => s.CreatedAt);
        builder.HasIndex(s => s.LastModifiedAt);
    }
}
