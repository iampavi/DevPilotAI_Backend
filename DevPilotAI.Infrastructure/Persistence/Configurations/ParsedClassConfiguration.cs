using System.Collections.Generic;
using System.Text.Json;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevPilotAI.Infrastructure.Persistence.Configurations;

public class ParsedClassConfiguration : IEntityTypeConfiguration<ParsedClass>
{
    public void Configure(EntityTypeBuilder<ParsedClass> builder)
    {
        builder.ToTable("ParsedClasses");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.FullName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(c => c.Namespace)
            .HasMaxLength(300);

        builder.Property(c => c.SymbolType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // Serializations
        builder.Property(c => c.BaseTypes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>())
            .HasColumnType("nvarchar(max)");

        builder.Property(c => c.Attributes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>())
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(c => c.ParsedFileId);
        builder.HasIndex(c => c.FullName);

        // Relation: ParsedFile -> ParsedClasses
        builder.HasOne(c => c.ParsedFile)
            .WithMany(f => f.Classes)
            .HasForeignKey(c => c.ParsedFileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
